using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;

namespace SiteLink.API.Handlers;

/// <summary>
/// API-style handler for managing SCP:SL server list communication.
/// </summary>
public class ScpServerListHandler
{
    private readonly HttpClient _client;
    private readonly CancellationToken _cancellationToken;

    public static string Password { get; private set; }
    public static AsymmetricKeyParameter PublicKey { get; private set; }

    private string _verKey;
    private string _cachedHash = string.Empty;
    private string _lastKeyHash = string.Empty;

    private bool _initialized;
    private bool _scheduleTokenRefresh;
    private bool _verifyNotice;
    private byte _cycle;

    public ScpServerListHandler(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("User-Agent", "SCP SL");
        _client.DefaultRequestHeaders.Add("Game-Version", SiteLinkAPI.GameVersion);
    }

    public async Task InitializeAsync()
    {
        await RefreshTokenAsync(true);
        await LoadPublicKeyAsync();

        _initialized = true;
    }

    public async Task RefreshTokenAsync(bool init = false)
    {
        _scheduleTokenRefresh = false;

        if (!File.Exists("verkey.txt"))
            await File.WriteAllTextAsync("verkey.txt", "none", _cancellationToken);

        _verKey ??= await File.ReadAllTextAsync("verkey.txt", _cancellationToken);

        if (string.IsNullOrEmpty(_verKey))
            return;

        if (Password != _verKey)
        {
            foreach (var listener in Listener.List)
                listener.ForceServerListUpdate = true;
        }

        Password = _verKey;
    }

    public async Task LoadPublicKeyAsync()
    {
        try
        {
            string cached = CentralServerKeyCache.ReadCache();

            if (!string.IsNullOrEmpty(cached))
            {
                PublicKey = ECDSA.PublicKeyFromString(cached);
                _cachedHash = Sha.HashToString(Sha.Sha256(ECDSA.KeyToString(PublicKey)));
            }

            await RefreshPublicKeyAsync();
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error(ex, nameof(ScpServerListHandler));
        }
    }

    public async Task RefreshPublicKeyAsync()
    {
        try
        {
            HttpResponseMessage response = await _client.GetAsync("https://api.scpslgame.com/v4/publickey.php?major=14", _cancellationToken);
            string responseText = await response.Content.ReadAsStringAsync(_cancellationToken);

            PublicKey publicKeyResponse = JsonConvert.DeserializeObject<PublicKey>(responseText);

            if (!ECDSA.Verify(publicKeyResponse.Key, publicKeyResponse.Signature, ScpCentralServer.MasterKey))
            {
                SiteLinkLogger.Error("Public key signature invalid.", nameof(ScpServerListHandler));
                return;
            }

            PublicKey = ECDSA.PublicKeyFromString(publicKeyResponse.Key);
            string hashKey = Sha.HashToString(Sha.Sha256(ECDSA.KeyToString(PublicKey)));

            if (hashKey != _lastKeyHash)
            {
                _lastKeyHash = hashKey;

                if (hashKey != _cachedHash)
                {
                    ScpCentralServer.SaveCache(publicKeyResponse.Key, publicKeyResponse.Signature);
                    SiteLinkLogger.Info("Central server public key refreshed.", nameof(ScpServerListHandler));
                }
            }
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error(ex, nameof(ScpServerListHandler));
        }
    }

    private async Task SaveNewTokenAsync(string token)
    {
        try
        {
            _verKey = token;
            await File.WriteAllTextAsync("verkey.txt", token, _cancellationToken);

            SiteLinkLogger.Info("Token saved", "ScpServerList");

            foreach (var listener in Listener.List)
                listener.ForceServerListUpdate = true;

            _scheduleTokenRefresh = true;
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error($"Token failed to save: {ex}", "ScpServerList");
        }
    }

    /// <summary>
    /// Performs a full update cycle for all active listeners.
    /// </summary>
    public async Task RefreshAsync()
    {
        try
        {
            await DoCycleAsync();
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error(ex, "ScpServerList");
        }

        if (_scheduleTokenRefresh || _cycle == 0)
            await RefreshTokenAsync();
    }

