using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace CTban;

[MinimumApiVersion(80)]
public class CTbanPlugin : BasePlugin
{
    public override string ModuleName => "CTban";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "Sm0ker";
    public override string ModuleDescription => "Allows admins to ban players from joining CT team";

    private ConcurrentDictionary<string, CTBanInfo> _bannedPlayers = new();
    private string _configPath = "";
    private PluginConfig _config = new();

    public override void Load(bool hotReload)
    {
        _configPath = Path.Combine(ModuleDirectory, "config.json");
        LoadConfig();
        LoadBannedPlayers();
        
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
        
        AddCommand("css_ctban", "Ban a player from CT team", CommandCTBan);
        AddCommand("css_ctunban", "Unban a player from CT team", CommandCTUnban);
        AddCommand("css_ctbanlist", "List all CT banned players", CommandCTBanList);
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _config = JsonSerializer.Deserialize<PluginConfig>(json) ?? new PluginConfig();
            }
            else
            {
                SaveConfig();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load config: {ex.Message}");
        }
    }

    private void SaveConfig()
    {
        try
        {
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save config: {ex.Message}");
        }
    }

    private void LoadBannedPlayers()
    {
        try
        {
            var bansPath = Path.Combine(ModuleDirectory, "ctbans.json");
            if (File.Exists(bansPath))
            {
                var json = File.ReadAllText(bansPath);
                var bans = JsonSerializer.Deserialize<Dictionary<string, CTBanInfo>>(json);
                if (bans != null)
                {
                    _bannedPlayers.Clear();
                    foreach (var kvp in bans)
                    {
                        _bannedPlayers[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load banned players: {ex.Message}");
        }
    }

    private void SaveBannedPlayers()
    {
        try
        {
            var bansPath = Path.Combine(ModuleDirectory, "ctbans.json");
            var bans = new Dictionary<string, CTBanInfo>();
            foreach (var kvp in _bannedPlayers)
            {
                bans[kvp.Key] = kvp.Value;
            }
            var json = JsonSerializer.Serialize(bans, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(bansPath, json);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save banned players: {ex.Message}");
        }
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        var steamId = player.SteamID.ToString();
        if (_bannedPlayers.TryGetValue(steamId, out var banInfo))
        {
            if (banInfo.ExpiresAt != null && DateTime.UtcNow > banInfo.ExpiresAt)
            {
                _bannedPlayers.TryRemove(steamId, out _);
                SaveBannedPlayers();
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        if ((int)@event.Team == (int)CsTeam.CounterTerrorist)
        {
            var steamId = player.SteamID.ToString();
            if (_bannedPlayers.TryGetValue(steamId, out var banInfo))
            {
                if (banInfo.ExpiresAt == null || DateTime.UtcNow <= banInfo.ExpiresAt)
                {
                    @event.Disconnect = true;
                    player.PrintToChat($" {ChatColors.Red}[NOLAG] {ChatColors.Red}You are banned from joining CT team!");
                    player.PrintToChat($" {ChatColors.Red}[NOLAG] {ChatColors.Yellow}Reason: {ChatColors.White}{banInfo.Reason}");
                    
                    if (banInfo.ExpiresAt.HasValue)
                    {
                        var remaining = banInfo.ExpiresAt.Value - DateTime.UtcNow;
                        player.PrintToChat($" {ChatColors.Red}[NOLAG] {ChatColors.Yellow}Time remaining: {ChatColors.White}{FormatTime(remaining)}");
                    }
                    else
                    {
                        player.PrintToChat($" {ChatColors.Red}[NOLAG] {ChatColors.Yellow}Duration: {ChatColors.White}Permanent");
                    }
                    
                    Server.NextFrame(() =>
                    {
                        player.ChangeTeam(CsTeam.Terrorist);
                    });
                    
                    return HookResult.Handled;
                }
                else
                {
                    _bannedPlayers.TryRemove(steamId, out _);
                    SaveBannedPlayers();
                }
            }
        }

        return HookResult.Continue;
    }

    public void CommandCTBan(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller != null && !AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            caller.PrintToChat($" {ChatColors.Red}[NOLAG] {ChatColors.Red}You don't have permission to use this command!");
            return;
        }

        if (command.ArgCount < 2)
        {
            ReplyToCommand(caller, $" {ChatColors.Green}[NOLAG] {ChatColors.Green}Usage: {ChatColors.White}css_ctban <#userid or name> [duration] [reason]");
            return;
        }

        var targetArg = command.GetArg(1);
        var target = FindPlayer(targetArg);
        
        if (target == null)
        {
            ReplyToCommand(caller, $" {ChatColors.Red}[NOLAG] {ChatColors.Red}Player not found!");
            return;
        }

        var steamId = target.SteamID.ToString();
        var duration = 0;
        var reason = "No reason specified";

        if (command.ArgCount >= 3)
        {
            if (!int.TryParse(command.GetArg(2), out duration))
            {
                duration = 0;
            }
        }

        if (command.ArgCount >= 4)
        {
            reason = command.GetArg(3);
            for (int i = 4; i <= command.ArgCount; i++)
            {
                reason += " " + command.GetArg(i);
            }
        }

        DateTime? expiresAt = null;
        if (duration > 0)
        {
            expiresAt = DateTime.UtcNow.AddMinutes(duration);
        }

        var banInfo = new CTBanInfo
        {
            SteamId = steamId,
            PlayerName = target.PlayerName,
            Reason = reason,
            BannedBy = caller?.PlayerName ?? "Console",
            BannedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        _bannedPlayers[steamId] = banInfo;
        SaveBannedPlayers();

        var durationText = duration > 0 ? $"{duration} minutes" : "permanent";
        ReplyToCommand(caller, $" {ChatColors.Red}[NOLAG] {ChatColors.Green}Player {ChatColors.White}{target.PlayerName} {ChatColors.Green}has been banned from CT for {ChatColors.White}{durationText}");
        ReplyToCommand(caller, $" {ChatColors.Red}[NOLAG] {ChatColors.Yellow}Reason: {ChatColors.White}{reason}");

        target.PrintToChat($" {ChatColors.Red}[NOLAG] {ChatColors.Red}You have been banned from joining CT team!");
        target.PrintToChat($" {ChatColors.Red}[NOLAG] {ChatColors.Yellow}Reason: {ChatColors.White}{reason}");
        target.PrintToChat($" {ChatColors.Red}[NOLAG] {ChatColors.Yellow}Duration: {ChatColors.White}{durationText}");
        target.PrintToChat($" {ChatColors.Red}[NOLAG] {ChatColors.Yellow}Banned by: {ChatColors.White}{banInfo.BannedBy}");

        if (target.Team == CsTeam.CounterTerrorist)
        {
            Server.NextFrame(() =>
            {
                target.ChangeTeam(CsTeam.Terrorist);
            });
        }
    }

    public void CommandCTUnban(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller != null && !AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            caller.PrintToChat($" {ChatColors.Red}[NOLAG] {ChatColors.Red}You don't have permission to use this command!");
            return;
        }

        if (command.ArgCount < 2)
        {
            ReplyToCommand(caller, $" {ChatColors.Green}[NOLAG] {ChatColors.Green}Usage: {ChatColors.White}css_ctunban <#userid or name>");
            return;
        }

        var targetArg = command.GetArg(1);
        var target = FindPlayer(targetArg);
        
        string steamId;
        string playerName;
        
        if (target != null)
        {
            steamId = target.SteamID.ToString();
            playerName = target.PlayerName;
        }
        else
        {
            steamId = targetArg;
            playerName = targetArg;
        }

        if (_bannedPlayers.TryRemove(steamId, out var banInfo))
        {
            SaveBannedPlayers();
            ReplyToCommand(caller, $" {ChatColors.Green}[NOLAG] {ChatColors.Green}Player {ChatColors.White}{banInfo.PlayerName} {ChatColors.Green}has been unbanned from CT team!");
        }
        else
        {
            ReplyToCommand(caller, $" {ChatColors.Red}[NOLAG] {ChatColors.Red}Player is not banned from CT team!");
        }
    }

    public void CommandCTBanList(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller != null && !AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            caller.PrintToChat($" {ChatColors.Red}[NOLAG] {ChatColors.Red}You don't have permission to use this command!");
            return;
        }

        if (_bannedPlayers.IsEmpty)
        {
            ReplyToCommand(caller, $" {ChatColors.Yellow}[NOLAG] {ChatColors.Yellow}No players are currently banned from CT team!");
            return;
        }

        ReplyToCommand(caller, $" {ChatColors.Green}[NOLAG] {ChatColors.Green}=== CT Ban List ===");
        foreach (var kvp in _bannedPlayers)
        {
            var ban = kvp.Value;
            var durationText = ban.ExpiresAt.HasValue ? $"Expires: {FormatTime(ban.ExpiresAt.Value - DateTime.UtcNow)}" : "Permanent";
            ReplyToCommand(caller, $" {ChatColors.White}{ban.PlayerName} {ChatColors.Yellow}| {ChatColors.Grey}{ban.SteamId} {ChatColors.Yellow}| {ChatColors.White}{durationText}");
            ReplyToCommand(caller, $" {ChatColors.Grey}  Reason: {ChatColors.White}{ban.Reason} {ChatColors.Grey}| Banned by: {ChatColors.White}{ban.BannedBy}");
        }
    }

    private CCSPlayerController? FindPlayer(string identifier)
    {
        if (identifier.StartsWith("#"))
        {
            var userId = int.Parse(identifier.Substring(1));
            return Utilities.GetPlayers().FirstOrDefault(p => p.UserId == userId);
        }
        else
        {
            return Utilities.GetPlayers().FirstOrDefault(p => 
                p.PlayerName?.Contains(identifier, StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    private void ReplyToCommand(CCSPlayerController? caller, string message)
    {
        if (caller == null)
        {
            Server.PrintToConsole(message);
        }
        else
        {
            caller.PrintToChat(message);
        }
    }

    private string FormatTime(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
        return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
    }
}

public class PluginConfig
{
    public string BanMessage { get; set; } = "You are banned from joining CT team!";
    public string DefaultReason { get; set; } = "CT Ban";
}

public class CTBanInfo
{
    public string SteamId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Reason { get; set; } = "";
    public string BannedBy { get; set; } = "";
    public DateTime BannedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
