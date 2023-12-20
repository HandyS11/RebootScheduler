# Features

- Restart your server when you want (SET_TIME or COOLDOWN)
- Restart your server when an update is available (UpdateNotice plugin required)

# Dependencies

**THIS PLUGIN REQUIRE THE [DISCORD](https://umod.org/extensions/discord) EXTENTION TO WORK!**

Compatible with [Update Notice](https://umod.org/plugins/update-notice) for restart on update

- CarbonUpdate
- OxideUpdate
- ServerUpdate

# Permissions

- `rebootscheduler.admin` - Allows player to use the plugin commands

# Commands

- `rs cancel` - Cancel the ongoing restart
- `rs discord` - Send a test message to discord
- `rs help` Display the help message
- `rs restart <time in seconds>` Initiate a restart (10s if no time given)
- `rs status` Display the current restart status

# Configuration

Default configuration:

```json
{
  "Default chat avatar": 0,
  "Enable UpdateNotice plugin (required for hooks)": true,
  "Hooks configuration (require UpdateNotice)": {
    "When the Server Restart (COOLDOWN | DAILY_TIME)": "COOLDOWN",
    "Cooldown time before restart (in seconds)": 300,
    "Enable restart OnCarbonUpdate": false,
    "Enable restart OnOxideUpdate": true,
    "Enable restart OnServerUpdate": true
  },
  "Restart messages cooldown": [
    3600,
    1800,
    900,
    300,
    120,
    60,
    30,
    10,
    5,
    4,
    3,
    2,
    1
  ],
  "Enable daily restart": false,
  "Daily restart time (13:30:00 as example for 1:30 pm UTC)": "04:00:00",
  "Daily restart cooldown (for message visibility)": 300,
  "Enable discord notifications": false,
  "Discord webhook url": "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
  "Discord role id to mention (0 = no mention)": 0
}
```

- `Default chat avatar` - SteamID of the avatar to use for the chat messages
- `Enable UpdateNotice plugin (required for hooks)` - Enable the UpdateNotice plugin hooks
- `Hooks configuration (require UpdateNotice)` - Configuration for the UpdateNotice plugin hooks
  - `When the Server Restart (COOLDOWN | DAILY_TIME)` - When the server should restart (on hook triggered)
    - `COOLDOWN` - Restart when the cooldown time is reached
    - `DAILY_TIME` - Restart on the daily restart time (even if not activated)
  - `Cooldown time before restart (in seconds)` - Cooldown time before restart (only if the **COOLDOWN** is selected)
  - `Enable restart OnCarbonUpdate` - Initiate restart when CarbonUpdate is triggered
  - `Enable restart OnOxideUpdate` - Initiate restart when OxideUpdate is triggered
  - `Enable restart OnServerUpdate` - Initiate restart when ServerUpdate is triggered
- `Restart messages cooldown` - Cooldown time for chat messages (in seconds)
- `Enable daily restart` - Enable daily restart
- `Daily restart time (13:30:00 as example for 1:30 pm UTC)` - Time of the daily restart (in UTC)
- `Daily restart cooldown (for message visibility)` - Cooldown time for chat messages (in seconds)
- `Enable discord notifications` - Enable discord notifications *(restartCancelled, restartInitiated, restartIminent)*
- `Discord webhook url` - Discord webhook url (https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks)
- `Discord role id to mention (0 = no mention)` - Discord roleId for mention on message embed

# Localization

Default localization:

```json
{
  "Help": "\nCommands:\t\t\t\tExplanations:\n\n- rs cancel\t\t\t\tCancel the ongoing restart\n- rs discord\t\t\t\tSend a test message to discord\n- rs help\t\t\t\tDisplay the help message\n- rs restart <time in seconds>\t\tInitiate a restart (10s if no time given)\n- rs status\t\t\t\tDisplay the current restart status",
  "KickReason": "The server is restarting for update.",
  "NativeRestartCancel": "Native restart was cancelled.",
  "NoPermission": "You are not allowed to run this command!",
  "NoRestartOnGoing": "There is no restart on going!",
  "RestartCancelMessage": "The restart has been cancelled.",
  "RestartGlobalMessage": "The server is restarting in {0} due to {1}!",
  "RestartGlobalMessageShort": "The server is restarting in {0}!",
  "RestartInitialized": "Restart has been initialize.",
  "Status": "Status: {0}",
  "StatusWithTime": "Status: {0} - {1}",
  "UnknownCommand": "Unknown command!",
  "UpdateNoticeMissing": "The plugin \"UpdateMissing\" was not found. Check on UMod: https://umod.org/plugins/update-notice",
  "WrongNumberOfElements": "Wrong number of elements! Please check the help command.",
  "WrongTimeFormat": "Wrong time format! Please use \"hh:mm:ss\" for a planned time OR xxx (in seconds) for a cooldown"
}
```

**PLEASE MAKE SURE TO KEEP THE {x} PARAMETERS STARTING FROM 0 AND INCREASING WITH THE ASCENDING ORDER!!**

# Credits

Inspired from [SmoothRestarter](https://umod.org/plugins/smooth-restarter)

* **[HandyS11](https://github.com/HandyS11)** - Author