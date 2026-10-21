using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod.Modifiers;

/// <summary>
/// Every death beeps like a C4 and then explodes, damaging everyone nearby. Players killed by
/// an explosion explode too, so deaths can chain. Kills are credited to the world.
/// Ragdolls are client-side, so they are made to fly further through replicated ConVars.
/// </summary>
public class ExplodingBodies : ConVarModifier
{
    private const int Damage = 200;
    private const int Radius = 600;
    private const float FuseSeconds = 0.5f;
    private const int Beeps = 2;
    private const string BeepSound = "C4.PlantSound";
    private const string ExplosionSound = "BaseGrenade.Explode";
    private const string ExplosionEffect = "particles/explosions_fx/explosion_hegrenade.vpcf";
    private const uint EnvExplosionNoSound = 64; // env_explosion spawnflag; we play our own sound

    public override string Name => "Exploding Bodies";
    public override string Description => "Every death goes boom, keep your distance";
    public override string Color => "#ff3b30";
    public override char ChatColor => ChatColors.Red;
    public override IEnumerable<string> Resources => [ExplosionEffect];

    protected override IReadOnlyDictionary<string, string> ConVars { get; } = new Dictionary<string, string>
    {
        ["phys_pushscale"] = "5", // default 1: force from damage on physics objects and death ragdolls
        ["ragdoll_vphysics_scale"] = "2", // default 0.5: physics impacts against ragdolls
        ["ragdoll_gravity_scale"] = "0.5", // default 1: bodies fall slower and fly further
    };

    // Bumped on apply and revert so explosions still on their fuse are cancelled
    private int _generation;
    private readonly BasePlugin.GameEventHandler<EventPlayerDeath> _onPlayerDeath;

    public ExplodingBodies()
    {
        _onPlayerDeath = OnPlayerDeath;
    }

    public override void Apply()
    {
        base.Apply();
        _generation++;
        Plugin.RegisterEventHandler(_onPlayerDeath);
    }

    public override void Revert()
    {
        base.Revert();
        _generation++;
        Plugin.DeregisterEventHandler(_onPlayerDeath);
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var pawn = @event.Userid?.PlayerPawn.Value;
        if (pawn?.AbsOrigin == null)
            return HookResult.Continue;

        var position = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z + 10);
        var generation = _generation;

        for (var i = 0; i < Beeps; i++)
        {
            Plugin.AddTimer(i * FuseSeconds / Beeps, () =>
            {
                if (generation == _generation && pawn.IsValid)
                    pawn.EmitSound(BeepSound);
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        Plugin.AddTimer(FuseSeconds, () =>
        {
            if (generation == _generation)
                Explode(position);
        }, TimerFlags.STOP_ON_MAPCHANGE);

        return HookResult.Continue;
    }

    private void Explode(Vector position)
    {
        PlayEffect(position);

        // env_explosion only does the damage and physics push; its default fireball does not show in CS2
        var explosion = Utilities.CreateEntityByName<CEnvExplosion>("env_explosion");
        if (explosion == null)
            return;

        explosion.Spawnflags |= EnvExplosionNoSound;
        explosion.Magnitude = Damage;
        explosion.RadiusOverride = Radius;
        explosion.Teleport(position, new QAngle(0, 0, 0), new Vector(0, 0, 0));
        explosion.DispatchSpawn();
        explosion.AcceptInput("Explode");

        Plugin.AddTimer(1f, () =>
        {
            if (explosion.IsValid)
                explosion.Remove();
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    /// <summary>HE grenade explosion particle and sound at the position.</summary>
    private void PlayEffect(Vector position)
    {
        var particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
        if (particle == null)
            return;

        particle.EffectName = ExplosionEffect;
        particle.StartActive = true;
        particle.Teleport(position, new QAngle(0, 0, 0), new Vector(0, 0, 0));
        particle.DispatchSpawn();
        particle.AcceptInput("Start");

        // Give clients a frame to learn about the entity before a sound is played from it
        Server.NextFrame(() =>
        {
            if (particle.IsValid)
                particle.EmitSound(ExplosionSound);
        });

        Plugin.AddTimer(5f, () =>
        {
            if (particle.IsValid)
                particle.Remove();
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }
}
