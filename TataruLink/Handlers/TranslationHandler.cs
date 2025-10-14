using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TataruLink.Configuration;
using TataruLink.DtrBar;
using TataruLink.Events;
using TataruLink.Glossary;
using TataruLink.Translation;
using TataruLink.Utils;

namespace TataruLink.Handlers;

public class TranslationHandler : IEventHandler<TranslationRequestedEvent>
{
    private readonly EventBus eventBus;
    private readonly TataruConfig configuration;
    private readonly ITranslationService translationService;
    private readonly GlossaryManager glossaryManager;
    private readonly DtrBarManager? dtrBarManager;

    public TranslationHandler(
        EventBus eventBus,
        TataruConfig configuration,
        ITranslationService translationService,
        GlossaryManager glossaryManager,
        DtrBarManager? dtrBarManager)
    {
        this.eventBus = eventBus;
        this.configuration = configuration;
        this.translationService = translationService;
        this.glossaryManager = glossaryManager;
        this.dtrBarManager = dtrBarManager;
    }

    public async Task HandleAsync(TranslationRequestedEvent @event)
    {
        var message = @event.Message;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Service.PluginLog.Debug($"Processing translation for message {message.Id}: {message.PlainTextContent}");

            if (!translationService.IsConfigured)
            {
                await PublishFailure(message, "Translation service not configured", "Please configure a translation provider in settings").ConfigureAwait(false);
                return;
            }

            dtrBarManager?.UpdateStatus(true);

            var useXmlTags = translationService.SupportsStructuredTranslation;

            // Prepare text with glossary application
            var glossaryTask = Task.Run(() =>
            {
                var (text, _, _) = SeStringUtils.PrepareForProviderWithGlossary(
                    message.OriginalContent,
                    useXmlTags,
                    glossaryManager.Apply);
                return text;
            });

            var textToTranslate = await glossaryTask.ConfigureAwait(false);

            Service.PluginLog.Debug($"Text prepared for translation: {textToTranslate}");

            // Translate
            var translatedText = await translationService.TranslateAsync(
                textToTranslate,
                @event.SourceLanguage,
                @event.TargetLanguage).ConfigureAwait(false);

            if (translatedText != null)
            {
                stopwatch.Stop();

                message.SetTranslation(
                    translatedText,
                    translationService.ProviderName,
                    stopwatch.Elapsed);

                Service.PluginLog.Debug($"Translation completed: {textToTranslate} -> {translatedText}");

                dtrBarManager?.IncrementTranslationCount();
                dtrBarManager?.UpdateStatus(false);

                var completedEvent = new TranslationCompletedEvent
                {
                    MessageId = message.Id,
                    Message = message,
                    TranslatedText = translatedText,
                    Provider = translationService.ProviderName,
                    TranslationTime = stopwatch.Elapsed
                };

                await eventBus.PublishAsync(completedEvent).ConfigureAwait(false);
            }
            else
            {
                var providerStatus = translationService.GetActiveProviderStatus();
                var errorMessage = providerStatus?.LastError?.Message ?? "Translation returned null";
                var userFriendlyError = providerStatus?.LastError?.UserFriendlyMessage;

                await PublishFailure(message, errorMessage, userFriendlyError).ConfigureAwait(false);
                dtrBarManager?.UpdateStatus(false);
            }
        }
        catch (OperationCanceledException)
        {
            Service.PluginLog.Debug($"Translation cancelled for message {message.Id}");
            await PublishFailure(message, "Translation cancelled or timed out", "Translation took too long. Try increasing the timeout in settings.").ConfigureAwait(false);
            dtrBarManager?.UpdateStatus(false);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, $"Error in translation handler for message {message.Id}");

            var error = TranslationError.FromException(ex, translationService.ProviderName);
            await PublishFailure(message, error.Message, error.UserFriendlyMessage).ConfigureAwait(false);
            dtrBarManager?.UpdateStatus(false);
        }
    }

    private async Task PublishFailure(Models.Message message, string error, string? userFriendlyError)
    {
        message.ErrorMessage = error;
        message.Status = Models.TranslationStatus.Failed;

        var failureEvent = new TranslationFailedEvent
        {
            MessageId = message.Id,
            Message = message,
            Error = error,
            UserFriendlyError = userFriendlyError
        };

        Service.PluginLog.Warning($"Translation failed for message {message.Id}: {error}");
        await eventBus.PublishAsync(failureEvent).ConfigureAwait(false);
    }
}
