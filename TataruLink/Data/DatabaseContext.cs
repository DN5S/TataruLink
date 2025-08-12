using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Microsoft.Data.Sqlite;
using TataruLink.Configuration;
using TataruLink.Services;

namespace TataruLink.Data;

public class DatabaseContext : IDisposable, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly CacheConfig config;
    private readonly SemaphoreSlim connectionSemaphore;
    private SqliteConnection? sharedConnection;
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private SqliteTransaction? currentTransaction;
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
            Pooling = false
        };
        
        connectionString = builder.ConnectionString;
        
        // WARNING: SQLite only supports one writer - semaphore prevents locking
        connectionSemaphore = new SemaphoreSlim(1, 1);
    }

    public async Task<SqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        // Use a timeout to prevent infinite waiting
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
        
        // If we're in a transaction, skip semaphore acquisition
        // The transaction already holds the semaphore
        var skipSemaphore = currentTransaction != null;
        
        if (!skipSemaphore)
        {
            // WARNING: Single connection prevents SQLite locking errors
            try
            {
                await connectionSemaphore.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Failed to acquire database connection within timeout");
            }
        }
        
        try
        {
            if (!connectionLock.Wait(TimeSpan.FromSeconds(1), timeoutCts.Token))
            {
                throw new TimeoutException("Failed to acquire connection lock");
            }
            
            try
            {
                if (sharedConnection is not { State: System.Data.ConnectionState.Open })
                {
                    sharedConnection?.Dispose();
                    sharedConnection = new SqliteConnection(connectionString);
                    await sharedConnection.OpenAsync(timeoutCts.Token).ConfigureAwait(false);
                    
                    if (config.EnableWal)
                    {
                        await using var cmd = sharedConnection.CreateCommand();
                        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=30000;";
                        await cmd.ExecuteNonQueryAsync(timeoutCts.Token).ConfigureAwait(false);
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
            if (!skipSemaphore)
                connectionSemaphore.Release();
            throw;
        }
    }
    
    public void ReleaseConnection()
    {
        // Don't release if we're in a transaction
        // The transaction manages the semaphore lifecycle
        if (currentTransaction == null)
        {
            connectionSemaphore.Release();
        }
    }

    public SqliteTransaction? GetCurrentTransaction()
    {
        return currentTransaction;
    }

    public async Task<SqliteTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (currentTransaction != null)
            throw new InvalidOperationException("A transaction is already in progress");

        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        currentTransaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        // Keep the semaphore held for the duration of the transaction
        // It will be released when the transaction completes
        return currentTransaction;
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (currentTransaction == null)
            throw new InvalidOperationException("No transaction is in progress");

        try
        {
            await currentTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await currentTransaction.DisposeAsync().ConfigureAwait(false);
            currentTransaction = null;
            // Release the semaphore now that transaction is complete
            connectionSemaphore.Release();
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (currentTransaction == null)
            throw new InvalidOperationException("No transaction is in progress");

        try
        {
            await currentTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await currentTransaction.DisposeAsync().ConfigureAwait(false);
            currentTransaction = null;
            // Release the semaphore now that transaction is complete
            connectionSemaphore.Release();
        }
    }
    
    public async Task ForceCleanupTransactionAsync()
    {
        if (currentTransaction != null)
        {
            try
            {
                await currentTransaction.RollbackAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, "Failed to rollback transaction during cleanup");
            }
            finally
            {
                try
                {
                    await currentTransaction.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Warning(ex, "Failed to dispose transaction during cleanup");
                }
                currentTransaction = null;
            }
        }
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
                MessageId TEXT NOT NULL UNIQUE,
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
        if (!isDisposed) return;
        throw new ObjectDisposedException(nameof(DatabaseContext));
    }

    public void Dispose()
    {
        if (isDisposed) return;
        
        connectionLock.Wait();
        try
        {
            // Force clean up any hanging transaction
            if (currentTransaction != null)
            {
                try
                {
                    currentTransaction.Rollback();
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Warning(ex, "Failed to rollback transaction during dispose");
                }
                finally
                {
                    currentTransaction.Dispose();
                    currentTransaction = null;
                }
            }
            
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
            // Force clean up any hanging transaction
            await ForceCleanupTransactionAsync().ConfigureAwait(false);
            
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
