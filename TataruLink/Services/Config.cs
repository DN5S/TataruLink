using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using TataruLink.Configuration;

namespace TataruLink.Services;

public class Config : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private CancellationTokenSource? saveDebounceTokenSource;
    private bool isDirty;
    
    private const int SaveDebounceDelayMs = 500;
    
    public TataruConfig Data { get; private set; }
    
    private Config(IDalamudPluginInterface pluginInterface, TataruConfig data)
    {
        this.pluginInterface = pluginInterface;
        this.Data = data;
    }
    
    public static Config Load(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            var data = pluginInterface.GetPluginConfig() as TataruConfig ?? new TataruConfig();
            Service.PluginLog.Information($"Configuration loaded (Version {data.Version})");
            
            return new Config(pluginInterface, data);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to load configuration, using defaults");
            return new Config(pluginInterface, new TataruConfig());
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
    
    // NOTE: Call this when plugin shuts down to flush pending saves
    public void SaveImmediately()
    {
        saveLock.Wait();
        try
        {
            if (!isDirty) return;
            
            try
            {
                pluginInterface.SavePluginConfig(Data);
                isDirty = false;
                Service.PluginLog.Debug("Configuration saved to disk");
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
            Translation = new TranslationConfig { ApiKeys = apiKeys }
        };
        
        Save();
        Service.PluginLog.Information("Configuration reset to defaults");
    }
    
    public void Reload()
    {
        try
        {
            if (pluginInterface.GetPluginConfig() is TataruConfig newData)
            {
                Data = newData;
                isDirty = false;
                Service.PluginLog.Information("Configuration reloaded from disk");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to reload configuration");
        }
    }
    
    public void Dispose()
    {
        SaveImmediately();
        
        saveDebounceTokenSource?.Cancel();
        saveDebounceTokenSource?.Dispose();
        saveLock.Dispose();
    }
}
