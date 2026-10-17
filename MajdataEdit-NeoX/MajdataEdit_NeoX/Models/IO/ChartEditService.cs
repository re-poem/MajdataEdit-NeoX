using Microsoft.Data.Sqlite;
using System;

namespace MajdataEdit_NeoX.Models.IO;

public sealed class ChartEditService : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public ChartEditService(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ChartEditRecords (
                ChartPath TEXT PRIMARY KEY,
                SelectedDifficulty INTEGER NOT NULL DEFAULT 0,
                TrackTime REAL NOT NULL DEFAULT 0,
                TotalEditDurationTicks INTEGER NOT NULL DEFAULT 0
            )
            """;
        cmd.ExecuteNonQuery();
    }

    public ChartEditRecord? GetRecord(string chartPath)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT ChartPath, SelectedDifficulty, TrackTime, TotalEditDurationTicks
            FROM ChartEditRecords
            WHERE ChartPath = @path
            """;
        cmd.Parameters.AddWithValue("@path", chartPath);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new ChartEditRecord(
            reader.GetInt32(1),
            reader.GetDouble(2),
            TimeSpan.FromTicks(reader.GetInt64(3)),
            reader.GetString(0));
    }

    public void UpsertRecord(ChartEditRecord record)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ChartEditRecords
                (ChartPath, SelectedDifficulty, TrackTime, TotalEditDurationTicks)
            VALUES (@path, @difficulty, @trackTime, @duration)
            ON CONFLICT(ChartPath) DO UPDATE SET
                SelectedDifficulty = excluded.SelectedDifficulty,
                TrackTime = excluded.TrackTime,
                TotalEditDurationTicks = excluded.TotalEditDurationTicks
            """;
        cmd.Parameters.AddWithValue("@path", record.ChartPath);
        cmd.Parameters.AddWithValue("@difficulty", record.SelectedDifficulty);
        cmd.Parameters.AddWithValue("@trackTime", record.TrackTime);
        cmd.Parameters.AddWithValue("@duration", record.TotalEditDuration.Ticks);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection.Dispose();
    }
}
