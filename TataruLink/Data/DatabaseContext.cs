using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Microsoft.Data.Sqlite;
using TataruLink.Configuration;
using TataruLink.Services;

namespace TataruLink.Data;

/// <summary>
/// Database context for managing SQLite connections and initialization
/// </summary>
public class DatabaseContext : IDisposable, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly CacheConfig config;
    private readonly SemaphoreSlim connectionSemaphore;
    private SqliteConnection? sharedConnection;
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private bool isDisposed;

    public DatabaseContext(IDalamudPluginInterface pluginInterface, CacheConfig config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        
        var configDir = pluginInterface.GetPluginConfigDirectory();
        var dbPath = Path.Combine(configDir, config.DatabaseFileName);
        
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = config.ConnectionTimeoutSeconds,
            Pooling = false // Disable pooling, we'll manage our own connection
        };
        
        connectionString = builder.ConnectionString;
        
        // Use semaphore to limit concurrent database access
        // SQLite can handle multiple readers but only one writer
        connectionSemaphore = new SemaphoreSlim(1, 1);
    }

    public async Task<SqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        // For SQLite, it's better to use a single connection with proper locking
        // This prevents "database is locked" errors
        await connectionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        
        try
        {
            connectionLock.Wait(cancellationToken);
            try
            {
                if (sharedConnection == null || sharedConnection.State != System.Data.ConnectionState.Open)
                {
                    sharedConnection?.Dispose();
                    sharedConnection = new SqliteConnection(connectionString);
                    sharedConnection.Open();
                    
                    if (config.EnableWal)
                    {
                        await using var cmd = sharedConnection.CreateCommand();
                        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=30000;";
                        cmd.ExecuteNonQuery();
                    }
                }
                
                return sharedConnection;
            }
            finally
            {
                connectionLock.Release();
            }
        }
        catch
        {
            connectionSemaphore.Release();
            throw;
        }
    }
    
    public void ReleaseConnection()
    {
        connectionSemaphore.Release();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CreateTablesAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ReleaseConnection();
        }
    }

    private static async Task CreateTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        // Using parameterized queries isn't possible for DDL statements,
        // but these are hardcoded strings with no user input
        const string createTablesSql = @"
            -- Translation cache table
            CREATE TABLE IF NOT EXISTS TranslationCache (
                Id TEXT PRIMARY KEY,
                OriginalText TEXT NOT NULL,
                TranslatedText TEXT NOT NULL,
                SourceLanguage TEXT NOT NULL,
                DetectedLanguage TEXT,
                TargetLanguage TEXT NOT NULL,
                Provider TEXT NOT NULL,
                CreatedAt INTEGER NOT NULL,
                LastAccessedAt INTEGER NOT NULL,
                AccessCount INTEGER DEFAULT 1,
                CharacterCount INTEGER,
                TimeTakenMs INTEGER,
                CacheKey TEXT NOT NULL UNIQUE
            );
            
            -- Chat history table
            CREATE TABLE IF NOT EXISTS ChatHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MessageId INTEGER NOT NULL UNIQUE,
                Timestamp INTEGER NOT NULL,
                ChatType INTEGER NOT NULL,
                ChatTypeName TEXT,
                SenderName TEXT,
                OriginalContent TEXT NOT NULL,
                TranslatedContent TEXT,
                TranslationCacheId TEXT,
                IsVisible INTEGER DEFAULT 1,
                FOREIGN KEY (TranslationCacheId) REFERENCES TranslationCache(Id) ON DELETE SET NULL
            );
            
            -- Create indexes for better performance
            CREATE INDEX IF NOT EXISTS idx_cache_key ON TranslationCache(CacheKey);
            CREATE INDEX IF NOT EXISTS idx_cache_access ON TranslationCache(LastAccessedAt DESC);
            CREATE INDEX IF NOT EXISTS idx_cache_language ON TranslationCache(SourceLanguage, TargetLanguage);
            CREATE INDEX IF NOT EXISTS idx_history_timestamp ON ChatHistory(Timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_history_visible ON ChatHistory(IsVisible, Timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_history_message_id ON ChatHistory(MessageId);";

        await using var command = connection.CreateCommand();
        command.CommandText = createTablesSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task VacuumAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "VACUUM;";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            
            Service.PluginLog.Information("Database vacuum completed");
        }
        finally
        {
            ReleaseConnection();
        }
    }

    public async Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT page_count * page_size FROM pragma_page_count(), pragma_page_size();";
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(result);
        }
        finally
        {
            ReleaseConnection();
        }
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
            throw new ObjectDisposedException(nameof(DatabaseContext));
    }

    public void Dispose()
    {
        if (isDisposed) return;
        
        connectionLock.Wait();
        try
        {
            sharedConnection?.Dispose();
            sharedConnection = null;
        }
        finally
        {
            connectionLock.Release();
        }
        
        connectionLock.Dispose();
        connectionSemaphore.Dispose();
        isDisposed = true;
    }
    
    public async ValueTask DisposeAsync()
    {
        if (isDisposed) return;
        
        await connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (sharedConnection != null)
            {
                await sharedConnection.DisposeAsync().ConfigureAwait(false);
                sharedConnection = null;
            }
        }
        finally
        {
            connectionLock.Release();
        }
        
        connectionLock.Dispose();
        connectionSemaphore.Dispose();
        isDisposed = true;
    }
}
