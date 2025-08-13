using System;
using System.Threading;
using System.Threading.Tasks;
using TataruLink.Services;

namespace TataruLink.Translation;

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

public class CircuitBreaker
    (int failureThreshold = 5, int openTimeoutSeconds = 30)
{
    private readonly TimeSpan openTimeout = TimeSpan.FromSeconds(openTimeoutSeconds);
    private readonly Lock stateLock = new();
    
    private CircuitState state = CircuitState.Closed;
    private int failureCount;
    private DateTime lastFailureTime;
    private DateTime openedAt;
    
    public CircuitState State
    {
        get
        {
            lock (stateLock)
            {
                if (state == CircuitState.Open)
                {
                    var elapsed = DateTime.UtcNow - openedAt;
                    if (elapsed >= openTimeout)
                    {
                        state = CircuitState.HalfOpen;
                        Service.PluginLog.Debug("Circuit breaker transitioned to HalfOpen");
                    }
                }
                return state;
            }
        }
    }

    public async Task<(bool Success, T? Result)> TryExecuteAsync<T>(Func<Task<T?>> operation, string operationName) where T : class
    {
        if (State == CircuitState.Open)
        {
            Service.PluginLog.Debug($"Circuit breaker is OPEN for {operationName}, rejecting request");
            return (false, null);
        }
        
        try
        {
            var result = await operation();
            OnSuccess();
            return (result != null, result);
        }
        catch (Exception ex)
        {
            OnFailure();
            Service.PluginLog.Debug(ex, $"Circuit breaker recorded failure for {operationName}");
            return (false, null);
        }
    }
    
    private void OnSuccess()
    {
        lock (stateLock)
        {
            switch (state)
            {
                case CircuitState.HalfOpen:
                    state = CircuitState.Closed;
                    failureCount = 0;
                    Service.PluginLog.Information("Circuit breaker closed after successful operation");
                    break;
                case CircuitState.Closed:
                {
                    // Reset failure count on success in a closed state
                    var timeSinceLastFailure = DateTime.UtcNow - lastFailureTime;
                    if (timeSinceLastFailure > TimeSpan.FromMinutes(1))
                    {
                        failureCount = 0;
                    }

                    break;
                }
                case CircuitState.Open:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    
    private void OnFailure()
    {
        lock (stateLock)
        {
            lastFailureTime = DateTime.UtcNow;
            failureCount++;
            
            switch (state)
            {
                case CircuitState.HalfOpen:
                    // Immediately open on failure in a half-open state
                    state = CircuitState.Open;
                    openedAt = DateTime.UtcNow;
                    Service.PluginLog.Warning($"Circuit breaker OPENED after failure in half-open state");
                    break;
                case CircuitState.Closed when failureCount >= failureThreshold:
                    state = CircuitState.Open;
                    openedAt = DateTime.UtcNow;
                    Service.PluginLog.Warning($"Circuit breaker OPENED after {failureCount} failures");
                    break;
                case CircuitState.Open:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    
    public void Reset()
    {
        lock (stateLock)
        {
            state = CircuitState.Closed;
            failureCount = 0;
            Service.PluginLog.Information("Circuit breaker manually reset");
        }
    }
}
