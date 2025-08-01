
// File: TataruLink/Core/ChatHookManager.cs

using System;
using System.Runtime.CompilerServices;
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
/// Enhanced with comprehensive error handling, performance optimizations, and robust logging.
/// IMPROVED: Memory safety, performance optimizations, and better error recovery.
/// </summary>
public class ChatHookManager(
    IChatGui chatGui,
    IFramework framework,
    IMessageService messageService,
    DisplayConfig displayConfig,
    TranslationOverlayWindow translationOverlayWindow,
    ILogger<ChatHookManager>? logger = null)
    : IChatHookManager
{
    private readonly IChatGui chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
    private readonly IFramework framework = framework ?? throw new ArgumentNullException(nameof(framework));
    private readonly IMessageService messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
    private readonly DisplayConfig displayConfig = displayConfig ?? throw new ArgumentNullException(nameof(displayConfig));
    private readonly TranslationOverlayWindow translationOverlayWindow = translationOverlayWindow ?? throw new ArgumentNullException(nameof(translationOverlayWindow));

    // PERFORMANCE: Track initialization state to avoid redundant operations
    private bool isInitialized;
    private bool isDisposed;
    
    // THREAD SAFETY: Use the object for synchronization in critical sections
    private readonly Lock lockObject = new();

    /// <inheritdoc />
    public bool IsInitialized => isInitialized;

    /// <inheritdoc />
    public void Initialize()
    {
        lock (lockObject)
        {
            if (isInitialized)
            {
                logger?.LogWarning("ChatHookManager is already initialized. Skipping duplicate initialization.");
                return;
            }

            try
            {
                // SAFETY: Register event handlers with error boundaries
                messageService.OnTranslationReady += OnTranslationReady;
                chatGui.ChatMessage += OnChatMessage;
                
                isInitialized = true;
                logger?.LogInformation("ChatHookManager initialized successfully.");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to initialize ChatHookManager");
                // CLEANUP: Ensure partial initialization doesn't leave dangling handlers
                Cleanup();
                throw;
            }
        }
    }

    /// <summary>
    /// Handles incoming chat messages from the game with comprehensive error handling.
    /// PERFORMANCE OPTIMIZED: Reduced framework thread calls and improved memory efficiency.
    /// </summary>
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        try
        {
            // PERFORMANCE: Early validation to avoid unnecessary processing
            if (isHandled || isDisposed)
                return;

            // SAFETY: Validate message content before processing
            if (message.Payloads.Count == 0)
            {
                logger?.LogDebug("Skipping empty or null message for chat type: {ChatType}", type);
                return;
            }

            // CRITICAL: Create local copies of ref parameters before passing them to async operations.
            // Capturing 'ref' parameters directly in a closure can lead to memory corruption
            // if the original data is freed before the framework thread executes the lambda.
            var senderLocal = CreateOptimizedSeStringCopy(sender);
            var messageLocal = CreateOptimizedSeStringCopy(message);

            // PERFORMANCE IMPROVEMENT: Avoid double framework thread queueing
            // Process directly since MessageService.EnqueueMessage is already async-safe
            try
            {
                messageService.EnqueueMessage(type, senderLocal, messageLocal);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error processing chat message of type {ChatType}", type);
                // Don't re-throw here to prevent game instability
            }
        }
        catch (Exception ex)
        {
            // CRITICAL: Never let exceptions bubble up to Dalamud's chat system
            // This could cause game instability or crashes
            logger?.LogError(ex, "Critical error in OnChatMessage for type {ChatType}", type);
        }
    }

    /// <summary>
    /// Creates an optimized copy of a SeString for cross-thread usage.
    /// PERFORMANCE OPTIMIZED: Direct array conversion for maximum efficiency.
    /// COMPATIBILITY: Resolves constructor ambiguity explicitly.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SeString CreateOptimizedSeStringCopy(SeString? original)
    {
        if (original?.Payloads == null || original.Payloads.Count == 0)
            return new SeString();

        try
        {
            // OPTIMAL: Direct array conversion - most efficient and unambiguous
            return new SeString(original.Payloads.ToArray());
        }
        catch (Exception)
        {
            try
            {
                // FALLBACK: Text-only SeString if payload copying fails
                var textValue = original.TextValue;
                return string.IsNullOrEmpty(textValue) 
                           ? new SeString() 
                           : new SeString(new TextPayload(textValue));
            }
            catch (Exception)
            {
                // ULTIMATE FALLBACK: Empty SeString
                return new SeString();
            }
        }
    }



    /// <summary>
    /// Handles the final translated message, displaying it through configured channels.
    /// IMPROVED: Better error isolation and performance optimization.
    /// </summary>
    /// <param name="formattedMessage">The final, formatted SeString ready for display.</param>
    private void OnTranslationReady(SeString formattedMessage)
    {
        if (isDisposed)
            return;

        try
        {
            // PERFORMANCE: Execute display logic on a framework thread for UI safety
            framework.RunOnFrameworkThread(() => DisplayTranslation(formattedMessage));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to queue translation display on framework thread");
        }
    }

    /// <summary>
    /// Displays the translated message through the configured output channels.
    /// SEPARATION OF CONCERNS: Extracted to improve readability and testability.
    /// </summary>
    /// <param name="formattedMessage">The formatted message to display.</param>
    private void DisplayTranslation(SeString formattedMessage)
    {
        try
        {
            var displayMode = displayConfig.DisplayMode;
            var displayedSuccessfully = false;

            // Display in the standard in-game chat unless mode is exclusively the separate window
            if (displayMode != TranslationDisplayMode.SeparateWindow)
            {
                try
                {
                    chatGui.Print(formattedMessage);
                    displayedSuccessfully = true;
                    logger?.LogDebug("Successfully printed message to in-game chat");
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to print message to in-game chat");
                    // Continue with overlay display even if chat print fails
                }
            }

            // Display in the overlay window unless the mode is exclusively in-game chat
            if (displayMode != TranslationDisplayMode.InGameChat)
            {
                try
                {
                    translationOverlayWindow.AddLog(formattedMessage);
                    displayedSuccessfully = true;
                    logger?.LogDebug("Successfully added message to translation overlay");
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to add message to translation overlay");
                }
            }

            if (displayedSuccessfully)
            {
                logger?.LogDebug("Translation displayed successfully via {DisplayMode}", displayMode);
            }
            else
            {
                logger?.LogWarning("Failed to display translation through any configured channel");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Critical error in DisplayTranslation");
        }
    }

    /// <summary>
    /// Performs cleanup of event handlers and resources.
    /// THREAD SAFETY: Protected with lock to prevent race conditions during disposal.
    /// </summary>
    private void Cleanup()
    {
        lock (lockObject)
        {
            try
            {
                messageService.OnTranslationReady -= OnTranslationReady;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error unregistering OnTranslationReady event");
            }

            try
            {
                chatGui.ChatMessage -= OnChatMessage;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error unregistering ChatMessage event");
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (lockObject)
        {
            if (isDisposed)
                return;

            try
            {
                Cleanup();
                isDisposed = true;
                logger?.LogInformation("ChatHookManager disposed successfully.");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error during ChatHookManager disposal");
            }
        }
    }
}
