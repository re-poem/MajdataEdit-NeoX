using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.Globalization;

namespace MajdataEdit_NeoX.Models.IO;

/// <summary>
/// 会话标记：判断上一次进程是不是「正常走完关闭流程」退出的。
///
/// 做法和 Vim 的 <c>.swp</c>、Word 的 autorecover 同一套——启动时把标记置脏，
/// 正常关闭时置净；下次启动读到脏标记，就说明上次是被强杀 / 崩溃 / 拔电结束的，
/// 这时的快照可能还是用户唯一的一份内容，值得主动提示一次。
///
/// 存在 <c>editor.db</c> 里而不是单独文件：少一个要维护的路径，
/// 也不会在「库打不开」之外多出一种失败模式（那种情况下本来就没快照可提示）。
///
/// 连接在 <see cref="Begin"/> 里才打开，且整条链路都不抛：这只是锦上添花的提示，
/// 装到只读目录、库被别的进程占死时，宁可安静地不提示，也不能因此起不来。
/// </summary>
public sealed class SessionStore : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _connection;
    private bool _disposed;

    public SessionStore(string dbPath) => _dbPath = dbPath;

    /// <summary>
    /// 开一个会话：先读上次是否脏，再把本次标脏。两步在同一事务里完成，
    /// 所以「读到脏」和「覆盖掉它」之间不会被别的实例插进来。
    /// </summary>
    /// <returns>上次退出不干净时返回当时的启动时间（unix 秒），否则 <c>null</c>。</returns>
    public long? Begin()
    {
        if (_disposed) return null;

        try
        {
            var connection = Open();

            using var transaction = connection.BeginTransaction();

            long? interrupted = null;
            using (var probe = connection.CreateCommand())
            {
                probe.Transaction = transaction;
                probe.CommandText = "SELECT Dirty, StartedTime FROM AppSession WHERE Id = 1";
                using var reader = probe.ExecuteReader();
                if (reader.Read() && reader.GetInt64(0) != 0)
                    interrupted = reader.GetInt64(1);
            }

            using (var mark = connection.CreateCommand())
            {
                mark.Transaction = transaction;
                mark.CommandText = """
                    INSERT INTO AppSession (Id, Dirty, StartedTime) VALUES (1, 1, @now)
                    ON CONFLICT(Id) DO UPDATE SET Dirty = 1, StartedTime = excluded.StartedTime
                    """;
                mark.Parameters.AddWithValue("@now", Now());
                mark.ExecuteNonQuery();
            }

            transaction.Commit();
            return interrupted;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SessionStore] begin: {ex.Message}");
            return null;
        }
    }

    /// <summary>正常关闭：把标记置净。下次启动就不会提示。</summary>
    public void End()
    {
        if (_disposed || _connection is not { } connection) return;

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE AppSession SET Dirty = 0 WHERE Id = 1";
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SessionStore] end: {ex.Message}");
        }
    }

    private SqliteConnection Open()
    {
        if (_connection is { } existing) return existing;

        var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        try
        {
            Execute(connection, "PRAGMA busy_timeout=3000");
            Execute(connection, """
                CREATE TABLE IF NOT EXISTS AppSession (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    Dirty INTEGER NOT NULL,
                    StartedTime INTEGER NOT NULL
                )
                """);
        }
        catch
        {
            // 建表失败就别留着这条半残连接，否则 End() 会拿着它去写一个不存在的表。
            connection.Dispose();
            throw;
        }

        _connection = connection;
        return connection;
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>把 <see cref="Begin"/> 返回的启动时间格式化进提示文案。</summary>
    public static string FormatStartedTime(long startedTime) =>
        DateTimeOffset.FromUnixTimeSeconds(startedTime)
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection?.Dispose();
        _connection = null;
    }
}
