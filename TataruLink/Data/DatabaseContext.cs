using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Microsoft.EntityFrameworkCore;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Data;

public class DatabaseContext : DbContext
{
    private readonly string dbPath;
    private readonly CacheConfig config;

    public DatabaseContext(IDalamudPluginInterface pluginInterface, CacheConfig config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        
        var configDir = pluginInterface.GetPluginConfigDirectory();
        dbPath = Path.Combine(configDir, config.DatabaseFileName);
    }

    public DbSet<TranslationCacheEntry> TranslationCache { get; set; } = null!;
    public DbSet<ChatHistoryEntry> ChatHistory { get; set; } = null!;
    public DbSet<GlossaryEntry> GlossaryEntries { get; set; } = null!;
    public DbSet<BlocklistEntry> BlocklistKeywords { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={dbPath};Cache=Shared;Mode=ReadWriteCreate;");
        
        if (config.EnableWal)
        {
            optionsBuilder.UseSqlite(connectionBuilder =>
            {
                connectionBuilder.CommandTimeout(config.ConnectionTimeoutSeconds);
            });
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TranslationCache configuration
        modelBuilder.Entity<TranslationCacheEntry>(entity =>
        {
            entity.ToTable("TranslationCache");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CacheKey).IsUnique();
            entity.HasIndex(e => e.LastAccessedAt);
            entity.HasIndex(e => new { e.SourceLanguage, e.TargetLanguage });
            entity.HasIndex(e => e.Provider);
            entity.HasIndex(e => new { e.Provider, e.SourceLanguage, e.TargetLanguage });
        });

        // ChatHistory configuration
        modelBuilder.Entity<ChatHistoryEntry>(entity =>
        {
            entity.ToTable("ChatHistory");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.IsVisible, e.Timestamp });
            entity.HasIndex(e => new { e.ChatType, e.IsVisible, e.Timestamp });
            
            entity.HasOne<TranslationCacheEntry>()
                .WithMany()
                .HasForeignKey(e => e.TranslationCacheId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // GlossaryEntries configuration
        modelBuilder.Entity<GlossaryEntry>(entity =>
        {
            entity.ToTable("GlossaryEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.Original).IsUnique();
            entity.HasIndex(e => e.IsEnabled);
        });

        // BlocklistKeywords configuration
        modelBuilder.Entity<BlocklistEntry>(entity =>
        {
            entity.ToTable("BlocklistKeywords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.Keyword).IsUnique();
            entity.HasIndex(e => e.IsEnabled);
        });
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Database.EnsureCreatedAsync(cancellationToken);
        
        if (config.EnableWal)
        {
            // SQLite-specific: Enable Write-Ahead Logging for better concurrency
            await Database.ExecuteSqlAsync(
                $"PRAGMA journal_mode=WAL; PRAGMA busy_timeout={config.ConnectionTimeoutSeconds * 1000};", 
                cancellationToken);
        }
    }

    public async Task VacuumAsync(CancellationToken cancellationToken = default)
    {
        // SQLite-specific: Rebuild a database file to reclaim space
        await Database.ExecuteSqlRawAsync("VACUUM;", cancellationToken);
        Service.PluginLog.Information("Database vacuum completed");
    }

    public Task<long> GetDatabaseSizeAsync()
    {
        var fileInfo = new FileInfo(dbPath); // Alternative: Use the file system to get database size without SQL
        return Task.FromResult(fileInfo.Exists ? fileInfo.Length : 0L);
    }
}
