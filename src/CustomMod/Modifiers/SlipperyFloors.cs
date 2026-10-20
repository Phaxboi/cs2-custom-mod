using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod.Modifiers;

public class SlipperyFloors : ConVarModifier
{
    public override string Name => "Slippery Floors";
    public override string Description => "The ground is ice, good luck stopping";
    public override string Color => "#b3f0ff";
    public override char ChatColor => ChatColors.Blue;

    protected override IReadOnlyDictionary<string, string> ConVars { get; } = new Dictionary<string, string>
    {
        ["sv_friction"] = "0.25", // default 5.2
    };
}
