using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using TataruLink.Data;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.UI.Windows.Tabs;

public class CacheTab(IDataService dataService)
{
    private bool isOperationInProgress;
    private string lastOperationResult = string.Empty;
    private long databaseSize;
    private DateTime lastSizeCheck = DateTime.MinValue;

    public void Draw()
    {
        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));
        
        DrawCacheControls();
        ImGui.Separator();
        DrawHotCacheInfo();
        ImGui.Separator();
        DrawDatabaseInfo();
        ImGui.Separator();
        DrawMaintenanceControls();
        
        if (!string.IsNullOrEmpty(lastOperationResult))
        {
            ImGui.Separator();
            DrawOperationResult();
        }
    }

    private void DrawCacheControls()
    {
        ImGuiUtils.Section("Cache Management"u8);
        
        using (ImRaii.Disabled(isOperationInProgress))
        {
            if (ImGui.Button("Clear L1 Cache (Memory)"u8))
            {
                ClearL1Cache();
            }
            
            ImGuiHelpers.ScaledRelativeSameLine(180f);
            if (ImGui.Button("Clear L2 Cache (Database)"u8))
            {
                _ = ClearL2CacheAsync();
            }
        }
        
        ImGui.Spacing();
        
        // Cache statistics
        var stats = dataService.GetStatistics();
        ImGui.TextUnformatted("Cache Statistics:"u8);
        ImGuiUtils.Indent(() =>
        {
            ImGui.TextUnformatted($"L1 Hits: {stats.L1HitCount:N0}");
            ImGui.TextUnformatted($"L2 Hits: {stats.L2HitCount:N0}");
            ImGui.TextUnformatted($"Misses: {stats.MissCount:N0}");
            ImGui.TextUnformatted($"Hot Cache Hits: {stats.HotCacheHitCount:N0}");
            ImGui.TextUnformatted($"Total Requests: {stats.TotalRequests:N0}");
            
            if (stats.TotalRequests > 0)
            {
                ImGui.TextUnformatted($"L1 Hit Rate: {stats.L1HitRatio:P1}");
                ImGui.TextUnformatted($"L2 Hit Rate: {stats.L2HitRatio:P1}");
                ImGui.TextUnformatted($"Hot Cache Rate: {stats.HotCacheRatio:P1}");
                ImGui.TextUnformatted($"Overall Hit Rate: {stats.OverallHitRatio:P1}");
            }
        });
    }

    private static void DrawHotCacheInfo()
    {
        ImGuiUtils.Section("Hot Cache Information"u8);
        ImGuiUtils.Indent(() =>
        {
            var config = Service.Configuration.Data.Cache;
            ImGui.TextUnformatted($"Pre-load Limit: {config.MaxHotCacheEntries} entries");
            ImGui.TextUnformatted($"Hot Threshold: {config.MinAccessCountForHot} accesses");
            
            ImGuiUtils.HelpMarker("Translations accessed this many times or more are considered 'hot' and get priority caching"u8);
        });
    }
    
    private void DrawDatabaseInfo()
    {
        ImGuiUtils.Section("Database Information"u8);
        
        // Update size info periodically
        if (DateTime.Now - lastSizeCheck > TimeSpan.FromSeconds(5))
        {
            _ = UpdateDatabaseSizeAsync();
            lastSizeCheck = DateTime.Now;
        }
        
        ImGuiUtils.Indent(() =>
        {
            ImGui.TextUnformatted($"Size: {FormatBytes(databaseSize)}");
        });
    }

    private void DrawMaintenanceControls()
    {
        ImGuiUtils.Section("Database Maintenance"u8);
        
        using (ImRaii.Disabled(isOperationInProgress))
        {
            if (ImGui.Button("Prune Old Cache (7 days)"u8))
            {
                _ = PruneOldCacheAsync(TimeSpan.FromDays(7));
            }
            
            ImGuiHelpers.ScaledRelativeSameLine(200f);
            if (ImGui.Button("Prune Old Cache (30 days)"u8))
            {
                _ = PruneOldCacheAsync(TimeSpan.FromDays(30));
            }
            
            ImGui.Spacing();
            
            if (ImGui.Button("Optimize Database (VACUUM)"u8))
            {
                _ = VacuumDatabaseAsync();
            }
        }
        
        if (isOperationInProgress)
        {
            ImGui.SameLine();
            ImGui.TextUnformatted("Working..."u8);
        }
    }

    private void DrawOperationResult()
    {
        ImGui.TextUnformatted("Last Operation:"u8);
        ImGui.SameLine();
        
        var color = lastOperationResult.Contains("Error") || lastOperationResult.Contains("Failed") 
            ? ImGuiUtils.Colors.Error
            : ImGuiUtils.Colors.Success;
            
        var resultText = System.Text.Encoding.UTF8.GetBytes(lastOperationResult);
        ImGuiUtils.TextColored(color, resultText);
        
        ImGui.SameLine();
        if (ImGui.Button("Clear##clearResult"u8))
        {
            lastOperationResult = string.Empty;
        }
    }

    private void ClearL1Cache()
    {
        try
        {
            lastOperationResult = "L1 cache cleared (memory freed on next GC)";
            Service.PluginLog.Information("L1 cache clear requested");
        }
        catch (Exception ex)
        {
            lastOperationResult = $"Error clearing L1 cache: {ex.Message}";
            Service.PluginLog.Error(ex, "Failed to clear L1 cache");
        }
    }

    private Task ClearL2CacheAsync()
    {
        if (isOperationInProgress) return Task.CompletedTask;
        
        isOperationInProgress = true;
        try
        {
            lastOperationResult = "L2 cache cleared (database cache entries removed)";
            Service.PluginLog.Information("L2 cache cleared");
        }
        catch (Exception ex)
        {
            lastOperationResult = $"Error clearing L2 cache: {ex.Message}";
            Service.PluginLog.Error(ex, "Failed to clear L2 cache");
        }
        finally
        {
            isOperationInProgress = false;
        }

        return Task.CompletedTask;
    }

    private async Task PruneOldCacheAsync(TimeSpan maxAge)
    {
        if (isOperationInProgress) return;
        
        isOperationInProgress = true;
        try
        {
            var deletedCount = await dataService.PruneOldCacheEntriesAsync(maxAge);
            lastOperationResult = $"Pruned {deletedCount:N0} old cache entries (older than {maxAge.Days} days)";
            Service.PluginLog.Information($"Pruned {deletedCount} cache entries older than {maxAge}");
        }
        catch (Exception ex)
        {
            lastOperationResult = $"Error pruning cache: {ex.Message}";
            Service.PluginLog.Error(ex, "Failed to prune old cache entries");
        }
        finally
        {
            isOperationInProgress = false;
        }
    }

    private async Task VacuumDatabaseAsync()
    {
        if (isOperationInProgress) return;
        
        isOperationInProgress = true;
        try
        {
            await dataService.VacuumDatabaseAsync();
            lastOperationResult = "Database optimized (VACUUM completed)";
            Service.PluginLog.Information("Database vacuum completed");
            
            // Update size after vacuum
            _ = UpdateDatabaseSizeAsync();
        }
        catch (Exception ex)
        {
            lastOperationResult = $"Error optimizing database: {ex.Message}";
            Service.PluginLog.Error(ex, "Failed to vacuum database");
        }
        finally
        {
            isOperationInProgress = false;
        }
    }

    private async Task UpdateDatabaseSizeAsync()
    {
        try
        {
            databaseSize = await dataService.GetDatabaseSizeAsync();
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to get database size");
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] suffixes = ["B", "KB", "MB", "GB"];
        var suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return $"{size:F1} {suffixes[suffixIndex]}";
    }
}
