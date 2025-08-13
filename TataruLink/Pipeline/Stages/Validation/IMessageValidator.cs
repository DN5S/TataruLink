using System.Threading.Tasks;
using TataruLink.Models;

namespace TataruLink.Pipeline.Stages.Validation;

public interface IMessageValidator
{
    void Initialize();
    
    ValueTask<ValidationResult> ValidateAsync(Message message, PipelineContext context);
    
    void Dispose();
}

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