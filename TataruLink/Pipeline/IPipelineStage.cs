using System.Collections.Generic;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Pipeline;

/// <summary>
/// Represents a stage in the message processing pipeline.
/// Each stage can process, transform, or filter messages.
/// </summary>
public interface IPipelineStage
{
    /// <summary>
    /// Name of this pipeline stage for logging and debugging.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Whether this stage is currently enabled.
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// Process a message through this pipeline stage.
    /// </summary>
    /// <param name="message">The message to process</param>
    /// <param name="context">Pipeline context for sharing data between stages</param>
    /// <returns>The processed message, or null to stop pipeline execution</returns>
    Task<Message?> ProcessAsync(Message message, PipelineContext context);
    
    /// <summary>
    /// Initialize the pipeline stage.
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Clean up resources used by this stage.
    /// </summary>
    void Dispose();
}

/// <summary>
/// Context object for sharing data between pipeline stages.
/// </summary>
public class PipelineContext
{
    private readonly Dictionary<string, object> data = new();
    
    /// <summary>
    /// Store data in the context.
    /// </summary>
    public void Set<T>(string key, T value) where T : notnull
    {
        data[key] = value;
    }
    
    /// <summary>
    /// Retrieve data from the context.
    /// </summary>
    public T? Get<T>(string key) where T : class
    {
        return data.TryGetValue(key, out var value) ? value as T : null;
    }
    
    /// <summary>
    /// Check if a key exists in the context.
    /// </summary>
    public bool Has(string key) => data.ContainsKey(key);
    
    /// <summary>
    /// Clear all context data.
    /// </summary>
    public void Clear() => data.Clear();
}
