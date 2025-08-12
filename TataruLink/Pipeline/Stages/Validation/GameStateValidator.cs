using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using TataruLink.Configuration;
using TataruLink.Models;
using TataruLink.Services;
using TataruLink.Utils;

namespace TataruLink.Pipeline.Stages.Validation;

public class GameStateValidator(FilterConfig config) : IMessageValidator
{
    public void Initialize()
    {
    }

    public ValueTask<ValidationResult> ValidateAsync(Message message, PipelineContext context)
    {
        if (config.SkipInCutscene)
        {
            // WARNING: NPCs allowed during cutscenes for quest progression
            var isNpcMessage = ChatTypeUtils.IsNpcMessage(message.ChatType);
            
            if (!isNpcMessage && Service.Condition.Any(
                ConditionFlag.OccupiedInCutSceneEvent,
                ConditionFlag.WatchingCutscene,
                ConditionFlag.WatchingCutscene78,
                ConditionFlag.OccupiedInQuestEvent))
            {
                Service.PluginLog.Debug($"GameStateValidator: Skipping non-NPC message during cutscene");
                return new ValueTask<ValidationResult>(ValidationResult.Failure("Player in cutscene (non-NPC message)"));
            }
        }
        
        if (config.SkipInLoading && 
            Service.Condition.Any(
                ConditionFlag.BetweenAreas,
                ConditionFlag.BetweenAreas51))
        {
            Service.PluginLog.Debug($"GameStateValidator: Skipping message during loading");
            return new ValueTask<ValidationResult>(ValidationResult.Failure("Player loading between areas"));
        }
        
  
        if (config.SkipInRetainer && 
            Service.Condition[ConditionFlag.OccupiedSummoningBell])
        {
            Service.PluginLog.Debug($"GameStateValidator: Skipping message at retainer bell");
            return new ValueTask<ValidationResult>(ValidationResult.Failure("Player at retainer bell"));
        }
        
        if (Service.Condition[ConditionFlag.LoggingOut])
        {
            Service.PluginLog.Debug($"GameStateValidator: Skipping message during logout");
            return new ValueTask<ValidationResult>(ValidationResult.Failure("Player logging out"));
        }
        
        return new ValueTask<ValidationResult>(ValidationResult.Success());
    }

    public void Dispose()
    {
    }
}
