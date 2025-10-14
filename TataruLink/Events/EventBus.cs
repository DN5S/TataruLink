using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TataruLink.Services;

namespace TataruLink.Events;

public class EventBus : IDisposable
{
    private readonly ConcurrentDictionary<Type, List<object>> handlers = new();
    private bool isDisposed;

    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : class
    {
        var eventType = typeof(TEvent);
        var handlerList = handlers.GetOrAdd(eventType, _ => new List<object>());

        lock (handlerList)
        {
            if (!handlerList.Contains(handler))
            {
                handlerList.Add(handler);
                Service.PluginLog.Debug($"Handler subscribed to {eventType.Name}");
            }
        }
    }

    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : class
    {
        var eventType = typeof(TEvent);
        if (handlers.TryGetValue(eventType, out var handlerList))
        {
            lock (handlerList)
            {
                handlerList.Remove(handler);
                Service.PluginLog.Debug($"Handler unsubscribed from {eventType.Name}");
            }
        }
    }

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : class
    {
        if (isDisposed)
        {
            Service.PluginLog.Warning($"Attempted to publish {typeof(TEvent).Name} on disposed EventBus");
            return;
        }

        var eventType = typeof(TEvent);
        if (!handlers.TryGetValue(eventType, out var handlerList))
        {
            Service.PluginLog.Debug($"No handlers registered for {eventType.Name}");
            return;
        }

        List<object> handlersCopy;
        lock (handlerList)
        {
            handlersCopy = new List<object>(handlerList);
        }

        Service.PluginLog.Debug($"Publishing {eventType.Name} to {handlersCopy.Count} handler(s)");

        var tasks = handlersCopy
            .OfType<IEventHandler<TEvent>>()
            .Select(handler => SafeHandleAsync(handler, @event));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task SafeHandleAsync<TEvent>(IEventHandler<TEvent> handler, TEvent @event) where TEvent : class
    {
        try
        {
            await handler.HandleAsync(@event).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Error in event handler for {typeof(TEvent).Name}");
        }
    }

    public void Clear()
    {
        handlers.Clear();
        Service.PluginLog.Information("EventBus cleared");
    }

    public void Dispose()
    {
        if (isDisposed) return;

        Clear();
        isDisposed = true;
        Service.PluginLog.Information("EventBus disposed");
        GC.SuppressFinalize(this);
    }
}
