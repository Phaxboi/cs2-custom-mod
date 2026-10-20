using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod.Modifiers;

public class Speed : ConVarModifier
{
    public override string Name => "Triple Speed";
    public override string Description => "Everything runs three times as fast";
    public override string Color => "#ff7043";
    public override char ChatColor => ChatColors.Orange;

    protected override IReadOnlyDictionary<string, string> ConVars { get; } = new Dictionary<string, string>
    {
        ["host_timescale"] = "3", // cheat ConVar; ConVarModifier sets it directly, without sv_cheats
    };
}
