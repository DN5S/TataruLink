using System;

namespace TataruLink.Configuration;

[Serializable]
public class ApiSettings
{
    public string? DeepLApiKey { get; set; }

    // TODO: Google API Key, Papago API Key 등 다른 번역기 API 설정 추가
}
