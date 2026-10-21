using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod.Modifiers;

public class BunnyHop : ConVarModifier
{
    public override string Name => "Bunny Hop";
    public override string Description => "Hold jump to hop, keep your speed";
    public override string Color => "#7cff6b";
    public override char ChatColor => ChatColors.Lime;

    protected override IReadOnlyDictionary<string, string> ConVars { get; } = new Dictionary<string, string>
    {
        ["sv_autobunnyhopping"] = "1", // re-jump while holding jump
        ["sv_enablebunnyhopping"] = "1", // no speed cap when landing a hop
    };
}
