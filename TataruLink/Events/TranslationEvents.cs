using System;
using Dalamud.Game.Text.SeStringHandling;
using TataruLink.Models;

namespace TataruLink.Events;

public abstract class TranslationEventBase
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public long MessageId { get; init; }
}

public class ChatMessageReceivedEvent : TranslationEventBase
{
    public ushort ChatType { get; init; }
    public SeString Sender { get; init; } = null!;
    public SeString Content { get; init; } = null!;
    public string SenderName { get; init; } = string.Empty;
    public string PlainTextContent { get; init; } = string.Empty;
}

public class MessageValidationFailedEvent : TranslationEventBase
{
    public string Reason { get; init; } = string.Empty;
    public Message Message { get; init; } = null!;
}

public class TranslationRequestedEvent : TranslationEventBase
{
    public Message Message { get; init; } = null!;
    public string SourceLanguage { get; init; } = string.Empty;
    public string TargetLanguage { get; init; } = string.Empty;
}

public class GlossaryAppliedEvent : TranslationEventBase
{
    public string OriginalText { get; init; } = string.Empty;
    public string ModifiedText { get; init; } = string.Empty;
    public int MatchCount { get; init; }
}

public class TranslationCompletedEvent : TranslationEventBase
{
    public Message Message { get; init; } = null!;
    public string TranslatedText { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public TimeSpan TranslationTime { get; init; }
}

public class TranslationFailedEvent : TranslationEventBase
{
    public Message Message { get; init; } = null!;
    public string Error { get; init; } = string.Empty;
    public string? UserFriendlyError { get; init; }
    public Exception? Exception { get; init; }
}

public class MessageDisplayedEvent : TranslationEventBase
{
    public Message Message { get; init; } = null!;
    public string DisplayText { get; init; } = string.Empty;
    public bool DisplayedInChat { get; init; }
    public bool DisplayedInOverlay { get; init; }
}
