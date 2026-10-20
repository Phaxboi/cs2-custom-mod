using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod.Modifiers;

public class DoubleSpeed : ConVarModifier
{
    public override string Name => "Double Speed";
    public override string Description => "Everything runs twice as fast";
    public override string Color => "#ff7043";
    public override char ChatColor => ChatColors.Orange;

    protected override IReadOnlyDictionary<string, string> ConVars { get; } = new Dictionary<string, string>
    {
        ["host_timescale"] = "2", // cheat ConVar; ConVarModifier lifts the cheat flag while active
    };
}
