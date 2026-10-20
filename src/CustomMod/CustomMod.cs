using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
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

    private ModifierEngine _engine = null!;

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
    }

    public override void Unload(bool hotReload)
    {
        _engine.Deactivate();
    }

    // The modifier stays active through the round-end delay and is swapped here,
    // when players respawn for the next round
    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _engine.Deactivate();
        if (IsWarmup())
            return HookResult.Continue;

        var modifier = _engine.PickRandom();
        if (modifier == null)
            return HookResult.Continue;

        _engine.Activate(modifier);
        if (_engine.Active == modifier)
            Announce(modifier);

        return HookResult.Continue;
    }

    [ConsoleCommand("css_modifier", "Apply a modifier now: css_modifier <id|off>. No argument lists them.")]
    [RequiresPermissions("@css/root")]
    public void OnModifierCommand(CCSPlayerController? player, CommandInfo command)
    {
        var arg = command.ArgCount > 1 ? command.GetArg(1) : "";

        if (arg == "")
        {
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

        var modifier = _engine.Find(arg);
        if (modifier == null)
        {
            command.ReplyToCommand($"Unknown modifier '{arg}'");
            return;
        }

        _engine.Activate(modifier);
        if (_engine.Active != modifier)
        {
            command.ReplyToCommand($"Failed to apply '{modifier.Id}', see server log");
            return;
        }

        Announce(modifier);
        command.ReplyToCommand($"Applied {modifier.Id}");
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

    private static CCSGameRules? GetGameRules() =>
        Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;

    private static bool IsWarmup() => GetGameRules()?.WarmupPeriod ?? false;
}
