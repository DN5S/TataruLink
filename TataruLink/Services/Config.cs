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
        
        try
        {
            TataruConfig data;
            
            // Try to load from a custom location first
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                data = JsonSerializer.Deserialize<TataruConfig>(json, JsonOptions) ?? new TataruConfig();
                Service.PluginLog.Information($"Configuration loaded from {configPath} (Version {data.Version})");
            }
            else
            {
                data = new TataruConfig();
                Service.PluginLog.Information("Creating new configuration");
            }
            
            return new Config(data, configPath);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load configuration, using defaults");
            return new Config(new TataruConfig(), configPath);
        }
    }
    
    public void Save()
    {
        saveLock.Wait();
        try
        {
            isDirty = true;
            
            saveDebounceTokenSource?.Cancel();
            saveDebounceTokenSource?.Dispose();
            
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
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                
                // Save to a custom location using atomic write
                var json = JsonSerializer.Serialize(Data, JsonOptions);
                FilesystemUtil.WriteAllTextSafe(configFilePath, json);
                
                isDirty = false;
                Service.PluginLog.Debug($"Configuration saved to {configFilePath}");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Failed to save configuration");
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
        Service.PluginLog.Information("Configuration reset to defaults");
    }
    
    public void Dispose()
    {
        SaveImmediately();
        
        saveDebounceTokenSource?.Cancel();
        saveDebounceTokenSource?.Dispose();
        saveLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
