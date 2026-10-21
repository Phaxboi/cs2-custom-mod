using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod.Modifiers;

/// <summary>
/// One secret traitor per team (teams of 3+). Friendly fire is on. Kills between a traitor and a
/// teammate carry no penalty either way, and a traitor's team kills are hidden from the kill feed
/// and shown in chat as "T-Traitor ➜ victim" instead. The traitors are revealed at round end.
/// </summary>
public class Sabotage : ConVarModifier
{
    private const int MinTeamSize = 3;

    public override string Name => "Sabotage";
    public override string Description => "There is a traitor on each team...";
    public override string Color => "#d500f9";
    public override char ChatColor => ChatColors.Magenta;

    protected override IReadOnlyDictionary<string, string> ConVars { get; } = new Dictionary<string, string>
    {
        ["mp_friendlyfire"] = "1",
        // Full damage to teammates, so the traitor (and suspicious teammates) can actually kill
        ["ff_damage_reduction_bullets"] = "1",
        ["ff_damage_reduction_grenade"] = "1",
        ["ff_damage_reduction_other"] = "1",
        ["mp_autokick"] = "0", // no kicks for team kills or team damage this round
        // Hide names, so it is harder to tell who is who
        ["mp_playerid"] = "2", // no name when aiming at someone
        ["sv_teamid_overhead"] = "0", // no teammate names/markers above heads
        ["sv_teamid_overhead_always_prohibit"] = "1",
        ["sv_show_team_equipment_prohibit"] = "1", // no teammate equipment overlay either
        ["sv_disable_radar"] = "1", // the radar shows each teammate in their own color
    };

    // Player slot of each team's traitor
    private readonly Dictionary<CsTeam, int> _traitors = new();

    // Attacker's money and score before a traitor team kill, restored after the game's penalty
    private readonly Dictionary<int, (int Money, int Score)> _beforePenalty = new();

    private readonly BasePlugin.GameEventHandler<EventPlayerDeath> _onPlayerDeath;
    private readonly BasePlugin.GameEventHandler<EventRoundFreezeEnd> _onFreezeEnd;
    private readonly BasePlugin.GameEventHandler<EventRoundEnd> _onRoundEnd;
    private readonly CommandInfo.CommandCallback _onTraitorCommand;

    public Sabotage()
    {
        _onPlayerDeath = OnPlayerDeath;
        _onFreezeEnd = OnFreezeEnd;
        _onRoundEnd = OnRoundEnd;
        _onTraitorCommand = OnTraitorCommand;
    }

    public override void Apply()
    {
        base.Apply();
        _traitors.Clear();

        foreach (var team in new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist })
        {
            var members = TeamMembers(team).ToList();
            if (members.Count >= MinTeamSize)
                _traitors[team] = members[Random.Shared.Next(members.Count)].Slot;
        }

        Plugin.RegisterEventHandler(_onPlayerDeath, HookMode.Pre);
        Plugin.RegisterEventHandler(_onFreezeEnd);
        Plugin.RegisterEventHandler(_onRoundEnd);
        Plugin.AddCommand("css_traitor", "Testing: make yourself your team's traitor this round", _onTraitorCommand);

        // After the round announcement, so it is not missed in the respawn
        Plugin.AddTimer(1f, TellTraitors, TimerFlags.STOP_ON_MAPCHANGE);
    }

    public override void Revert()
    {
        base.Revert();
        Plugin.DeregisterEventHandler(_onPlayerDeath, HookMode.Pre);
        Plugin.DeregisterEventHandler(_onFreezeEnd);
        Plugin.DeregisterEventHandler(_onRoundEnd);
        Plugin.RemoveCommand("css_traitor", _onTraitorCommand);
        _traitors.Clear();
        _beforePenalty.Clear();
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        var attacker = @event.Attacker;
        if (victim is not { IsValid: true } || attacker is not { IsValid: true } || attacker == victim ||
            attacker.Team != victim.Team)
            return HookResult.Continue;

        var attackerIsTraitor = IsTraitor(attacker);
        if (!attackerIsTraitor && !IsTraitor(victim))
            return HookResult.Continue; // ordinary team kill: normal penalty

        // No penalty for kills between a traitor and a teammate: the game applies it right after
        // this event, so undo it next frame
        var slot = attacker.Slot;
        _beforePenalty[slot] = (attacker.InGameMoneyServices?.Account ?? 0, attacker.Score);
        Server.NextFrame(() => RestoreAfterPenalty(attacker, slot));

        if (attackerIsTraitor)
        {
            // Hide who did it: no kill feed entry, a masked line in chat instead
            info.DontBroadcast = true;
            var label = victim.Team == CsTeam.Terrorist ? "T-Traitor" : "CT-Traitor";
            Server.PrintToChatAll($" {ChatColors.Magenta}{label}{ChatColors.Default} ➜ {victim.PlayerName}");
        }

        return HookResult.Continue;
    }

    private void RestoreAfterPenalty(CCSPlayerController attacker, int slot)
    {
        if (!_beforePenalty.Remove(slot, out var before) || !attacker.IsValid)
            return;

        var money = attacker.InGameMoneyServices;
        if (money != null && money.Account < before.Money)
        {
            money.Account = before.Money;
            Utilities.SetStateChanged(attacker, "CCSPlayerController", "m_pInGameMoneyServices");
        }

        if (attacker.Score < before.Score)
        {
            attacker.Score = before.Score;
            Utilities.SetStateChanged(attacker, "CCSPlayerController", "m_iScore");
        }
    }

    private HookResult OnFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
    {
        TellTraitors(); // once more as the round goes live
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        Server.PrintToChatAll($" {ChatColors.Magenta}[Sabotage]{ChatColors.Default} The traitors were:");
        foreach (var (team, slot) in _traitors)
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            var label = team == CsTeam.Terrorist ? "T" : "CT";
            Server.PrintToChatAll($" {ChatColors.Magenta}{label}:{ChatColors.Default} {player?.PlayerName ?? "(left)"}");
        }
        return HookResult.Continue;
    }

    private void OnTraitorCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || player.Team is not (CsTeam.Terrorist or CsTeam.CounterTerrorist))
            return;

        _traitors[player.Team] = player.Slot;
        TellTraitor(player);
    }

    private void TellTraitors()
    {
        foreach (var slot in _traitors.Values)
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is { IsValid: true, IsBot: false })
                TellTraitor(player);
        }
    }

    private static void TellTraitor(CCSPlayerController player)
    {
        player.PrintToChat($" {ChatColors.Magenta}[Sabotage]{ChatColors.Red} You are the traitor!{ChatColors.Default} " +
                           "Kill your teammates without penalty, and don't get caught.");
        player.PrintToCenterAlert("You are the TRAITOR");
    }

    private bool IsTraitor(CCSPlayerController player) =>
        _traitors.TryGetValue(player.Team, out var slot) && slot == player.Slot;

    private static IEnumerable<CCSPlayerController> TeamMembers(CsTeam team) =>
        Utilities.GetPlayers().Where(p => p is { IsValid: true } && p.Team == team);
}
