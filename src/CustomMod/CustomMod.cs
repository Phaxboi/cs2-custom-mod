using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using CustomMod.Modifiers;

namespace CustomMod;

public class CustomModPlugin : BasePlugin
{
    public override string ModuleName => "CustomMod";
    public override string ModuleVersion => "0.1.0";
    public override string ModuleAuthor => "Phaxboi";
    public override string ModuleDescription => "Per-round randomizer: a random modifier each round";

    private const int AnnounceMilliseconds = 5000;

    private const int MenuMilliseconds = 20000;

    private ModifierEngine _engine = null!;

    // Set by "!modifier next <id>": used instead of a random pick at the next round
    private Modifier? _next;

    // Player slot -> until when their !modifier list accepts a number
    private readonly Dictionary<int, long> _menuOpenUntil = new();

    // CS2 clears center HTML almost immediately (and on respawn), so it is re-sent every tick
    private string? _centerHtml;
    private long _centerHtmlUntil;

    public override void Load(bool hotReload)
    {
        _engine = new ModifierEngine(this, Logger);
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            _engine.Deactivate();
            _centerHtml = null;
        });
        RegisterListener<Listeners.OnTick>(OnTick);
        AddCommandListener("say", OnSay);
        AddCommandListener("say_team", OnSay);
        RegisterListener<Listeners.OnServerPrecacheResources>(manifest =>
        {
            foreach (var resource in _engine.Modifiers.SelectMany(m => m.Resources))
                manifest.AddResource(resource);
        });
    }

    public override void Unload(bool hotReload)
    {
        _engine.Deactivate();
    }

    // The modifier stays active through the round-end delay and is swapped here, just before
    // players respawn for the next round. Prestart rather than round_start, because some settings
    // (like sv_buy_status_override) are only read while the game sets up the new round.
    [GameEventHandler]
    public HookResult OnRoundPrestart(EventRoundPrestart @event, GameEventInfo info)
    {
        _engine.Deactivate();
        if (IsWarmup())
            return HookResult.Continue;

        var modifier = _next ?? _engine.PickRandom();
        _next = null;
        if (modifier == null)
            return HookResult.Continue;

        _engine.Activate(modifier);
        if (_engine.Active == modifier)
            Announce(modifier);

        return HookResult.Continue;
    }

    // Open to everyone on purpose, like css_skip
    [ConsoleCommand("css_modifier", "Apply a modifier now: css_modifier <id|off>, or next round: css_modifier next <id>. No argument opens a menu (console: lists them).")]
    public void OnModifierCommand(CCSPlayerController? player, CommandInfo command)
    {
        var arg = command.ArgCount > 1 ? command.GetArg(1) : "";

        if (arg == "")
        {
            if (player != null)
            {
                OpenModifierMenu(player);
                return;
            }

            command.ReplyToCommand($"Active: {_engine.Active?.Id ?? "none"}");
            foreach (var m in _engine.Modifiers)
                command.ReplyToCommand($"  {m.Id} - {m.Name}: {m.Description}");
            return;
        }

        if (arg.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            _engine.Deactivate();
            command.ReplyToCommand("Modifier off");
            return;
        }

        if (arg.Equals("next", StringComparison.OrdinalIgnoreCase))
        {
            var id = command.ArgCount > 2 ? command.GetArg(2) : "";
            _next = _engine.Find(id);
            command.ReplyToCommand(_next == null ? $"Unknown modifier '{id}'" : $"{_next.Id} starts next round");
            return;
        }

        var modifier = _engine.Find(arg);
        if (modifier == null)
        {
            command.ReplyToCommand($"Unknown modifier '{arg}'");
            return;
        }

        command.ReplyToCommand(ApplyModifier(modifier));
    }

    /// <summary>
    /// Prints a numbered list in chat; the player then picks with !0, !1, ... (see OnSay).
    /// Not CounterStrikeSharp's menus: those number from !1 and own css_1 to css_9.
    /// </summary>
    private void OpenModifierMenu(CCSPlayerController player)
    {
        player.PrintToChat($" {ChatColors.Grey}[Modifier]{ChatColors.Default} Active: {_engine.Active?.Name ?? "none"}. Type the number to pick:");
        for (var i = 0; i < _engine.Modifiers.Count; i++)
        {
            var m = _engine.Modifiers[i];
            player.PrintToChat($" {ChatColors.Gold}!{i}{ChatColors.Default} {m.ChatColor}{m.Name}{ChatColors.Default} - {m.Description}");
        }
        player.PrintToChat($" {ChatColors.Gold}!{_engine.Modifiers.Count}{ChatColors.Default} Off");

        _menuOpenUntil[player.Slot] = Environment.TickCount64 + MenuMilliseconds;
    }

    private HookResult OnSay(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !_menuOpenUntil.TryGetValue(player.Slot, out var until))
            return HookResult.Continue;

        if (Environment.TickCount64 > until)
        {
            _menuOpenUntil.Remove(player.Slot);
            return HookResult.Continue;
        }

        var message = info.GetArg(1).Trim();
        if (message.Length < 2 || message[0] is not ('!' or '/') || !int.TryParse(message[1..], out var choice))
            return HookResult.Continue;

        if (choice < 0 || choice > _engine.Modifiers.Count)
            return HookResult.Continue;

        _menuOpenUntil.Remove(player.Slot);
        if (choice == _engine.Modifiers.Count)
        {
            _engine.Deactivate();
            player.PrintToChat("Modifier off");
        }
        else
        {
            player.PrintToChat(ApplyModifier(_engine.Modifiers[choice]));
        }

        // Hide the "!0" from chat
        return HookResult.Handled;
    }

    /// <summary>Applies and announces the modifier; returns a reply for whoever asked.</summary>
    private string ApplyModifier(Modifier modifier)
    {
        _engine.Activate(modifier);
        if (_engine.Active != modifier)
            return $"Failed to apply '{modifier.Id}', see server log";

        Announce(modifier);
        return $"Applied {modifier.Id}";
    }

    private void Announce(Modifier modifier)
    {
        Server.PrintToChatAll(
            $" {ChatColors.Grey}[Modifier]{ChatColors.Default} {modifier.ChatColor}{modifier.Name}{ChatColors.Default}: {modifier.Description}");

        var icon = modifier.IconUrl == null ? "" : $"<img src='{modifier.IconUrl}'><br>";
        _centerHtml = icon +
                      $"<font class='fontSize-xl' color='{modifier.Color}'>{modifier.Name}</font><br>" +
                      $"<font class='fontSize-m'>{modifier.Description}</font>";
        // Wall-clock time, so the duration is not shortened by host_timescale
        _centerHtmlUntil = Environment.TickCount64 + AnnounceMilliseconds;
    }

    private void OnTick()
    {
        if (_centerHtml == null)
            return;

        if (Environment.TickCount64 > _centerHtmlUntil)
        {
            _centerHtml = null;
            return;
        }

        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false }))
            player.PrintToCenterHtml(_centerHtml, 1);
    }

    // Open to everyone on purpose: this is a just-for-fun server
    [ConsoleCommand("css_skip", "Skip to the next round (ends warmup during warmup)")]
    public void OnSkipCommand(CCSPlayerController? player, CommandInfo command)
    {
        var gameRules = GetGameRules();
        if (gameRules == null)
            return;

        var who = player?.PlayerName ?? "Console";
        if (gameRules.WarmupPeriod)
        {
            Server.ExecuteCommand("mp_warmup_end");
            Server.PrintToChatAll($" {ChatColors.Grey}[Skip]{ChatColors.Default} {who} ended warmup");
            return;
        }

        gameRules.TerminateRound(1f, RoundEndReason.RoundDraw);
        Server.PrintToChatAll($" {ChatColors.Grey}[Skip]{ChatColors.Default} {who} skipped the round");
    }

    // Testing helper, open to everyone like css_skip. bot_stop is cheat-protected, so it is set
    // directly (the same way ConVarModifier does) instead of through sv_cheats.
    [ConsoleCommand("css_freezebots", "Toggle bots standing still (for testing)")]
    public void OnFreezeBotsCommand(CCSPlayerController? player, CommandInfo command)
    {
        var botStop = ConVar.Find("bot_stop");
        if (botStop == null)
            return;

        var frozen = NativeAPI.GetConvarValueAsString(botStop.AccessIndex) != "0";
        NativeAPI.SetConvarValueAsString(botStop.AccessIndex, frozen ? "0" : "1");

        var who = player?.PlayerName ?? "Console";
        Server.PrintToChatAll($" {ChatColors.Grey}[Bots]{ChatColors.Default} {who} {(frozen ? "unfroze" : "froze")} the bots");
    }

    private static CCSGameRules? GetGameRules() =>
        Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;

    private static bool IsWarmup() => GetGameRules()?.WarmupPeriod ?? false;
}