    private async Task DoCycleAsync()
    {
        _cycle++;

        foreach (var listener in Listener.List)
            listener.ServerListCycle++;

        if (!_initialized && string.IsNullOrEmpty(Password) && _cycle < 15)
        {
            if (_cycle is 5 or 12 || _scheduleTokenRefresh)
                await RefreshTokenAsync();
            return;
        }

        _initialized = true;

        foreach (var listener in Listener.List)
        {
            if (!listener.Settings.ServerList.ShowServerOnServerList)
                continue;

            if (string.IsNullOrEmpty(listener.PublicAddress))
                await listener.Initialize();

            listener.ServerListUpdate = listener.ForceServerListUpdate || listener.ServerListCycle == 10;

            string playersStr = $"{listener.ClientById.Values.Count}/{SiteLinkSettings.Singleton.PlayerLimit}";

            if (string.IsNullOrEmpty(listener.Settings.ServerList.TakePlayerCountFromServer))
            {
                Server targetServer = Server.Get<Server>(name: listener.Settings.ServerList.TakePlayerCountFromServer);

                if (targetServer == null)
                {
                    SiteLinkLogger.Warn($"{listener.Tag} Failed to bind player count from server '{listener.Settings.ServerList.TakePlayerCountFromServer}' because server does not exist!");
                }
                else
                {
                    playersStr = $"{targetServer.ClientsCount}/{targetServer.MaxClientsCount}";
                }
            }

            var updateData = BuildUpdateData(listener, playersStr);

            bool result = await SendDataAsync(listener, updateData);

            if (result && !_verifyNotice)
            {
                SiteLinkLogger.Info($"{listener.Tag} Server {listener.PublicAddress}:{listener.ListenPort} should be visible on serverlist!");
                _verifyNotice = true;
            }

            listener.ServerListUpdate = false;
        }

        if (_cycle >= 15)
            _cycle = 0;

        foreach (var server in Listener.List)
        {
            if (server.ServerListCycle >= 15)
                server.ServerListCycle = 0;
        }
    }

