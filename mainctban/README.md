# CT Ban Plugin for Counter-Strike 2

<p align="center">
  <img src="https://raw.githubusercontent.com/roflmuffin/CounterStrikeSharp/master/docs/images/logo.png" alt="CounterStrikeSharp Logo" width="200"/>
</p>

A Counter-Strike 2 plugin that allows administrators to ban players from joining the Counter-Terrorist (CT) team only.

## Features

- **CT Team Banning**: Ban players from joining CT team while still allowing them to play as Terrorists
- **Admin Permission System**: Requires `@css/generic` permission to use commands
- **Temporary & Permanent Bans**: Set time-limited bans or permanent bans
- **Automatic Team Switching**: Automatically moves banned players to Terrorist team if they try to join CT
- **Persistent Storage**: Bans are saved to JSON files and persist across server restarts
- **Ban Management**: List, ban, and unban players with detailed information

## Commands

### `css_ctban <#userid or name> [duration] [reason]`
Ban a player from joining CT team.

**Parameters:**
- `#userid or name`: Player's user ID (with # prefix) or partial name
- `duration`: Optional. Ban duration in minutes. 0 or omitted = permanent ban
- `reason`: Optional. Reason for the ban

**Examples:**
```
css_ctban #123 60 Team killing
css_ctban PlayerName 0 Permanent ban
css_ctban #456 30 Breaking rules
```

### `css_ctunban <#userid or name>`
Unban a player from CT team.

**Parameters:**
- `#userid or name`: Player's user ID (with # prefix), name, or Steam ID

**Examples:**
```
css_ctunban #123
css_ctunban PlayerName
```

### `css_ctbanlist`
List all currently CT-banned players with details.

## Installation

1. Compile the plugin using the CounterStrikeSharp.API framework
2. Place the compiled DLL in your server's `addons/counterstrikesharp/plugins` directory
3. Restart your server or reload plugins

## Configuration

The plugin creates configuration files in the plugin directory:

- `config.json`: Plugin configuration
- `ctbans.json`: Stored ban data

### Default Configuration
```json
{
  "BanMessage": "You are banned from joining CT team!",
  "DefaultReason": "CT Ban"
}
```

## Permissions

Users need the `@css/generic` permission to use CT ban commands. This can be configured in your server's admin configuration file.

## How It Works

1. When a banned player tries to join CT team, the plugin intercepts the event
2. The player is prevented from joining CT and automatically moved to Terrorist team
3. The player receives notification messages about their ban status
4. Temporary bans automatically expire when the time limit is reached
5. All bans are persisted across server restarts

## Requirements

- CounterStrikeSharp
- .NET 8.0 runtime

## Support

For issues or feature requests, please contact me on Discord : bismilahsm0ker
