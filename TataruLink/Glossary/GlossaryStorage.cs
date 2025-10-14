using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using TataruLink.Models;

namespace TataruLink.Glossary;

public class GlossaryStorage : IDisposable
{
    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);
    private const int MaxEntries = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GlossaryStorage(IDalamudPluginInterface pluginInterface)
    {
        var configDir = pluginInterface.GetPluginConfigDirectory();
        filePath = Path.Combine(configDir, "glossary.json");

        Service.PluginLog.Information($"Glossary storage initialized at: {filePath}");
    }

    public async Task<List<GlossaryEntry>> LoadAsync()
    {
        await fileLock.WaitAsync();
        try
        {
            if (!File.Exists(filePath))
            {
                Service.PluginLog.Information("Glossary file not found, starting with empty glossary");
                return [];
            }

            var json = await File.ReadAllTextAsync(filePath);
            var wrapper = JsonSerializer.Deserialize<GlossaryWrapper>(json, JsonOptions);

            if (wrapper?.Entries == null)
            {
                Service.PluginLog.Warning("Invalid glossary file format, starting fresh");
                return [];
            }

            Service.PluginLog.Information($"Loaded {wrapper.Entries.Count} glossary entries from file");
            return wrapper.Entries;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load glossary from file");
            return [];
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<bool> SaveAsync(List<GlossaryEntry> entries)
    {
        await fileLock.WaitAsync();
        try
        {
            // Enforce max entries limit
            if (entries.Count > MaxEntries)
            {
                Service.PluginLog.Warning($"Glossary has {entries.Count} entries, truncating to {MaxEntries}");
                entries = entries.Take(MaxEntries).ToList();
            }

            var wrapper = new GlossaryWrapper
            {
                Version = 1,
                LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Entries = entries
            };

            var json = JsonSerializer.Serialize(wrapper, JsonOptions);

            // Write to temporary file first, then replace atomically
            var tempPath = filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);

            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, null);
            }
            else
            {
                File.Move(tempPath, filePath);
            }

            Service.PluginLog.Debug($"Saved {entries.Count} glossary entries to file");
            return true;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to save glossary to file");
            return false;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public bool CanAddEntry(int currentCount)
    {
        return currentCount < MaxEntries;
    }

    public int GetMaxEntries() => MaxEntries;

    public int GetRemainingCapacity(int currentCount)
    {
        return Math.Max(0, MaxEntries - currentCount);
    }

    public void Dispose()
    {
        fileLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private class GlossaryWrapper
    {
        public int Version { get; set; }
        public long LastModified { get; set; }
        public List<GlossaryEntry> Entries { get; set; } = [];
    }
}
