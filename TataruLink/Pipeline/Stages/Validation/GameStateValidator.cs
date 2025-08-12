using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.Pipeline.Stages.Validation;

/// <summary>
/// Validates messages based on current game state conditions.
/// Skips translation when the player is in states where they can't see or won't read chat.
/// </summary>
public class GameStateValidator : IMessageValidator
{
    private readonly FilterConfig config;

    public GameStateValidator(FilterConfig config)
    {
        this.config = config;
    }

    public void Initialize()
    {
        // No initialization needed for this validator
    }

    public Task<ValidationResult> ValidateAsync(Message message, PipelineContext context)
    {
        // Check cutscene conditions
        if (config.SkipInCutscene)
        {
            // Special handling: Allow NPC messages even during cutscenes
            var isNpcMessage = ChatTypeUtils.IsNpcMessage(message.ChatType);
            
            if (!isNpcMessage && Service.Condition.Any(
                ConditionFlag.OccupiedInCutSceneEvent,
                ConditionFlag.WatchingCutscene,
                ConditionFlag.WatchingCutscene78,
                ConditionFlag.OccupiedInQuestEvent))
            {
                Service.PluginLog.Debug($"GameStateValidator: Skipping non-NPC message during cutscene");
                return Task.FromResult(ValidationResult.Failure("Player in cutscene (non-NPC message)"));
            }
        }
        
        // Check loading conditions
        if (config.SkipInLoading && 
            Service.Condition.Any(
                ConditionFlag.BetweenAreas,
                ConditionFlag.BetweenAreas51))
        {
            Service.PluginLog.Debug($"GameStateValidator: Skipping message during loading");
            return Task.FromResult(ValidationResult.Failure("Player loading between areas"));
        }
        
        // Check retainer conditions  
        if (config.SkipInRetainer && 
            Service.Condition[ConditionFlag.OccupiedSummoningBell])
        {
            Service.PluginLog.Debug($"GameStateValidator: Skipping message at retainer bell");
            return Task.FromResult(ValidationResult.Failure("Player at retainer bell"));
        }
        
        // Always skip when logging out
        if (Service.Condition[ConditionFlag.LoggingOut])
        {
            Service.PluginLog.Debug($"GameStateValidator: Skipping message during logout");
            return Task.FromResult(ValidationResult.Failure("Player logging out"));
        }
        
        return Task.FromResult(ValidationResult.Success());
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}