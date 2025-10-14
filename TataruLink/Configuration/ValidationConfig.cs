namespace TataruLink.Configuration;

public class ValidationConfig
{
    public int DuplicateDetectionPeriodMs { get; set; } = 1000;  // 1 second

    public bool EnableDeduplication { get; set; } = true;

    public int MinContentLength { get; set; } = 1;
}