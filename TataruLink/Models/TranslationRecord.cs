// File: TataruLink/Models/TranslationRecord.cs
using System;
using Dalamud.Game.Text;
using TataruLink.Configuration;

namespace TataruLink.Models;

/// <summary>
/// Represents a complete record of a single translation event, including all relevant metadata.
/// This object is used for caching, formatting, and future analytics.
/// </summary>
public class TranslationRecord(
    string originalText,
    string translatedText,
    string sender,
    XivChatType chatType,
    TranslationEngine engineUsed,
    string sourceLanguage,
    string? detectedSourceLanguage,
    string targetLanguage)
{
    #region Original Message Context

    public string OriginalText { get; } = originalText;
    public string Sender { get; } = sender;
    public XivChatType ChatType { get; } = chatType;

    #endregion

    #region Translation Details

    public string TranslatedText { get; } = translatedText;
    public TranslationEngine EngineUsed { get; } = engineUsed;

    /// <summary>
    /// The source language used for the translation. Can be "auto" or a specific code.
    /// </summary>
    public string SourceLanguage { get; } = sourceLanguage;

    /// <summary>
    /// The language code automatically detected by the engine. Null if detection was off or failed.
    /// </summary>
    public string? DetectedSourceLanguage { get; } = detectedSourceLanguage;

    /// <summary>
    /// The target language for the translation, specified by the user's configuration.
    /// </summary>
    public string TargetLanguage { get; } = targetLanguage;

    #endregion

    #region Metadata

    public Guid Id { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public int CharacterCount => OriginalText.Length;
    public long TimeTakenMs { get; set; }
    public bool FromCache { get; set; }

    #endregion
}
