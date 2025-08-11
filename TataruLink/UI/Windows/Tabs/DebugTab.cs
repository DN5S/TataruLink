using System.Linq;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.Services;
using TataruLink.Translation;
using TataruLink.Utils;

namespace TataruLink.UI.Windows.Tabs;

/// <summary>
/// Debug settings tab
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class DebugTab(TataruConfig configuration, ITranslationService translationService)
{
    public void Draw()
    {
        var debugMode = configuration.DebugMode;
        if (ImGui.Checkbox("Enable Debug Mode", ref debugMode))
        {
            configuration.DebugMode = debugMode;
            configuration.Save();
        }
        
        if (configuration.DebugMode)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Debug Information");
            ImGui.TextUnformatted($"Config Version: {configuration.Version}");
            ImGui.TextUnformatted($"Translation Provider: {translationService.ProviderName}");
            ImGui.TextUnformatted($"Provider Configured: {translationService.IsConfigured}");
            
            // API Key encryption status
            ImGui.Separator();
            ImGui.TextUnformatted("API Key Security Status:");
            
            foreach (var kvp in configuration.Translation.ApiKeys)
            {
                var isProtected = SecureStorage.IsProtected(kvp.Value);
                var status = isProtected ? "Encrypted" : "Plain Text (Legacy)";
                var statusColor = isProtected 
                    ? new System.Numerics.Vector4(0, 1, 0, 1)  // Green
                    : new System.Numerics.Vector4(1, 1, 0, 1);  // Yellow
                    
                ImGui.TextColored(statusColor, $"  {kvp.Key}: {status}");
                
                // Option to migrate plain text keys
                if (!isProtected)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Encrypt##{kvp.Key}"))
                    {
                        // Re-save the key to trigger encryption
                        configuration.Translation.SetApiKey(kvp.Key, kvp.Value);
                        configuration.Save();
                        Service.PluginLog.Information($"Encrypted API key for {kvp.Key}");
                    }
                }
            }
            
            if (!configuration.Translation.ApiKeys.Any())
            {
                ImGui.TextDisabled("  No API keys configured");
            }
            
            ImGui.Separator();
            if (ImGui.Button("Reset Configuration"))
            {
                configuration.Reset();
                Service.PluginLog.Information("Configuration reset to defaults");
            }
        }
    }
}
