using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace CustomMod.Modifiers;

/// <summary>
/// Everyone leaves a trail of fire that burns anyone standing in it, including its owner once it
/// is a moment old, so standing still hurts. Damage is dealt through point_hurt with the trail's
/// owner as attacker, so trail kills are credited to them.
/// </summary>
public class FireTrails : Modifier
{
    private const float TickSeconds = 0.25f;
    private const int Damage = 5; // per tick
    private const float TrailSeconds = 5f; // how long a trail point burns
    private const float OwnerSafeSeconds = 1f; // your own fresh trail does not hurt you yet
    private const float DropDistance = 64f; // a new point every this many units moved
    private const float FlameSpread = 28f; // side flames this far left and right of the path
    private const float BurnRadius = 60f; // horizontal distance from a point that burns
    private const float BurnHeight = 48f; // vertical distance from a point that burns
    private const string FlameEffect = "particles/inferno_fx/molotov_fire01.vpcf";

    public override string Name => "Fire Trails";
    public override string Description => "You leave fire behind you. Keep moving!";
    public override string Color => "#ff6d00";
    public override char ChatColor => ChatColors.Orange;
    public override IEnumerable<string> Resources => [FlameEffect];

    private sealed record TrailPoint(Vector Position, CCSPlayerPawn Owner, double Created, List<CParticleSystem> Flames);

    private readonly List<TrailPoint> _points = new();
    private readonly Dictionary<uint, Vector> _lastDrop = new(); // pawn index -> last point position
    private Timer? _timer;
    private CPointHurt? _hurt;

    public override void Apply()
    {
        _timer = Plugin.AddTimer(TickSeconds, Tick, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    public override void Revert()
    {
        _timer?.Kill();
        _timer = null;

        foreach (var point in _points)
            RemoveFlames(point);
        _points.Clear();
        _lastDrop.Clear();

        RemoveEntity(_hurt);
        _hurt = null;
    }

    private void Tick()
    {
        // Freeze time: nobody can move, so no trail yet (and it would burn everyone at go)
        if (GameRules()?.FreezePeriod ?? false)
            return;

        var now = Server.CurrentTime;

        // Expire old points
        for (var i = _points.Count - 1; i >= 0; i--)
        {
            if (now - _points[i].Created < TrailSeconds)
                continue;
            RemoveFlames(_points[i]);
            _points.RemoveAt(i);
        }

        var pawns = Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, PawnIsAlive: true })
            .Select(p => p.PlayerPawn.Value)
            .Where(p => p?.AbsOrigin != null)
            .Cast<CCSPlayerPawn>()
            .ToList();

        foreach (var pawn in pawns)
        {
            Burn(pawn, now);
            DropPoint(pawn, now);
        }
    }

    private void DropPoint(CCSPlayerPawn pawn, double now)
    {
        var origin = pawn.AbsOrigin!;
        if (_lastDrop.TryGetValue(pawn.Index, out var last) && Distance2D(origin, last) < DropDistance)
            return;

        var position = new Vector(origin.X, origin.Y, origin.Z);

        // Three flames side by side, across the direction the player moved
        var flames = new List<CParticleSystem>();
        var (sideX, sideY) = (0f, 0f);
        if (last != null && Distance2D(position, last) > 0)
        {
            var length = Distance2D(position, last);
            (sideX, sideY) = (-(position.Y - last.Y) / length, (position.X - last.X) / length);
        }
        foreach (var offset in new[] { 0f, FlameSpread, -FlameSpread })
        {
            if (offset != 0 && sideX == 0 && sideY == 0)
                continue; // first point: no direction yet, just the center flame
            var flame = SpawnFlame(new Vector(position.X + sideX * offset, position.Y + sideY * offset, position.Z));
            if (flame != null)
                flames.Add(flame);
        }

        _lastDrop[pawn.Index] = position;
        _points.Add(new TrailPoint(position, pawn, now, flames));
    }

    private static void RemoveFlames(TrailPoint point)
    {
        foreach (var flame in point.Flames)
            RemoveEntity(flame);
    }

    private void Burn(CCSPlayerPawn victim, double now)
    {
        var origin = victim.AbsOrigin!;
        var point = _points.FirstOrDefault(p =>
            (p.Owner != victim || now - p.Created >= OwnerSafeSeconds) &&
            Distance2D(origin, p.Position) <= BurnRadius &&
            Math.Abs(origin.Z - p.Position.Z) <= BurnHeight);
        if (point == null)
            return;

        _hurt ??= CreateHurt();
        if (_hurt == null)
            return;

        // point_hurt damages everything within its radius, crediting the activator
        _hurt.Teleport(new Vector(origin.X, origin.Y, origin.Z + 36), new QAngle(0, 0, 0), new Vector(0, 0, 0));
        _hurt.AcceptInput("Hurt", point.Owner.IsValid ? point.Owner : null);
    }

    private static CPointHurt? CreateHurt()
    {
        var hurt = Utilities.CreateEntityByName<CPointHurt>("point_hurt");
        if (hurt == null)
            return null;

        hurt.Damage = Damage;
        hurt.BitsDamageType = DamageTypes_t.DMG_BURN;
        hurt.Radius = 32f;
        hurt.DispatchSpawn();
        return hurt;
    }

    private static CParticleSystem? SpawnFlame(Vector position)
    {
        var flame = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
        if (flame == null)
            return null;

        flame.EffectName = FlameEffect;
        flame.StartActive = true;
        flame.Teleport(position, new QAngle(0, 0, 0), new Vector(0, 0, 0));
        flame.DispatchSpawn();
        flame.AcceptInput("Start");
        return flame;
    }

    private static CCSGameRules? GameRules() =>
        Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;

    private static void RemoveEntity(CEntityInstance? entity)
    {
        if (entity is { IsValid: true })
            entity.Remove();
    }

    private static float Distance2D(Vector a, Vector b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