    private async Task<bool> SendDataAsync(Listener server, Dictionary<string, string> data)
    {
        var content = new FormUrlEncodedContent(data);

        try
        {
            using var response = await server.Http.PostAsync("https://api.scpslgame.com/v4/authenticator.php", content, _cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(_cancellationToken);

            return responseText.StartsWith("{\"")
                ? await ProcessResponseAsync(server, responseText)
                : await ProcessLegacyResponseAsync(server, responseText);
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error(ex, "ScpServerList");
            return false;
        }
    }

    private async Task<bool> ProcessResponseAsync(Listener server, string data)
    {
        AuthResponseModel authResponse = JsonConvert.DeserializeObject<AuthResponseModel>(data);

        if (!string.IsNullOrEmpty(authResponse.VerificationChallenge))
            SiteLinkLogger.Info("Verification challenge obtained");

        if (!authResponse.Success)
        {
            SiteLinkLogger.Error($"Failed to update: {authResponse.Error}");
            return false;
        }

        if (!string.IsNullOrEmpty(authResponse.Token))
        {
            SiteLinkLogger.Info("Received token");
            await SaveNewTokenAsync(authResponse.Token);
        }

        if (authResponse.Actions != null)
        {
            foreach (var action in authResponse.Actions)
                await HandleActionAsync(server, action);
        }

        if (authResponse.Messages != null)
        {
            foreach (var message in authResponse.Messages)
                SiteLinkLogger.Info($"Message from central server: {message}", "ScpServerList");
        }

        return authResponse.Verified;
    }

    private async Task<bool> ProcessLegacyResponseAsync(Listener server, string response)
    {
        if (response == "YES")
            return true;

        if (response.StartsWith("New code generated:"))
        {
            try
            {
                var text = response[(response.IndexOf(':') + 1)..].Replace(":", string.Empty);
                _verKey = text;
                await File.WriteAllTextAsync("verkey.txt", text, _cancellationToken);

                SiteLinkLogger.Info("Password saved");
                server.ForceServerListUpdate = true;
                return true;
            }
            catch
            {
                SiteLinkLogger.Error("Failed to save password");
                return true;
            }
        }

        string[] commands = { "Restart", "RoundRestart", "UpdateData", "RefreshKey", "GetContactAddress" };
        foreach (var cmd in commands)
        {
            if (response.Contains($":{cmd}:"))
            {
                await HandleActionAsync(server, cmd);
                return true;
            }
        }

        if (response.Contains(":Message - "))
        {
            string message = response[(response.IndexOf(":Message - ", StringComparison.Ordinal) + 11)..];
            message = message[..message.IndexOf(":::", StringComparison.Ordinal)];
            SiteLinkLogger.Info(message, "CommandService");
        }

        if (response.Contains("Server is not verified."))
            return false;

        SiteLinkLogger.Error($"Can't update data: {response}", "ScpServerList");
        return true;
    }

    private async Task HandleActionAsync(Listener listener, string action)
    {
        SiteLinkLogger.Info($"Action: {action}");

        switch (action.ToUpperInvariant())
        {
            case "UPDATEDATA":
                listener.ForceServerListUpdate = true;
                break;

            case "GETCONTACTADDRESS":
                await SendContactAddressAsync(listener);
                break;
        }
    }

    private async Task SendContactAddressAsync(Listener listener)
    {
        var data = new Dictionary<string, string>
        {
            { "ip", listener.PublicAddress },
            { "port", $"{listener.ListenPort}" },
            { "version", "2" },
            { "address", listener.Settings.ServerList.Email.Base64Encode() }
        };

        if (!string.IsNullOrEmpty(Password))
            data.Add("passcode", Password);

        try
        {
            using var response = await listener.Http.PostAsync("https://api.scpslgame.com/v4/contactaddress.php", new FormUrlEncodedContent(data), _cancellationToken);
            string text = await response.Content.ReadAsStringAsync(_cancellationToken);
            Console.WriteLine(text);
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error(ex, "ScpServerList");
        }
    }

    private Dictionary<string, string> BuildUpdateData(Listener listener, string players)
    {
        var baseData = new Dictionary<string, string>
        {
            { "ip", listener.PublicAddress },
            { "players", players },
            { "port", $"{listener.ListenPort}" },
            { "version", "2" }
        };

        if (listener.ServerListUpdate)
        {
            baseData["update"] = "1";
            baseData["gameVersion"] = listener.Settings.GameVersion;
            baseData["info"] = listener.Settings.Name.Replace('+', '-') + $"<color=#00000000><size=1>SiteLink v{SiteLinkAPI.Version}</size></color>".Base64Encode();
            baseData["pastebin"] = listener.Settings.ServerList.Pastebin;
            baseData["modded"] = "True";
            baseData["emailSet"] = "True";
            baseData["enforceSameIp"] = "True";
        }

        if (!string.IsNullOrEmpty(Password))
            baseData["passcode"] = Password;

        return baseData;
    }

    public static async Task ExecuteCentralCommandAsync(Listener listener, string cmd, params string[] cmdArgs)
    {
        if (listener?.Http == null)
            throw new ArgumentNullException(nameof(listener), "Listener is null or not initialized.");

        Dictionary<string, string> data = new Dictionary<string, string>
        {
            { "ip", listener.PublicAddress },
            { "port", $"{listener.ListenPort}" },
            { "cmd", cmd.Base64Encode() },
            { "args", string.Join(" ", cmdArgs ?? Array.Empty<string>()).Base64Encode() }
        };

        if (!string.IsNullOrEmpty(Password))
            data.Add("passcode", Password);

        using FormUrlEncodedContent content = new FormUrlEncodedContent(data);
        using HttpResponseMessage response = await listener.Http.PostAsync($"https://api.scpslgame.com/centralcommands/{cmd}.php", content);
        string responseText = await response.Content.ReadAsStringAsync();

        SiteLinkLogger.Info($"[(f=green){cmd}(f=white)] {responseText}", $"central");
    }

    public static void ExecuteCentralCommand(Listener listener, string cmd, params string[] cmdArgs) => ExecuteCentralCommandAsync(listener, cmd, cmdArgs).GetAwaiter().GetResult();
}
