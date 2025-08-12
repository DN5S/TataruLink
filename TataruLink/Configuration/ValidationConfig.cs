namespace TataruLink.Configuration;

public class ValidationConfig
{
    public int DuplicateDetectionPeriodMs { get; set; } = 1000;  // 1 second
    
    public bool PreserveAutoTranslate { get; set; } = true;
}