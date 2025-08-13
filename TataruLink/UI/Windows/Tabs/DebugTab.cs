using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using TataruLink.Configuration;
using TataruLink.DtrBar;
using TataruLink.Services;
using TataruLink.Translation;
using TataruLink.Utils;

namespace TataruLink.UI.Windows.Tabs;

public class DebugTab(TataruConfig configuration, ITranslationService translationService, DtrBarManager? dtrBarManager)
{
    public void Draw()
    {
        // Debug Mode Section
        ImGuiUtils.Section("Debug Settings");
        
        var debugMode = configuration.DebugMode;
        if (ImGui.Checkbox("Enable Debug Mode"u8, ref debugMode))
        {
            configuration.DebugMode = debugMode;
            Service.Configuration.Save();
        }
        ImGui.SameLine();
        ImGuiUtils.HelpMarker("Enables detailed logging and debug features");
        
        if (configuration.DebugMode)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Debug Information"u8);
            ImGui.TextUnformatted($"Config Version: {configuration.Version}");
            ImGui.TextUnformatted($"Translation Provider: {translationService.ProviderName}");
            ImGui.TextUnformatted($"Provider Configured: {translationService.IsConfigured}");
            
            // API Key status
            ImGui.Separator();
            ImGui.TextUnformatted("API Keys:"u8);
            
            if (configuration.Translation.ApiKeys.Count != 0)
            {
                foreach (var kvp in configuration.Translation.ApiKeys)
                {
                    ImGuiUtils.TextColored(ImGuiUtils.Colors.Success, $"  {kvp.Key}: Configured");
                }
            }
            else
            {
                ImGui.TextDisabled("  No API keys configured"u8);
            }
            
            ImGui.Separator();
            
            // Performance Settings Section
            ImGuiUtils.Section("Performance Settings");
            
            var enableCache = configuration.Performance.EnableCache;
            if (ImGui.Checkbox("Enable Translation Cache"u8, ref enableCache))
            {
                configuration.Performance.EnableCache = enableCache;
                Service.Configuration.Save();
            }
            
            var translationsPerSecond = configuration.Performance.TranslationsPerSecond;
            if (ImGui.SliderInt("Translations Per Second"u8, ref translationsPerSecond, 1, 20))
            {
                configuration.Performance.TranslationsPerSecond = translationsPerSecond;
                Service.Configuration.Save();
            }
            ImGuiUtils.HelpMarker("Rate limit to avoid overwhelming translation APIs");
            
            var maxQueueSize = configuration.Performance.MaxQueueSize;
            if (ImGui.SliderInt("Max Queue Size"u8, ref maxQueueSize, 100, 5000))
            {
                configuration.Performance.MaxQueueSize = maxQueueSize;
                Service.Configuration.Save();
            }
            
            var maxHistory = configuration.Performance.MaxMessageHistory;
            if (ImGui.SliderInt("Max Message History"u8, ref maxHistory, 50, 2000))
            {
                configuration.Performance.MaxMessageHistory = maxHistory;
                Service.Configuration.Save();
            }
            
            ImGui.Separator();
            
            // DTR Bar statistics section
            if (dtrBarManager != null)
            {
                ImGui.TextUnformatted("DTR Bar Statistics:"u8);
                ImGui.TextUnformatted($"  Translation Count: {dtrBarManager.GetTranslationCount()}");
                
                if (ImGui.Button("Reset Translation Counter"u8))
                {
                    dtrBarManager.ResetTranslationCount();
                    Service.PluginLog.Debug("Translation counter reset to 0");
                }
                
                ImGui.Separator();
            }
            
            // Pipeline failure debugging section
            if (ImGui.CollapsingHeader("Pipeline Failures"u8))
            {
                DrawPipelineFailures();
            }
            
            ImGui.Separator();
            if (ImGui.Button("Reset Configuration"u8))
            {
                Service.Configuration.Reset();
            }
        }
    }
    
    private void DrawPipelineFailures()
    {
        var failures = Service.PipelineDebug.GetRecentFailures();
        var stats = Service.PipelineDebug.GetFailureStatistics();
        
        // Statistics summary
        ImGui.TextUnformatted($"Total Failures: {failures.Count}");
        
        if (stats.Count > 0)
        {
            ImGui.TextUnformatted("Failure Types:"u8);
            foreach (var (type, count) in stats.OrderByDescending(x => x.Value))
            {
                ImGui.TextDisabled($"  {type}: {count}");
            }
        }
        
        ImGui.Spacing();
        
        // Filter controls
        ImGui.TextUnformatted("Recent Failures (last 100):"u8);
        
        if (ImGui.Button("Clear History"u8))
        {
            Service.PipelineDebug.ClearFailures();
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Last 5 min"u8))
        {
            failures = Service.PipelineDebug.GetRecentFailures(5);
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Last 15 min"u8))
        {
            failures = Service.PipelineDebug.GetRecentFailures(15);
        }
        
        ImGui.SameLine();
        if (ImGui.Button("All"u8))
        {
            failures = Service.PipelineDebug.GetRecentFailures();
        }
        
        ImGui.Separator();
        
        // Failure list
        if (failures.Count == 0)
        {
            ImGui.TextDisabled("No failures recorded"u8);
        }
        else
        {
            // Table with failures
            if (ImGui.BeginTable("FailuresTable"u8, 5, 
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | 
                ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,
                new Vector2(0, 300)))
            {
                ImGui.TableSetupColumn("Time"u8, ImGuiTableColumnFlags.WidthFixed, 70);
                ImGui.TableSetupColumn("Stage"u8, ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Reason"u8, ImGuiTableColumnFlags.WidthStretch, 0.4f);
                ImGui.TableSetupColumn("Chat Type"u8, ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Message"u8, ImGuiTableColumnFlags.WidthStretch, 0.6f);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableHeadersRow();
                
                foreach (var failure in failures.OrderByDescending(f => f.Timestamp))
                {
                    ImGui.TableNextRow();
                    
                    // Time
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{failure.Timestamp:HH:mm:ss}");
                    
                    // Stage
                    ImGui.TableNextColumn();
                    var stageColor = failure.FailureStage == "Validation" 
                        ? ImGuiUtils.Colors.Warning
                        : ImGuiUtils.Colors.Error;
                    ImGuiUtils.TextColored(stageColor, failure.FailureStage);
                    
                    // Reason
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped(failure.FailureReason);
                    
                    // Chat Type
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(failure.ChatType);
                    
                    // Message content (truncated)
                    ImGui.TableNextColumn();
                    var content = failure.Content.Length > 100 
                        ? failure.Content[..97] + "..." 
                        : failure.Content;
                    ImGui.TextWrapped(content);
                    
                    // Tooltip with full details on hover
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted($"Full Message: {failure.Content}");
                        if (!string.IsNullOrEmpty(failure.SenderName))
                            ImGui.TextUnformatted($"Sender: {failure.SenderName}");
                        ImGui.TextUnformatted($"Message ID: {failure.MessageId}");
                        
                        if (failure.ContextData.Count > 0)
                        {
                            ImGui.Separator();
                            ImGui.TextUnformatted("Context Data:"u8);
                            foreach (var (key, value) in failure.ContextData)
                            {
                                ImGui.TextDisabled($"  {key}: {value ?? "null"}");
                            }
                        }
                        ImGui.EndTooltip();
                    }
                }
                
                ImGui.EndTable();
            }
        }
    }
}
