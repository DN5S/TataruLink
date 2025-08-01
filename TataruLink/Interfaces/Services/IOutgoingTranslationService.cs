// File: TataruLink/Interfaces/Services/IOutgoingTranslationService.cs

using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;

namespace TataruLink.Interfaces.Services;

/// <summary>
/// Defines a service for processing outgoing messages for translation and replacement.
/// </summary>
public interface IOutgoingTranslationService
{
    /// <summary>
    /// Processes a user-input message, translates it, and replaces the content of the chat input box.
    /// </summary>
    /// <param name="message">The original SeString message from the chat input box.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous translation operation.</returns>
    Task ProcessTranslationAsync(SeString message, CancellationToken cancellationToken = default);

}
