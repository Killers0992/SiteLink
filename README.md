![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Killers0992/SiteLink/total?label=Downloads\&labelColor=2e343e\&color=00FFFF\&style=for-the-badge)
[![Discord](https://img.shields.io/discord/1434213646510325762?label=Discord\&labelColor=2e343e\&color=00FFFF\&style=for-the-badge)](https://discord.gg/Sva8TaCR7Q)
![NuGet Version](https://img.shields.io/nuget/v/SiteLink.API?labelColor=2e343e\&color=00FFFF\&style=for-the-badge)



# SiteLink

**SiteLink** is a high-performance proxy for *SCP: Secret Laboratory*, inspired by BungeeCord.  

It connects multiple servers into one seamless network, enabling load balancing, player transfers, and centralized management — built for stability, speed, and scalability.

> **API Version:** `0.1.0`  
> **Supported Game Version:** `14.2.2`  

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

### **1. Prepare a machine capable of running .NET 9**
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

> 🧱 *SiteLink — bridging SCP:SL servers into one connected network.*
