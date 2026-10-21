using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod.Modifiers;

/// <summary>
/// Everyone is stripped to a knife, an HE grenade and a molotov. A thrown grenade is replaced
/// right away, and buying is disabled. The C4 is removed too, so rounds end by elimination or time.
/// </summary>
public class GrenadeParty : LoadoutModifier
{
    public override string Name => "Grenade Party";
    public override string Description => "Knife, HE and molotov only. Infinite grenades";
    public override string Color => "#ffb300";
    public override char ChatColor => ChatColors.Yellow;

    protected override IReadOnlyList<string> Loadout { get; } = ["weapon_knife", "weapon_hegrenade", "weapon_molotov"];

    private readonly BasePlugin.GameEventHandler<EventGrenadeThrown> _onGrenadeThrown;

    public GrenadeParty()
    {
        _onGrenadeThrown = OnGrenadeThrown;
    }

    public override void Apply()
    {
        base.Apply();
        Plugin.RegisterEventHandler(_onGrenadeThrown);
    }

    public override void Revert()
    {
        base.Revert();
        Plugin.DeregisterEventHandler(_onGrenadeThrown);
    }

    private HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is not { IsValid: true, PawnIsAlive: true })
            return HookResult.Continue;

        // Event names the grenade without the prefix, e.g. "hegrenade" or "molotov"
        var item = $"weapon_{@event.Weapon}";
        Server.NextFrame(() =>
        {
            if (player.IsValid && player.PawnIsAlive)
                player.GiveNamedItem(item);
        });

        return HookResult.Continue;
    }
}
