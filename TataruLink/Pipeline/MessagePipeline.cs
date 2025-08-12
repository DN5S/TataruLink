using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Models;
using TataruLink.Services;

namespace TataruLink.Pipeline;

/// <summary>
/// Orchestrates the message processing pipeline.
/// Messages flow through each stage in sequence.
/// </summary>
public class MessagePipeline : IDisposable
{
    private readonly List<IPipelineStage> stages = [];
    private readonly SemaphoreSlim @lock = new(1, 1);
    private bool isInitialized;

    /// <summary>
    /// Add a stage to the pipeline.
    /// </summary>
    public MessagePipeline AddStage(IPipelineStage stage)
    {
        @lock.Wait();
        try
        {
            stages.Add(stage);
            if (isInitialized)
            {
                stage.Initialize();
            }
            
            Service.PluginLog.Information($"Added pipeline stage: {stage.Name}");
        }
        finally
        {
            @lock.Release();
        }
        return this;
    }

    /// <summary>
    /// Remove a stage from the pipeline.
    /// </summary>
    public MessagePipeline RemoveStage(IPipelineStage stage)
    {
        @lock.Wait();
        try
        {
            if (stages.Remove(stage))
            {
                stage.Dispose();
                Service.PluginLog.Information($"Removed pipeline stage: {stage.Name}");
            }
        }
        finally
        {
            @lock.Release();
        }
        return this;
    }

    /// <summary>
    /// Initialize all pipeline stages.
    /// </summary>
    public void Initialize()
    {
        @lock.Wait();
        try
        {
            if (isInitialized) return;
            
            foreach (var stage in stages)
            {
                try
                {
                    stage.Initialize();
                    Service.PluginLog.Debug($"Initialized stage: {stage.Name}");
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, $"Failed to initialize stage: {stage.Name}");
                }
            }
            
            isInitialized = true;
            Service.PluginLog.Information($"Pipeline initialized with {stages.Count} stages");
        }
        finally
        {
            @lock.Release();
        }
    }

    /// <summary>
    /// Process a message through the entire pipeline.
    /// </summary>
    public async Task<Message?> ProcessAsync(Message message)
    {
        if (!isInitialized)
        {
            Service.PluginLog.Warning("Pipeline not initialized, skipping message");
            return null;
        }

        var context = new PipelineContext();
        var currentMessage = message;
        var stopwatch = Stopwatch.StartNew();

        Service.PluginLog.Debug($"Pipeline processing started for message ID: {message.Id}");

        foreach (var stage in stages)
        {
            if (!stage.IsEnabled)
            {
                Service.PluginLog.Debug($"Skipping disabled stage: {stage.Name}");
                continue;
            }

            try
            {
                var stageStart = stopwatch.ElapsedMilliseconds;
                
                // Check if the message is null before processing (from the previous stage)
                if (currentMessage == null)
                {
                    Service.PluginLog.Debug($"Pipeline already stopped, skipping stage: {stage.Name}");
                    break;
                }

                currentMessage = await stage.ProcessAsync(currentMessage, context);
                var stageTime = stopwatch.ElapsedMilliseconds - stageStart;
                
                Service.PluginLog.Debug($"Stage '{stage.Name}' completed in {stageTime}ms");

                if (currentMessage == null)
                {
                    Service.PluginLog.Debug($"Pipeline stopped at stage: {stage.Name}");
                    break;
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, $"Error in pipeline stage: {stage.Name}");
                // Continue to the next stage on error (resilience)
            }
        }

        stopwatch.Stop();
        Service.PluginLog.Debug($"Pipeline completed in {stopwatch.ElapsedMilliseconds}ms");
        
        return currentMessage;
    }

    /// <summary>
    /// Get information about the current pipeline configuration.
    /// </summary>
    public string GetPipelineInfo()
    {
        var info = new List<string> { "Pipeline Stages:" };
        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            var status = stage.IsEnabled ? "✓" : "✗";
            info.Add($"  {i + 1}. [{status}] {stage.Name}");
        }
        return string.Join("\n", info);
    }

    /// <summary>
    /// Clear all stages from the pipeline.
    /// </summary>
    public void Clear()
    {
        @lock.Wait();
        try
        {
            foreach (var stage in stages)
            {
                stage.Dispose();
            }
            stages.Clear();
            Service.PluginLog.Information("Pipeline cleared");
        }
        finally
        {
            @lock.Release();
        }
    }

    public void Dispose()
    {
        Clear();
        isInitialized = false;
        @lock.Dispose();
    }
}
