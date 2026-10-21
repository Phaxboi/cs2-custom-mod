using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod.Modifiers;

/// <summary>
/// Everyone gets an AWP (and a knife) and every AWP shot also launches a live HE grenade.
/// The grenade is spawned through the game's own HE create function, the same one
/// cs_script's Instance.SpawnGrenadeProjectile calls, so it detonates and credits the shooter.
/// </summary>
public class GrenadeLauncher : LoadoutModifier
{
    // CHEGrenadeProjectile::Create(origin, angles, velocity, angularVelocity, thrower, itemDefIndex, float)
    // in libserver.so (Linux). Found through Instance.SpawnGrenadeProjectile; may break on CS2 updates.
    private const string CreateSignature =
        "55 4C 89 C1 48 89 E5 41 57 49 89 FF 41 56 49 89 D6 48 89 F2 48 89 FE 41 55 48 8D 3D ? ? ? ? 4D 89 C5 41 54 45 89 CC 53";
    private const int HeGrenadeItemDef = 44;
    private const float LaunchSpeed = 1500f;
    private const float SpawnDistance = 32f; // in front of the eyes, so it does not hit the shooter

    public override string Name => "Grenade Launcher";
    public override string Description => "AWPs only. Every shot fires a live HE";
    public override string Color => "#9ccc65";
    public override char ChatColor => ChatColors.Green;

    protected override IReadOnlyList<string> Loadout { get; } = ["weapon_knife", "weapon_awp"];

    private MemoryFunctionWithReturn<nint, nint, nint, nint, nint, int, float, nint>? _create;
    private readonly BasePlugin.GameEventHandler<EventWeaponFire> _onWeaponFire;
    private readonly Listeners.OnEntityTakeDamagePre _onTakeDamage;

    public GrenadeLauncher()
    {
        _onWeaponFire = OnWeaponFire;
        _onTakeDamage = OnTakeDamage;
    }

    public override void Apply()
    {
        // Resolved here rather than in the constructor so a broken signature only fails this modifier
        _create ??= new MemoryFunctionWithReturn<nint, nint, nint, nint, nint, int, float, nint>(CreateSignature);

        base.Apply();
        Plugin.RegisterEventHandler(_onWeaponFire);
        Plugin.RegisterListener(_onTakeDamage);
    }

    public override void Revert()
    {
        base.Revert();
        Plugin.DeregisterEventHandler(_onWeaponFire);
        Plugin.RemoveListener(_onTakeDamage);
    }

    // AWP bullets do no damage, so only the grenades kill. Everything else (HE, knife, zeus,
    // falling) is untouched.
    private HookResult OnTakeDamage(CBaseEntity entity, CTakeDamageInfo info) =>
        info.Ability.Value?.DesignerName == "weapon_awp" ? HookResult.Handled : HookResult.Continue;

    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        if (!@event.Weapon.Contains("awp"))
            return HookResult.Continue;

        var pawn = @event.Userid?.PlayerPawn.Value;
        if (pawn?.AbsOrigin == null || _create == null)
            return HookResult.Continue;

        var angles = pawn.EyeAngles;
        var pitch = angles.X * MathF.PI / 180f;
        var yaw = angles.Y * MathF.PI / 180f;
        var forward = new Vector(MathF.Cos(pitch) * MathF.Cos(yaw), MathF.Cos(pitch) * MathF.Sin(yaw), -MathF.Sin(pitch));

        var eye = pawn.AbsOrigin.Z + pawn.ViewOffset.Z;
        var origin = new Vector(
            pawn.AbsOrigin.X + forward.X * SpawnDistance,
            pawn.AbsOrigin.Y + forward.Y * SpawnDistance,
            eye + forward.Z * SpawnDistance);
        var velocity = new Vector(forward.X * LaunchSpeed, forward.Y * LaunchSpeed, forward.Z * LaunchSpeed);
        var spin = new Vector(600, 0, 0);

        _create.Invoke(origin.Handle, new QAngle(0, 0, 0).Handle, velocity.Handle, spin.Handle,
            pawn.Handle, HeGrenadeItemDef, 3f);

        return HookResult.Continue;
    }
}
