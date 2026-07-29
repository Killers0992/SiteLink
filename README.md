![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Killers0992/SiteLink/total?label=Downloads\&labelColor=2e343e\&color=00FFFF\&style=for-the-badge)
[![Discord](https://img.shields.io/discord/1434213646510325762?label=Discord\&labelColor=2e343e\&color=00FFFF\&style=for-the-badge)](https://discord.gg/Sva8TaCR7Q)
[![NuGet Version](https://img.shields.io/nuget/v/SiteLink.API?labelColor=2e343e\&color=00FFFF\&style=for-the-badge)](https://www.nuget.org/packages/SiteLink.API)



# SiteLink

**SiteLink** is a high-performance proxy for *SCP: Secret Laboratory*, inspired by BungeeCord.  

It connects multiple servers into one seamless network, enabling load balancing, player transfers, and centralized management — built for stability, speed, and scalability.

> **API Version:** `2.1.1`  
> **Supported Game Version:** `14.2.7`  

---

## Features

- 🚀 Multi-server networking
- 🔁 Seamless player transfers
- 🧱 Fallback failover
- 🧩 Plugin API
- 📡 Server-list publishing
- 🛡 IP forwarding (proxy passthrough)

---

# Installation

### **1. Prepare a machine capable of running .NET 10**
You can host SiteLink on:
- Dedicated server  
- VPS  
- Linux or Windows machine  

### **2. Download SiteLink**

> 🪟 **Windows**  
> [SiteLink.exe](https://github.com/Killers0992/SiteLink/releases/latest/download/SiteLink.exe)

> 🐧 **Linux**  
> [SiteLink](https://github.com/Killers0992/SiteLink/releases/latest/download/SiteLink)

### **3. Run SiteLink**

``SiteLink.exe`` or ``SiteLink``

### **4. Edit `settings.yml`**
Configure your network, servers, listeners, and server-list options.

---

## 🥚 Pterodactyl Egg

Hosting SiteLink on a [Pterodactyl](https://pterodactyl.io/) panel? Import the ready-to-use egg:

- [egg-site-link.json](https://github.com/Killers0992/SiteLink/blob/main/egg-site-link.json)

The egg automatically downloads the latest Linux build from releases and runs it on the `Dotnet 10` container image. After the first start, edit the generated `settings.yml` (set `listen_port` to match your allocation) and restart the server.

## Available Plugins

SiteLink can be extended with plugins placed inside the `Plugins` directory.  

| Plugin | Version / Requirement | Description |
|---|---|---|
| [SiteLink.Portals](https://github.com/Killers0992/SiteLink.Portals) | Requires **SiteLink `2.1.0+`** | Universal portal API for redirecting players between SiteLink servers. |
| [SiteLink.Lobby](https://github.com/Killers0992/SiteLink.Lobby) | Requires **SiteLink `2.1.0+`** and **Portals `1.1.0`** | Dedicated lobby world with interactive portals and floating texts. |
| [SiteLink.Queue](https://github.com/Killers0992/SiteLink.Queue) | Requires **SiteLink `2.1.0+`** | Automatic queue system for full target servers. |

---

# 🔧 Example Configuration (settings.yml)

```yml
player_limit: 100

listeners:
- name: main
  listen_address: 0.0.0.0
  listen_port: 7777
  game_version: latest
  priorities:
    - default

  server_list:
    show_server_on_server_list: false
    display_name: SiteLink
    pastebin: 7wV681fT
    email: your-email@gmail.com
    public_address: auto
    take_player_count_from_server: ''

servers:
- name: default
  display_name: <color=white>Default</color>
  address: 127.0.0.1
  port: 7778
  max_clients: 25
  forward_ip_address: false
  fallback_servers: []

servers_in_selector:
  - default

maximum_reconnect_attempts: 5
```

# IP Forwarding (Proxy Passthrough)

To forward the real player IP from SiteLink → SCP:SL backend servers, configure both the SCP:SL server and SiteLink.

## 1. SCP:SL server configuration (config_gameplay.txt)
### 1.1 Disable IP rate limiting

Find and change true -> false:
``enable_ip_ratelimit: false``

### 1.2 Enable proxy passthrough

Add or edit:

```yml
enable_proxy_ip_passthrough: true
trusted_proxies_ip_addresses:
  - <IP OF YOUR PROXY>
```

Example:

```yml
enable_proxy_ip_passthrough: true
trusted_proxies_ip_addresses:
  - 203.0.113.15
```


## Restart your SCP:SL server afterward.

### 2. SiteLink configuration (settings.yml)

In your backend server entry set:

``forward_ip_address: true``


Example:
```yml
servers:
  -
    name: default
    display_name: <color=white>Server</color>
    address: 127.0.0.1
    port: 7777
    max_clients: 25
    forward_ip_address: true
    fallback_servers: []
```

# How show SiteLink on SCP: SL serverlist

1. Put your verification token inside ``verkey.txt``.
2. Open ``settings.yml`` and modify your ``main`` listener,  set  ``show_server_on_server_list`` to ``true``, set your ``pastebin`` + ``email``
```yml
listeners:
-
  name: main

  server_list:
    # If true, the server will be visible on the public SCP:SL server list.
    show_server_on_server_list: false # <-- set to true

    # Pastebin ID used by SCP:SL for listing metadata or MOTD content.
    pastebin: 7wV681fT # <-- change default pastebin to your own

    # Private contact email for SCP:SL staff to reach the server owner if necessary (not shown publicly).
    email: your-email@gmail.com # <-- change email to your contact email
```
3. Restart ``SiteLink``, if everything  was properly set you should see in your console
``Server <ip>:<port> should be visible on serverlist!``

If server is still not visbile make sure to run central command:
- ``central main public`` ( it shows your main listener on serverlist )

# 🌉 SiteLink.Bridge (game server plugin)

`SiteLink.Bridge` is a LabAPI plugin that connects a SCP:SL game server back to the proxy.
It is optional, but without it the proxy has to guess your player count, and with more than
one proxy in front of the same game server that guess is wrong.

## Why you want it

Rule 5.6 of the CSGD requires the data reported to the central servers — including the
player count — to be accurate. A proxy only knows about the sessions it is holding itself.
Run two proxies and each one reports its own slice, so neither number matches reality.
The bridge makes the game server report its own count, and the proxy uses that instead.

Dummies and the host are never counted.

## Installation

1. Download `dependencies.zip` and `SiteLink.Bridge.dll` from the
   [releases](https://github.com/Killers0992/SiteLink/releases) page.
2. Extract `dependencies.zip` into `LabAPI/dependencies/global`
   (this is `SiteLink.API.dll`).
3. Drop `SiteLink.Bridge.dll` into `LabAPI/plugins/global` (or `LabAPI/plugins/<port>`).
4. Start the game server once to generate
   `LabAPI/configs/<port>/SiteLink.Bridge/config.yml`.

## Game server configuration

`LabAPI/configs/<port>/SiteLink.Bridge/config.yml`:

```yml
# Address of the SiteLink proxy this game server should connect to.
ip: 127.0.0.1

# Port of the SiteLink proxy this game server should connect to.
port: 7777

# Must match 'secret_key' under the server's bridge settings in the proxy config.
secret_key: '---'

# Print connection state changes and player count reports to the server console.
debug: true

# How often, in seconds, the current player count is reported to the proxy.
player_count_report_interval: 5

# Report the player count to the proxy.
report_player_count: true
```

## Proxy configuration

Enable the bridge on the matching server entry and use the same secret, then point the
listener's player count at that server:

```yml
servers:
-
  name: default
  ip: 127.0.0.1
  port: 7777

  bridge:
    enabled: true
    secret_key: '---'

listeners:
-
  name: main
  server_list:
    take_player_count_from_server: default
```

When the bridge is connected, the proxy reports the game server's count. If the bridge goes
away, the proxy warns once and falls back to its own session count after 30 seconds.

## Commands

| Command | Where | Description |
|---|---|---|
| `.gsh` | game server | Lists the target servers advertised by the proxy (the ones in `servers_in_selector`). |
| `.slbridge` | game server | Connection state, proxy endpoint, last reported count, raw/dummy counts and target servers. |

## Writing your own plugin against the bridge

`SiteLink.API.dll` is usable on its own if you would rather write your own plugin:

```csharp
SiteLinkBridge.Initialize("127.0.0.1", 7777, "---");

SiteLinkBridge.RegisterConnectedHandler(() => Logger.Info("Connected"));
SiteLinkBridge.RegisterDisconnectedHandler(info => Logger.Warn($"Lost: {info.Reason}"));

// Game server -> proxy
SiteLinkBridge.Send(1001, writer => writer.Put("hello"));

// Proxy -> game server (on the proxy side)
SiteLinkBridge.SendTo(server, 1001, writer => writer.Put("hello"));

// Both sides
SiteLinkBridge.RegisterHandler(1001, reader => { /* ... */ });
```

Message ids `17150` (target server list) and `17151` (player count) are reserved by
SiteLink itself.

> 🧱 *SiteLink — bridging SCP:SL servers into one connected network.*
