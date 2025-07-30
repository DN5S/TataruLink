// File: TataruLink/Models/TranslationResult.cs

using System;
using Dalamud.Game.Text;
using TataruLink.Config;
using TataruLink.Interfaces.Services;

namespace TataruLink.Models;

/// <summary>
/// Encapsulates the complete result of a single translation operation, including contextual metadata.
/// This immutable object serves as the data transfer object throughout the translation pipeline.
/// </summary>
public class TranslationResult(
    string originalText,
    string translatedText,
    string sender,
    XivChatType chatType,
    TranslationEngine engineUsed,
    string sourceLanguage,
    string? detectedSourceLanguage,
    string targetLanguage)
{
    #region Metadata
    
    /// <summary>
    /// A unique identifier for this specific translation event.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// The UTC timestamp indicating when the translation result was created.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    
    /// <summary>
    /// The number of characters in the original text, useful for API consumption tracking.
    /// </summary>
    public int CharacterCount => OriginalText.Length;
    
    /// <summary>
    /// The time taken to receive a response from the translation engine, in milliseconds.
    /// This value is enriched by the responsible <see cref="ITranslationEngine"/>.
    /// </summary>
    public long TimeTakenMs { get; init; }

    /// <summary>
    /// A value indicating whether this result was retrieved from the cache.
    /// This value is enriched by the <see cref="ICacheService"/>.
    /// </summary>
    public bool FromCache { get; set; }

    #endregion

    #region Original Message Context

    /// <summary>
    /// The original, untranslated text of the message.
    /// </summary>
    public string OriginalText { get; } = originalText;

    /// <summary>
    /// The sender's name from the original message.
    /// </summary>
    public string Sender { get; } = sender;

    /// <summary>
    /// The chat type (e.g., Say, Party, Shout) of the original message.
    /// </summary>
    public XivChatType ChatType { get; } = chatType;

    #endregion

    #region Translation Details

    /// <summary>
    /// The resulting translated text.
    /// </summary>
    public string TranslatedText { get; } = translatedText;

    /// <summary>
    /// The translation engine that produced this result.
    /// </summary>
    public TranslationEngine EngineUsed { get; } = engineUsed;

    /// <summary>
    /// The source language provided for the translation request (e.g., "auto", "ja").
    /// </summary>
    public string SourceLanguage { get; } = sourceLanguage;

    /// <summary>
    /// The source language automatically detected by the translation engine, if applicable.
    /// This may be null if language detection was disabled or the engine does not provide this information.
    /// </summary>
    public string? DetectedSourceLanguage { get; } = detectedSourceLanguage;

    /// <summary>
    /// The target language requested for the translation (e.g., "en").
    /// </summary>
    public string TargetLanguage { get; } = targetLanguage;

    #endregion
}
