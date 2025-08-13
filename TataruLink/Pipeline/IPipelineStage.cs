using System.Collections.Generic;
using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Pipeline;

public interface IPipelineStage
{
    string Name { get; }
    
    bool IsEnabled { get; set; }
    
    // Returns null to stop pipeline execution
    Task<Message?> ProcessAsync(Message message, PipelineContext context);
    
    void Initialize();
    
    void Dispose();
}

public class PipelineContext
{
    private readonly Dictionary<string, object> data = new();
    
    public void Set<T>(string key, T value) where T : notnull
    {
        data[key] = value;
    }
    
    public T? Get<T>(string key) where T : class
    {
        return data.TryGetValue(key, out var value) ? value as T : null;
    }
    
    public bool Has(string key) => data.ContainsKey(key);
    
    public void Clear() => data.Clear();
}
