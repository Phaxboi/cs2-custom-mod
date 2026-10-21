using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod.Modifiers;

/// <summary>
/// A modifier that gives everyone a fixed loadout and disables buying for the round.
/// On revert, survivors are reset to a knife and their team's default pistol, so the loadout
/// does not carry over into the next round.
/// </summary>
public abstract class LoadoutModifier : ConVarModifier
{
    /// <summary>Items given to every alive player, e.g. "weapon_knife".</summary>
    protected abstract IReadOnlyList<string> Loadout { get; }

    /// <summary>Armor everyone gets (0 removes it). Bought or carried-over armor is replaced.</summary>
    protected virtual int Armor => 0;

    protected virtual bool Helmet => false;

    protected override IReadOnlyDictionary<string, string> ConVars { get; } = new Dictionary<string, string>
    {
        // 3 = nobody can buy. Only read when a round is set up, so a modifier applied
        // mid-round (!modifier) blocks buying from the next round on
        ["sv_buy_status_override"] = "3",
    };

    private readonly BasePlugin.GameEventHandler<EventPlayerSpawn> _onPlayerSpawn;

    protected LoadoutModifier()
    {
        _onPlayerSpawn = OnPlayerSpawn;
    }

    public override void Apply()
    {
        base.Apply();
        Plugin.RegisterEventHandler(_onPlayerSpawn);

        // Players already alive (modifier applied mid-round); everyone else gets it on spawn
        foreach (var player in AlivePlayers())
            GiveLoadout(player);
    }

    public override void Revert()
    {
        base.Revert();
        Plugin.DeregisterEventHandler(_onPlayerSpawn);

        foreach (var player in AlivePlayers())
        {
            player.RemoveWeapons();
            player.GiveNamedItem("weapon_knife");
            player.GiveNamedItem(player.Team == CsTeam.CounterTerrorist ? "weapon_hkp2000" : "weapon_glock");
        }
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;

        // Next frame, so the game has finished handing out the spawn loadout first
        Server.NextFrame(() =>
        {
            if (player is { IsValid: true, PawnIsAlive: true })
                GiveLoadout(player);
        });

        return HookResult.Continue;
    }

    private void GiveLoadout(CCSPlayerController player)
    {
        player.RemoveWeapons();
        foreach (var item in Loadout)
            player.GiveNamedItem(item);
        SetArmor(player, Armor, Helmet);
    }

    private static IEnumerable<CCSPlayerController> AlivePlayers() =>
        Utilities.GetPlayers().Where(p => p is { IsValid: true, PawnIsAlive: true });

    private static void SetArmor(CCSPlayerController player, int armor, bool helmet)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null)
            return;

        pawn.ArmorValue = armor;
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_ArmorValue");

        if (pawn.ItemServices != null)
        {
            new CCSPlayer_ItemServices(pawn.ItemServices.Handle).HasHelmet = helmet;
            Utilities.SetStateChanged(pawn, "CBasePlayerPawn", "m_pItemServices");
        }
    }
}
