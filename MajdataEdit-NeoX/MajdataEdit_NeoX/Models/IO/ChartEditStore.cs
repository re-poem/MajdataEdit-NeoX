using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;

namespace MajdataEdit_NeoX.Models.IO;

/// <summary>某个谱面的编辑现场：上次停在哪个难度、播放头在哪、累计编辑了多久。</summary>
public readonly record struct ChartEditRecord(
    int SelectedDifficulty,
    double TrackTime,
    TimeSpan TotalEditDuration,
    string ChartPath);

/// <summary>
/// 谱面编辑记录库。和 <see cref="AutoSaveStore"/> 一样落在 <c>MajEnv.DatabaseFile</c> 上，
/// 但只管一张 <c>ChartEditRecords</c> 表：按谱面路径记「上次编辑到哪」，
/// 下次打开同一份谱面时恢复选中难度、播放头位置和累计编辑时长。
///
/// 与 <see cref="AutoSaveStore"/> 的差异是刻意的：那边是后台高频写入，所以异步 + 信号量；
/// 这里是低频、只在打开 / 切换 / 关闭谱面时读写一行，因此保持同步签名——
/// 改成 async 只会把「读回来之前用户又切了谱面」的竞态引进调用方，换不来任何收益。
///
/// 连接不线程安全，但本类的所有调用都来自 UI 线程，因此不需要额外加锁。
/// 读写的异常一律吞成「没有记录」：编辑记录是锦上添花，坏了不该挡住打开谱面。
/// </summary>
public sealed class ChartEditStore : IDisposable
{
    private const string TableSql = """
        CREATE TABLE IF NOT EXISTS ChartEditRecords (
            ChartPath TEXT PRIMARY KEY,
            SelectedDifficulty INTEGER NOT NULL DEFAULT 0,
            TrackTime REAL NOT NULL DEFAULT 0,
            TotalEditDurationTicks INTEGER NOT NULL DEFAULT 0
        )
        """;

    private readonly SqliteConnection _connection;
    private bool _disposed;

    public ChartEditStore(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        // WAL 是库级持久属性，正常情况下 <see cref="AutoSaveStore"/> 已经设过；
        // 但这里不该依赖构造顺序，自己再说一遍。busy_timeout 则必须自备——
        // 和后台快照写入撞上时，没有它就只能靠默认的 30 秒命令超时干等。
        Execute("PRAGMA journal_mode=WAL");
        Execute("PRAGMA busy_timeout=3000");
        Execute(TableSql);
    }

    /// <summary>取某个谱面的编辑记录；没有记录或读失败时返回 <c>null</c>。</summary>
    public ChartEditRecord? GetRecord(string chartPath)
    {
        if (_disposed) return null;

        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT ChartPath, SelectedDifficulty, TrackTime, TotalEditDurationTicks
                FROM ChartEditRecords
                WHERE ChartPath = @path
                """;
            command.Parameters.AddWithValue("@path", chartPath);

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;

            return new ChartEditRecord(
                reader.GetInt32(1),
                reader.GetDouble(2),
                TimeSpan.FromTicks(reader.GetInt64(3)),
                reader.GetString(0));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChartEditStore] get: {ex.Message}");
            return null;
        }
    }

    /// <summary>写入（或覆盖）一条编辑记录。失败只记日志。</summary>
    public void UpsertRecord(ChartEditRecord record)
    {
        if (_disposed) return;

        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ChartEditRecords
                    (ChartPath, SelectedDifficulty, TrackTime, TotalEditDurationTicks)
                VALUES (@path, @difficulty, @trackTime, @duration)
                ON CONFLICT(ChartPath) DO UPDATE SET
                    SelectedDifficulty = excluded.SelectedDifficulty,
                    TrackTime = excluded.TrackTime,
                    TotalEditDurationTicks = excluded.TotalEditDurationTicks
                """;
            command.Parameters.AddWithValue("@path", record.ChartPath);
            command.Parameters.AddWithValue("@difficulty", record.SelectedDifficulty);
            command.Parameters.AddWithValue("@trackTime", record.TrackTime);
            command.Parameters.AddWithValue("@duration", record.TotalEditDuration.Ticks);
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChartEditStore] upsert: {ex.Message}");
        }
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
    }
}
