using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Models.IO;

/// <summary>
/// 一条自动保存快照。<paramref name="TargetUri"/> 是恢复目标的存储 URI，
/// <paramref name="TargetPath"/> 只用于展示（<c>file:</c> 之外的平台没有本地路径）。
/// </summary>
public sealed record AutoSaveSnapshot(
    long Id,
    string SessionKey,
    string? TargetUri,
    string? TargetPath,
    long SavedTime,
    string Content);

/// <summary>
/// 自动保存快照库。和 <see cref="ChartEditManager"/> 一样落在 <c>MajEnv.DatabaseFile</c> 上，
/// 但走独立连接：一张表 + 事务就能表达「写入 + 轮转」，不再需要 <c>.autosave/</c> 目录、
/// 变长文件名的生成规则，以及那份额外维护、会被写坏的 <c>.index.json</c>。
///
/// 一次编辑只写一行——快照本来就只有一份内容，按会话记够即止；
/// 「本地 / 全局」这种曾经靠两个目录区分的概念，在单表里已经从数据里派生得出来，不再占列。
///
/// 并发约定：连接本身不线程安全，全部访问都在 <see cref="_gate"/> 后面；
/// 同步的 Sqlite 调用再包一层 <see cref="Task.Run(Func{Task})"/> 甩出 UI 线程。
/// 写路径的异常一律吞成 <c>false</c>——自动保存失败不该打断编辑。
/// </summary>
public sealed class AutoSaveStore : IDisposable
{
    /// <summary>全库硬上限。每会话的额度见 <see cref="SaveAsync"/>，这里只防库无限长。</summary>
    private const int TotalCap = 200;

    private const string TableSql = """
        CREATE TABLE IF NOT EXISTS AutoSaves (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SessionKey TEXT NOT NULL,
            TargetUri TEXT,
            TargetPath TEXT,
            SavedTime INTEGER NOT NULL,
            Content TEXT NOT NULL
        )
        """;

    private const string IndexSql = """
        CREATE INDEX IF NOT EXISTS IX_AutoSaves_Session_Saved
        ON AutoSaves (SessionKey, SavedTime DESC)
        """;

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public AutoSaveStore(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        Execute("PRAGMA journal_mode=WAL");
        Execute("PRAGMA busy_timeout=3000");
        Execute(TableSql);
        Execute(IndexSql);
        DropLegacyScope();
    }

    /// <summary>
    /// 存一条快照并在同一事务里轮转：该会话只保留最新的 <paramref name="keepPerChart"/> 条，
    /// 整表再压到 <see cref="TotalCap"/> 条以内。
    /// </summary>
    /// <returns>写入并完成轮转返回 <c>true</c>；失败返回 <c>false</c>。</returns>
    public Task<bool> SaveAsync(
        string sessionKey,
        string? targetUri,
        string? targetPath,
        string content,
        int keepPerChart) => Task.Run(async () =>
    {
        if (_disposed) return false;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            using var transaction = _connection.BeginTransaction();

            using (var insert = _connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO AutoSaves (SessionKey, TargetUri, TargetPath, SavedTime, Content)
                    VALUES (@sessionKey, @uri, @path, @savedTime, @content)
                    """;
                insert.Parameters.AddWithValue("@sessionKey", sessionKey);
                insert.Parameters.AddWithValue("@uri", (object?)targetUri ?? DBNull.Value);
                insert.Parameters.AddWithValue("@path", (object?)targetPath ?? DBNull.Value);
                insert.Parameters.AddWithValue("@savedTime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                insert.Parameters.AddWithValue("@content", content);
                insert.ExecuteNonQuery();
            }

            Rotate(transaction, sessionKey, keepPerChart);
            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoSaveStore] save: {ex.Message}");
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }, CancellationToken.None);

    /// <summary>按时间倒序取快照。</summary>
    public Task<IReadOnlyList<AutoSaveSnapshot>> QueryAsync(int limit) => Task.Run(async () =>
    {
        IReadOnlyList<AutoSaveSnapshot> result = [];
        if (_disposed) return result;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT Id, SessionKey, TargetUri, TargetPath, SavedTime, Content
                FROM AutoSaves ORDER BY SavedTime DESC, Id DESC LIMIT @limit
                """;
            command.Parameters.AddWithValue("@limit", limit);

            using var reader = command.ExecuteReader();
            var list = new List<AutoSaveSnapshot>();
            while (list.Count < limit && reader.Read())
            {
                list.Add(new AutoSaveSnapshot(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetInt64(4),
                    reader.GetString(5)));
            }

            return (IReadOnlyList<AutoSaveSnapshot>)list;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoSaveStore] query: {ex.Message}");
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }, CancellationToken.None);

    /// <summary>删掉一条快照（恢复成功后调用）。</summary>
    public Task DeleteAsync(long id) => Task.Run(async () =>
    {
        if (_disposed) return;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM AutoSaves WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoSaveStore] delete: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }, CancellationToken.None);

    private static void Rotate(SqliteTransaction transaction, string sessionKey, int keepPerChart)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM AutoSaves
            WHERE SessionKey = @sessionKey AND Id NOT IN (
                SELECT Id FROM AutoSaves WHERE SessionKey = @sessionKey
                ORDER BY SavedTime DESC, Id DESC LIMIT @keep
            );

            DELETE FROM AutoSaves
            WHERE Id NOT IN (
                SELECT Id FROM AutoSaves ORDER BY SavedTime DESC, Id DESC LIMIT @total
            )
            """;
        command.Parameters.AddWithValue("@keep", Math.Max(1, keepPerChart));
        command.Parameters.AddWithValue("@total", TotalCap);
        command.Parameters.AddWithValue("@sessionKey", sessionKey);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 早期版本对每次编辑按「本地 / 全局」双写两行同样的内容。改单行存储后
    /// 按 (会话, 内容) 去重、整表重建去掉 Scope 列；没有该列就是新库，直接跳过。
    /// 用重建而不是 <c>DROP COLUMN</c>：旧索引引用了 Scope，重建顺带把索引也换掉。
    /// </summary>
    private void DropLegacyScope()
    {
        using var probe = _connection.CreateCommand();
        probe.CommandText = "SELECT COUNT(*) FROM pragma_table_info('AutoSaves') WHERE name = 'Scope'";
        if (Convert.ToInt64(probe.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture) == 0) return;

        using var transaction = _connection.BeginTransaction();
        try
        {
            using var rebuild = _connection.CreateCommand();
            rebuild.Transaction = transaction;
            rebuild.CommandText = """
                CREATE TABLE AutoSaves_Migrated (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionKey TEXT NOT NULL,
                    TargetUri TEXT,
                    TargetPath TEXT,
                    SavedTime INTEGER NOT NULL,
                    Content TEXT NOT NULL
                );

                INSERT INTO AutoSaves_Migrated (SessionKey, TargetUri, TargetPath, SavedTime, Content)
                SELECT SessionKey, TargetUri, TargetPath, SavedTime, Content FROM AutoSaves
                WHERE Id IN (SELECT MAX(Id) FROM AutoSaves GROUP BY SessionKey, Content);

                DROP TABLE AutoSaves;
                ALTER TABLE AutoSaves_Migrated RENAME TO AutoSaves;
                """;
            rebuild.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Debug.WriteLine($"[AutoSaveStore] migrate: {ex.Message}");
            return;
        }

        Execute(IndexSql);
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection.Dispose();
        _gate.Dispose();
    }

    /// <summary>把 unix 秒数格式化成快照列表里显示的时间。</summary>
    public static string FormatTime(long savedTime) =>
        DateTimeOffset.FromUnixTimeSeconds(savedTime)
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
