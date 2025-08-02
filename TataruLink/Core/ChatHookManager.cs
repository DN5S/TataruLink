
// File: TataruLink/Core/ChatHookManager.cs

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using TataruLink.Config;
using TataruLink.Interfaces.Core;
using TataruLink.Interfaces.Services;
using TataruLink.UI.Windows;

namespace TataruLink.Core;

/// <summary>
/// Manages hooks into Dalamud's chat events to capture and process messages.
/// This class acts as the bridge between the game's chat UI and the plugin's translation pipeline.
/// </summary>
public class ChatHookManager(
    IChatGui chatGui,
    IFramework framework,
    IMessageService messageService,
    DisplayConfig displayConfig,
    TranslationOverlayWindow translationOverlayWindow,
    ILogger<ChatHookManager> logger) // ILogger is correctly injected via DI.
    : IChatHookManager
{
    private bool isDisposed;
    private readonly ConcurrentDictionary<string, DateTime> recentTranslations = new();
    private readonly Lock lockObject = new();

    /// <inheritdoc />
    public bool IsInitialized { get; private set; }

    /// <inheritdoc />
    public void Initialize()
    {
        lock (lockObject)
        {
            if (IsInitialized)
            {
                logger.LogWarning("ChatHookManager is already initialized. Skipping.");
                return;
            }

            try
            {
                logger.LogInformation("Initializing ChatHookManager...");
                messageService.OnTranslationReady += OnTranslationReady;
                chatGui.ChatMessage += OnChatMessage;
                
                IsInitialized = true;
                logger.LogInformation("ChatHookManager initialized and hooks are active.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize ChatHookManager. Hooks will not be active.");
                Cleanup();
                throw;
            }
        }
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        try
        {
            if (isHandled || isDisposed || message.Payloads.Count == 0)
                return;

            var messageText = message.TextValue;
            
            // --- CRITICAL FIX: Hash-based infinite loop prevention ---
            var messageHash = ComputeMessageHash(messageText);
            if (recentTranslations.TryGetValue(messageHash, out var lastSeen))
            {
                if (DateTime.Now - lastSeen < TimeSpan.FromSeconds(2))
                {
                    logger.LogTrace("Skipping recently processed message to prevent infinite loop. Hash: {hash}", messageHash);
                    return; // Skip recently processed messages
                }
            }
            
            // Clean up old entries periodically (keep only last 30 seconds)
            CleanupOldTranslations();
            
            // CRITICAL: Create local copies of ref parameters immediately.
            // Capturing 'ref' parameters in a closure can lead to memory corruption if the original data is freed.
            var senderLocal = CreateOptimizedSeStringCopy(sender);
            var messageLocal = CreateOptimizedSeStringCopy(message);
            
            logger.LogTrace("Captured chat message. Type: {type}, Sender: '{sender}', Hash: {hash}", type, sender.TextValue, messageHash);

            // Enqueue the message for asynchronous processing.
            messageService.EnqueueMessage(type, senderLocal, messageLocal);
        }
        catch (Exception ex)
        {
            // CRITICAL: Never let exceptions bubble up to Dalamud's chat system to prevent game instability.
            logger.LogCritical(ex, "A critical, unhandled error occurred in OnChatMessage for type {type}. The message was dropped.", type);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ComputeMessageHash(string messageText)
    {
        // Use a simple but effective hash for message deduplication
        // We use SHA256 for better collision resistance than GetHashCode()
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(messageText));
        return Convert.ToBase64String(hashBytes)[..12]; // Use the first 12 characters for efficiency
    }

    private void CleanupOldTranslations()
    {
        // Only clean up every 50 messages to avoid performance impact
        if (recentTranslations.Count < 50) return;
        
        var cutoff = DateTime.Now.AddSeconds(-30);
        var keysToRemove = recentTranslations
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in keysToRemove)
        {
            recentTranslations.TryRemove(key, out _);
        }
        
        if (keysToRemove.Count > 0)
        {
            logger.LogTrace("Cleaned up {count} old translation hashes", keysToRemove.Count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SeString CreateOptimizedSeStringCopy(SeString? original)
    {
        if (original?.Payloads == null || original.Payloads.Count == 0)
            return new SeString();

        try
        {
            // The most efficient and reliable copy method.
            return new SeString(original.Payloads.ToArray());
        }
        catch (Exception ex)
        {
            // This fallback is crucial if payload structures change in Dalamud.
            logger.LogWarning(ex, "Failed to copy SeString payloads. Falling back to text-only copy for message: '{text}'", original.TextValue);
            var textValue = original.TextValue;
            return string.IsNullOrEmpty(textValue) 
                       ? new SeString() 
                       : new SeString(new TextPayload(textValue));
        }
    }

    private void OnTranslationReady(SeString formattedMessage)
    {
        if (isDisposed) return;

        try
        {
            logger.LogTrace("Translation ready. Queuing display on framework thread.");
            // All UI updates must be on the framework thread.
            framework.RunOnFrameworkThread(() => DisplayTranslation(formattedMessage));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue translation display on framework thread.");
        }
    }

    private void DisplayTranslation(SeString formattedMessage)
    {
        try
        {
            var mode = displayConfig.DisplayMode;
            var displayed = false;

            if (mode is TranslationDisplayMode.InGameChat or TranslationDisplayMode.Both)
            {
                // --- CRITICAL: Register the translation hash BEFORE displaying ---
                var messageText = formattedMessage.TextValue;
                var messageHash = ComputeMessageHash(messageText);
                recentTranslations[messageHash] = DateTime.Now;
                
                logger.LogTrace("Registering translation hash before display: {hash}", messageHash);
                
                chatGui.Print(formattedMessage);
                displayed = true;
            }

            if (mode is TranslationDisplayMode.SeparateWindow or TranslationDisplayMode.Both)
            {
                translationOverlayWindow.AddLog(formattedMessage);
                displayed = true;
            }

            if(displayed)
                logger.LogDebug("Translation displayed successfully via {mode}.", mode);
            else
                logger.LogWarning("Translation was ready but no display mode was configured to show it.");

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while displaying the final translated message.");
        }
    }

    private void Cleanup()
    {
        lock (lockObject)
        {
            logger.LogInformation("Cleaning up ChatHookManager resources.");
            try
            {
                messageService.OnTranslationReady -= OnTranslationReady;
                chatGui.ChatMessage -= OnChatMessage;
                recentTranslations.Clear();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "An error occurred while unregistering event handlers.");
            }
        }
    }

    public void Dispose()
    {
        lock (lockObject)
        {
            if (isDisposed) return;
            isDisposed = true;
            Cleanup();
            logger.LogInformation("ChatHookManager disposed.");
        }
    }
}
