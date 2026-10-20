using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod.Modifiers;

public class LowGravity : ConVarModifier
{
    public override string Name => "Low Gravity";
    public override string Description => "Jump higher, fall slower";
    public override string Color => "#7fd4ff";
    public override char ChatColor => ChatColors.LightBlue;

    protected override IReadOnlyDictionary<string, string> ConVars { get; } = new Dictionary<string, string>
    {
        ["sv_gravity"] = "300", // default 800
    };
}
