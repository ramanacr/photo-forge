using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PhotoForge.Core.Models;
using PhotoForge.Core.Services;

namespace PhotoForge.Storage.Database;

/// <summary>
/// SQLite local operational repository for audit records, migration history, and candidate caching.
/// </summary>
public class AuditDatabase : IAuditRepository, IDisposable
{
    private readonly string _connectionString;
    private readonly string _dbPath;
    private bool _initialized;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AuditDatabase(string? customDbPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customDbPath))
        {
            _dbPath = customDbPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "PhotoForge");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _dbPath = Path.Combine(dir, "photoforge_history.db");
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Migrations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OperationId TEXT NOT NULL,
                    TargetPath TEXT NOT NULL,
                    TargetFingerprint TEXT,
                    SourcePath TEXT,
                    SourceFingerprint TEXT,
                    Profile TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    DurationMs INTEGER NOT NULL,
                    DiffJson TEXT,
                    VerificationJson TEXT,
                    ErrorMessage TEXT,
                    ProcessedAtUtc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Batches (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    BatchId TEXT NOT NULL UNIQUE,
                    TotalItems INTEGER NOT NULL,
                    SucceededCount INTEGER NOT NULL,
                    WarningsCount INTEGER NOT NULL,
                    SkippedCount INTEGER NOT NULL,
                    NoMatchCount INTEGER NOT NULL,
                    FailedCount INTEGER NOT NULL,
                    DurationMs INTEGER NOT NULL,
                    StartedAtUtc TEXT NOT NULL,
                    FinishedAtUtc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS CandidateCache (
                    FilePath TEXT PRIMARY KEY,
                    Sha256 TEXT NOT NULL,
                    PerceptualHash INTEGER NOT NULL,
                    LastSeenAtUtc TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_Migrations_TargetFingerprint ON Migrations(TargetFingerprint);
                CREATE INDEX IF NOT EXISTS IX_Migrations_SourceFingerprint ON Migrations(SourceFingerprint);
            ";

            await cmd.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RecordMigrationAsync(OperationResult result, string profileName, CancellationToken ct = default)
    {
        await InitializeAsync(ct);

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Migrations (
                OperationId, TargetPath, TargetFingerprint, SourcePath, SourceFingerprint,
                Profile, Status, DurationMs, DiffJson, VerificationJson, ErrorMessage, ProcessedAtUtc
            ) VALUES (
                @opId, @targetPath, @targetFp, @sourcePath, @sourceFp,
                @profile, @status, @duration, @diff, @verification, @error, @ts
            );
        ";

        cmd.Parameters.AddWithValue("@opId", result.OperationId);
        cmd.Parameters.AddWithValue("@targetPath", result.TargetRef.FilePath);
        cmd.Parameters.AddWithValue("@targetFp", (object?)result.TargetRef.Sha256Fingerprint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sourcePath", (object?)result.OriginalRef?.FilePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sourceFp", (object?)result.OriginalRef?.Sha256Fingerprint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@profile", profileName);
        cmd.Parameters.AddWithValue("@status", result.Status.ToString());
        cmd.Parameters.AddWithValue("@duration", (long)result.Duration.TotalMilliseconds);
        cmd.Parameters.AddWithValue("@diff", JsonSerializer.Serialize(result.Diff));
        cmd.Parameters.AddWithValue("@verification", result.Verification != null ? JsonSerializer.Serialize(result.Verification) : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@error", (object?)result.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ts", result.CompletedAtUtc.ToString("O"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<MigrationMarker?> GetMigrationRecordAsync(string targetFingerprint, CancellationToken ct = default)
    {
        await InitializeAsync(ct);

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT SourceFingerprint, Profile, ProcessedAtUtc 
            FROM Migrations 
            WHERE TargetFingerprint = @fp AND (Status = 'Success' OR Status = 'SuccessWithWarnings')
            ORDER BY Id DESC LIMIT 1;
        ";
        cmd.Parameters.AddWithValue("@fp", targetFingerprint);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var src = reader.GetString(0);
            var prof = reader.GetString(1);
            var ts = DateTime.Parse(reader.GetString(2));

            return new MigrationMarker
            {
                Processed = true,
                SourceFingerprint = src,
                Profile = prof,
                MigrationVersion = 1,
                EngineVersion = "1.0.0",
                ProcessedAtUtc = ts
            };
        }

        return null;
    }

    public async Task RecordBatchAsync(BatchSummary summary, CancellationToken ct = default)
    {
        await InitializeAsync(ct);

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Batches (
                BatchId, TotalItems, SucceededCount, WarningsCount, SkippedCount,
                NoMatchCount, FailedCount, DurationMs, StartedAtUtc, FinishedAtUtc
            ) VALUES (
                @batchId, @total, @succ, @warn, @skip, @nomatch, @fail, @duration, @start, @finish
            );
        ";

        cmd.Parameters.AddWithValue("@batchId", summary.BatchId);
        cmd.Parameters.AddWithValue("@total", summary.TotalItems);
        cmd.Parameters.AddWithValue("@succ", summary.SucceededCount);
        cmd.Parameters.AddWithValue("@warn", summary.WarningsCount);
        cmd.Parameters.AddWithValue("@skip", summary.SkippedCount);
        cmd.Parameters.AddWithValue("@nomatch", summary.NoMatchCount);
        cmd.Parameters.AddWithValue("@fail", summary.FailedCount);
        cmd.Parameters.AddWithValue("@duration", (long)summary.TotalDuration.TotalMilliseconds);
        cmd.Parameters.AddWithValue("@start", summary.StartedAtUtc.ToString("O"));
        cmd.Parameters.AddWithValue("@finish", summary.FinishedAtUtc.ToString("O"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<OperationResult>> GetRecentHistoryAsync(int limit = 100, CancellationToken ct = default)
    {
        await InitializeAsync(ct);

        var list = new List<OperationResult>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT OperationId, TargetPath, TargetFingerprint, SourcePath, SourceFingerprint,
                   Profile, Status, DurationMs, DiffJson, VerificationJson, ErrorMessage, ProcessedAtUtc
            FROM Migrations
            ORDER BY Id DESC
            LIMIT @limit;
        ";
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var opId = reader.GetString(0);
            var targetPath = reader.GetString(1);
            var targetFp = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var srcPath = reader.IsDBNull(3) ? null : reader.GetString(3);
            var srcFp = reader.IsDBNull(4) ? "" : reader.GetString(4);
            var statusStr = reader.GetString(6);
            Enum.TryParse<OperationStatus>(statusStr, out var status);
            var durationMs = reader.GetInt64(7);
            var diffJson = reader.IsDBNull(8) ? "{}" : reader.GetString(8);
            var verJson = reader.IsDBNull(9) ? null : reader.GetString(9);
            var error = reader.IsDBNull(10) ? null : reader.GetString(10);
            var ts = DateTime.Parse(reader.GetString(11));

            var diff = JsonSerializer.Deserialize<MetadataDiff>(diffJson) ?? new MetadataDiff();
            VerificationResult? ver = !string.IsNullOrEmpty(verJson) ? JsonSerializer.Deserialize<VerificationResult>(verJson) : null;

            var targetRef = PhotoRef.Create(targetPath, PhotoFormat.Jpeg, 0, targetFp);
            PhotoRef? srcRef = srcPath != null ? PhotoRef.Create(srcPath, PhotoFormat.Jpeg, 0, srcFp) : null;

            list.Add(new OperationResult
            {
                OperationId = opId,
                TargetRef = targetRef,
                OriginalRef = srcRef,
                Status = status,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Diff = diff,
                Verification = ver,
                ErrorMessage = error,
                CompletedAtUtc = ts
            });
        }

        return list;
    }

    public async Task CacheCandidateFingerprintAsync(string filePath, string sha256, ulong perceptualHash, CancellationToken ct = default)
    {
        await InitializeAsync(ct);

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO CandidateCache (FilePath, Sha256, PerceptualHash, LastSeenAtUtc)
            VALUES (@path, @sha, @phash, @ts);
        ";
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.Parameters.AddWithValue("@sha", sha256);
        cmd.Parameters.AddWithValue("@phash", (long)perceptualHash);
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
