using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod;

public class CustomMod : BasePlugin
{
    public override string ModuleName => "CustomMod";
    public override string ModuleVersion => "0.1.0";
    public override string ModuleAuthor => "Phaxboi";
    public override string ModuleDescription => "Test mod: USP-S deals 5x damage and announces kills";

    private const ushort UspDefIndex = 61;
    private const float UspDamageMultiplier = 5f;

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnPlayerTakeDamagePre>(OnPlayerTakeDamagePre);
    }

    private HookResult OnPlayerTakeDamagePre(CCSPlayerPawn victim, CTakeDamageInfo info)
    {
        // Ability is the weapon the damage came from
        var weapon = info.Ability.Value?.As<CBasePlayerWeapon>();
        if (weapon != null && weapon.IsValid && weapon.AttributeManager.Item.ItemDefinitionIndex == UspDefIndex)
            info.Damage *= UspDamageMultiplier;

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (@event.Weapon is not ("usp_silencer" or "usp_silencer_off"))
            return HookResult.Continue;

        var attacker = @event.Attacker;
        var victim = @event.Userid;
        if (attacker == null || !attacker.IsValid || victim == null || !victim.IsValid || attacker == victim)
            return HookResult.Continue;

        Server.PrintToChatAll(
            $" {ChatColors.Green}[CustomMod]{ChatColors.Default} {attacker.PlayerName} killed {victim.PlayerName} with the {ChatColors.Gold}USP-S{ChatColors.Default}!");
        return HookResult.Continue;
    }
}
