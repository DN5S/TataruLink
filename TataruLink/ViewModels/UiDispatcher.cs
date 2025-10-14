using System;
using System.Collections.Generic;
using System.Threading;

namespace TataruLink.ViewModels;

/// <summary>
/// Dispatches actions to the UI thread for thread-safe property updates.
/// Processes queued actions during ImGui Draw() calls.
/// </summary>
public class UiDispatcher
{
    private readonly Queue<Action> actionQueue = new();
    private readonly Lock syncRoot = new();

    /// <summary>
    /// Enqueues an action to be executed on the UI thread.
    /// Safe to call from any thread.
    /// </summary>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (syncRoot)
        {
            actionQueue.Enqueue(action);
        }
    }

    /// <summary>
    /// Processes all queued actions. Should be called from the UI thread
    /// (typically in Draw() methods or before rendering).
    /// </summary>
    public void ProcessQueue()
    {
        Action[] actionsToRun;

        lock (syncRoot)
        {
            if (actionQueue.Count == 0)
                return;

            actionsToRun = actionQueue.ToArray();
            actionQueue.Clear();
        }

        foreach (var action in actionsToRun)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                // Log but don't crash the UI thread
                Services.Service.PluginLog.Error(ex, "Error processing UI dispatcher action");
            }
        }
    }

    /// <summary>
    /// Gets the count of pending actions. Useful for debugging.
    /// </summary>
    public int PendingCount
    {
        get
        {
            lock (syncRoot)
            {
                return actionQueue.Count;
            }
        }
    }
}
