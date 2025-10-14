using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Utility;
using TataruLink.Configuration;

namespace TataruLink.Services;

public class Config : IDisposable
{
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private CancellationTokenSource? saveDebounceTokenSource;
    private bool isDirty;
    private readonly string configFilePath;
    
    private const int SaveDebounceDelayMs = 500;
    private const string ConfigFileName = "tatarulink.json";
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        IncludeFields = false
    };
    
    public TataruConfig Data { get; private set; }
    
    private Config(TataruConfig data, string configPath)
    {
        Data = data;
        configFilePath = configPath;
    }
    
    public static Config Load(IDalamudPluginInterface pluginInterface)
    {
        var configDir = pluginInterface.GetPluginConfigDirectory();
        var configPath = Path.Combine(configDir, ConfigFileName);

        TataruConfig data;

        try
        {
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                data = JsonSerializer.Deserialize<TataruConfig>(json, JsonOptions) ?? new TataruConfig();
                Service.PluginLog.Information($"Configuration loaded from {configPath} (Version {data.Version})");

                // Ensure all overlay windows have default colors initialized
                foreach (var overlay in data.Display.OverlayWindows)
                {
                    overlay.EnsureDefaultColors();
                }
            }
            else
            {
                data = new TataruConfig();
                Service.PluginLog.Information("No existing configuration found, creating new defaults");
            }
        }
        catch (JsonException ex)
        {
            Service.PluginLog.Error(ex, "Failed to deserialize configuration file, using defaults");
            data = new TataruConfig();
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load configuration file, using defaults");
            data = new TataruConfig();
        }

        return new Config(data, configPath);
    }
    
    public void Save()
    {
        saveLock.Wait();
        try
        {
            isDirty = true;

            // Cancel any pending save operation
            saveDebounceTokenSource?.Cancel();
            saveDebounceTokenSource?.Dispose();

            // Schedule a new debounced save after the delay
            // This prevents excessive disk writes during rapid config changes (e.g., color picker dragging)
            saveDebounceTokenSource = new CancellationTokenSource();
            var token = saveDebounceTokenSource.Token;

            Task.Delay(SaveDebounceDelayMs, token).ContinueWith(_ =>
            {
                if (!token.IsCancellationRequested)
                {
                    SaveImmediately();
                }
            }, TaskScheduler.Default);
        }
        finally
        {
            saveLock.Release();
        }
    }
    
    // NOTE: Call this when the plugin shuts down to flush pending saves
    public void SaveImmediately()
    {
        saveLock.Wait();
        try
        {
            if (!isDirty) return;

            try
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(configFilePath);
                if (string.IsNullOrEmpty(dir))
                {
                    Service.PluginLog.Error("Configuration directory path is invalid");
                    return;
                }

                Directory.CreateDirectory(dir);

                // Save using atomic write
                var json = JsonSerializer.Serialize(Data, JsonOptions);
                FilesystemUtil.WriteAllTextSafe(configFilePath, json);

                isDirty = false;
                Service.PluginLog.Debug($"Configuration saved to {configFilePath}");
            }
            catch (JsonException ex)
            {
                Service.PluginLog.Error(ex, "Failed to serialize configuration");
            }
            catch (IOException ex)
            {
                Service.PluginLog.Error(ex, $"Failed to write configuration file to {configFilePath}");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Unexpected error while saving configuration");
            }

            saveDebounceTokenSource?.Cancel();
            saveDebounceTokenSource?.Dispose();
            saveDebounceTokenSource = null;
        }
        finally
        {
            saveLock.Release();
        }
    }
    
    public void Reset()
    {
        Service.PluginLog.Information("Resetting configuration to defaults (preserving API keys)");

        // NOTE: Preserve API keys when resetting config
        var apiKeys = Data.Translation.ApiKeys;

        Data = new TataruConfig
        {
            Translation =
            {
                ApiKeys = apiKeys
            }
        };

        Save();
    }
    
    public void Dispose()
    {
        // Flush any pending saves
        SaveImmediately();

        // Cancel and cleanup debounce timer
        saveDebounceTokenSource?.Cancel();
        saveDebounceTokenSource?.Dispose();

        saveLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
