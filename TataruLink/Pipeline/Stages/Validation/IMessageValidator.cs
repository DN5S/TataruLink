using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Pipeline.Stages.Validation;

/// <summary>
/// Interface for message validators used within the validation pipeline stage.
/// </summary>
public interface IMessageValidator
{
    /// <summary>
    /// Initialize the validator.
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Validate a message.
    /// </summary>
    ValueTask<ValidationResult> ValidateAsync(Message message, PipelineContext context);
    
    /// <summary>
    /// Clean up resources.
    /// </summary>
    void Dispose();
}

/// <summary>
/// Result of a validation check.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public string Reason { get; }
    
    private ValidationResult(bool isValid, string reason)
    {
        IsValid = isValid;
        Reason = reason;
    }
    
    public static ValidationResult Success() => new(true, "Validation passed");
    public static ValidationResult Failure(string reason) => new(false, reason);
}