# SiteLink.Bridge — game server plugin and release pipeline

Date: 2026-07-29
Status: Approved

## Problem

SiteLink ships a `net48` build of `SiteLink.API.dll` (containing only `SiteLinkBridge.cs`)
that a game server can load from `LabAPI/dependencies/global`. It exposes the connection,
messaging and target-server APIs, but nothing calls `SiteLinkBridge.Initialize`. Every server
owner has to write their own LabAPI plugin to do it.

Two consequences:

1. There is no released, ready-to-drop-in plugin. `release.yml` publishes only the proxy
   executables; the `net48` bridge assembly is never built in CI (and could not be — the
   reference download step does not fetch `mscorlib.dll` or `CommandSystem.Core.dll`).
2. Player counts reported to the SCP:SL central servers come from
   `Server.SessionsCount` — the number of sessions *this proxy* is holding. With two proxies
   in front of one game server, each reports its own slice, so neither number is correct.
   CSG 5.6 requires accurate data. Northwood's guidance is to report the game server's
   count, not the proxy's.

## Scope

- A new `SiteLink.Bridge` LabAPI plugin project in this repository.
- A player-count packet, consumed built-in by `SiteLink.API` (not delegated to a
  third-party proxy plugin).
- `SiteLinkBridge.TargetServers` filtered by `servers_in_selector`.
- `release.yml` producing `SiteLink.Bridge.dll` and `dependencies.zip` as release assets.

Out of scope: changing the proxy's session accounting, the selector UI, or the PreAuth
handshake.

## Architecture

```
Game server (net48)                        Proxy (net10.0)
------------------                         ---------------
SiteLink.Bridge.dll   (LabAPI plugin)
  └ SiteLinkBridge.Initialize(ip,port,key)
        │  LiteNetLib UDP, ClientType.Bridge + secret
        └──────────────────────────────────────► Listener → BridgeConnection
                                                     └ SiteLinkBridge.AttachServerPeer

  TargetServers  ◄── 17150 MsgTargetServersList ─── SendTargetServersList
                                                     (servers_in_selector filtered)

  PlayerCountReporter ── 17151 MsgPlayerCount ─────► Server.BridgePlayerCount
                                                     └ ScpServerListHandler
```

`SiteLink.API.dll` (net48) stays the only dependency; its LiteNetLib types resolve from the
game's `Assembly-CSharp.dll`, which embeds LiteNetLib. No extra dependency DLL is needed.

## Components

### SiteLink.Bridge (new, net48)

| File | Purpose |
|---|---|
| `SiteLink.Bridge.csproj` | net48, `Northwood.LabAPI 1.1.7`, `Microsoft.NETFramework.ReferenceAssemblies`, `ProjectReference` to `SiteLink.API` with `Private=false` so the dependency DLL is not duplicated next to the plugin |
| `BridgeConfig.cs` | `ip` (`127.0.0.1`), `port` (`7777`), `secret_key` (`---`), `debug` (`true`), `player_count_report_interval` (`5.0` seconds) |
| `SiteLinkBridgePlugin.cs` | `Plugin<BridgeConfig>`; `Enable()` calls `Initialize` and registers connected/disconnected handlers; `Disable()` unregisters |
| `PlayerCountReporter.cs` | Counts real players and pushes `MsgPlayerCount` |
| `BridgeStatusCommand.cs` | `.slbridge` — connection state, target endpoint, last reported count, raw/dummy counts, `TargetServers` |

**Player counting.** Follows the rules established in `PrometheusConnector`:

```
foreach (Player player in Player.List)
    if (player == null || player.IsHost) continue;   // host is not a player
    if (player.IsDummy) { dummies++; continue; }      // dummies are not players
    counted++;
```

Reported on a `MonoBehaviour` heartbeat (`InvokeRepeating`) at
`player_count_report_interval`, plus immediately when the count changes (join / leave /
round restart), debounced to one send per tick. Sending the count on a timer rather than
only on change means a bridge reconnect self-heals without extra handshake logic.

Max players comes from `CustomNetworkManager.slots`, matching what the game server itself
advertises.

### SiteLink.API changes

- `MsgPlayerCount = 17151` alongside the existing `MsgTargetServersList = 17150`.
- `SendTargetServersList` filters by `SiteLinkSettings.Singleton.ServersInSelector`,
  preserving selector order, matching case-insensitively.
- `Server` gains `BridgePlayerCount` (`-1` = unknown), `BridgeMaxPlayers`,
  `BridgePlayerCountUpdatedAt`, and `HasFreshBridgePlayerCount` (bridge attached **and**
  updated within 30 s).
- A built-in `MsgPlayerCount` handler registered in the `NET10_0` static constructor writes
  those fields; `DetachServerPeer` resets them.
- `ScpServerListHandler` prefers `HasFreshBridgePlayerCount` over `SessionsCount`, with a
  one-shot warning when it falls back.

The 30-second freshness window is deliberately longer than the 5-second report interval:
one lost UDP packet must not flip the source of truth. A bridge that dies silently degrades
to the old behaviour within 30 s instead of freezing a stale count forever.

### release.yml changes

- Add `mscorlib.dll` and `CommandSystem.Core.dll` to `filesToDownload`.
- Build `SiteLink.API` (net48) and `SiteLink.Bridge` (net48) with `-p:Version=$VERSION`.
- Package the net48 `SiteLink.API.dll` as `dependencies.zip` with the layout the user drops
  into `LabAPI/dependencies/global`.
- Attach `SiteLink.Bridge.dll` and `dependencies.zip` to the release, and upload both as
  workflow artifacts.

## Error handling

- `Initialize` is idempotent; a second call is a no-op.
- Handlers registered by the plugin are wrapped so a throwing handler cannot kill the
  dispatch loop (`SiteLinkBridge.Dispatch` already catches).
- `Send` no-ops when disconnected; the reporter does not queue, it simply reports the
  current count on the next tick after reconnect.
- Proxy-side, an unparseable `MsgPlayerCount` payload leaves the previous value untouched
  and does not mark it fresh.

## Verification

1. CI builds `SiteLink.Bridge.dll` and `dependencies.zip`.
2. Test proxy on `7800/udp`, test game server on `7801/udp`, both isolated from production.
3. `Bridge connected!` in proxy log, `IsConnected` true in `.slbridge`.
4. `.gsh` lists only servers present in `servers_in_selector`.
5. Spawn 5 dummies → `.slbridge` shows `raw=6, dummy=5, reported=0`; the proxy's
   `BridgePlayerCount` reads `0`.
6. Kill the bridge → after 30 s the proxy logs the fallback warning once and reverts to
   `SessionsCount`.

Known limitation: the headless test environment cannot connect a real game client, so
"N real players reported as N" is verified by code review and by the dummy-exclusion
observation, not by a live client.
