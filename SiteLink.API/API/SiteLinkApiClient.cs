using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;
using SiteLink.API.Handlers;

namespace SiteLink.API;

/// <summary>
/// Centralized API client using RestSharp for HTTP requests.
/// Provides better performance, connection pooling, and cleaner error handling.
/// </summary>
public class SiteLinkApiClient : IDisposable
{
    private readonly RestClient _client;
    private readonly string _userAgent;
    private readonly string _gameVersion;

    public SiteLinkApiClient(string userAgent = "SCPSL", string gameVersion = null)
    {
        _userAgent = userAgent;
        _gameVersion = gameVersion ?? SiteLinkAPI.GameVersionText;

        RestClientOptions options = new RestClientOptions
        {
            Timeout = TimeSpan.FromSeconds(30),
            ThrowOnAnyError = false,
            FailOnDeserializationError = false,
            UserAgent = _userAgent
        };

        _client = new RestClient(options, configureSerialization: s => s.UseNewtonsoftJson());
    }

    /// <summary>
    /// Executes a GET request and deserializes the response.
    /// </summary>
    public async Task<T> GetAsync<T>(string endpoint, CancellationToken ct = default)
        where T : class
    {
        var request = new RestRequest(endpoint, Method.Get)
            .AddHeader("Game-Version", _gameVersion);

        var response = await _client.ExecuteAsync<T>(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessful || response.Data is null)
            throw new InvalidOperationException($"GET request failed: HTTP {(int)response.StatusCode} - {response.Content}");

        return response.Data;
    }

    /// <summary>
    /// Executes a GET request and returns the raw response string.
    /// </summary>
    public async Task<string> GetRawAsync(string endpoint, CancellationToken ct = default)
    {
        var request = new RestRequest(endpoint, Method.Get)
            .AddHeader("Game-Version", _gameVersion);

        var response = await _client.ExecuteAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessful || response.Content is null)
            throw new InvalidOperationException($"GET request failed: HTTP {(int)response.StatusCode}");

        return response.Content;
    }

    /// <summary>
    /// Executes a POST request with form-encoded data and deserializes the response.
    /// </summary>
    public async Task<T> PostFormAsync<T>(string endpoint, Dictionary<string, string> formData, CancellationToken ct = default)
        where T : class
    {
        var request = new RestRequest(endpoint, Method.Post)
            .AddHeader("Game-Version", _gameVersion);

        foreach (var kvp in formData)
        {
            request.AddParameter(kvp.Key, kvp.Value);
        }

        var response = await _client.ExecuteAsync<T>(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessful || response.Data is null)
            throw new InvalidOperationException($"POST request failed: HTTP {(int)response.StatusCode} - {response.Content}");

        return response.Data;
    }

    /// <summary>
    /// Executes a POST request with form-encoded data and returns the raw response string.
    /// </summary>
    public async Task<string> PostFormRawAsync(string endpoint, Dictionary<string, string> formData, CancellationToken ct = default)
    {
        var request = new RestRequest(endpoint, Method.Post)
            .AddHeader("Game-Version", _gameVersion);

        foreach (var kvp in formData)
        {
            request.AddParameter(kvp.Key, kvp.Value);
        }

        var response = await _client.ExecuteAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessful || response.Content is null)
            throw new InvalidOperationException($"POST request failed: HTTP {(int)response.StatusCode} {request}");

        return response.Content;
    }

    /// <summary>
    /// Executes a POST request with JSON body.
    /// </summary>
    public async Task<T> PostJsonAsync<T>(string endpoint, object body, CancellationToken ct = default)
        where T : class
    {
        var request = new RestRequest(endpoint, Method.Post)
            .AddHeader("Game-Version", _gameVersion)
            .AddJsonBody(body);

        var response = await _client.ExecuteAsync<T>(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessful || response.Data is null)
            throw new InvalidOperationException($"POST request failed: HTTP {(int)response.StatusCode} - {response.Content}");

        return response.Data;
    }

    #region SCP:SL Server List API Methods

    /// <summary>
    /// Gets the public key from the SCP:SL central server.
    /// </summary>
    public async Task<PublicKey> GetPublicKeyAsync(int majorVersion, CancellationToken ct = default)
    {
        string endpoint = $"{ScpServerListHandler.PublicKeyUrl}?major={majorVersion}";
        return await GetAsync<PublicKey>(endpoint, ct);
    }

    /// <summary>
    /// Posts server list update data to the SCP:SL authenticator.
    /// </summary>
    public async Task<string> PostServerListUpdateAsync(Dictionary<string, string> updateData, CancellationToken ct = default)
    {
        return await PostFormRawAsync(ScpServerListHandler.AuthenticatorUrl, updateData, ct);
    }

    /// <summary>
    /// Posts contact address information to the SCP:SL central server.
    /// </summary>
    public async Task<string> PostContactAddressAsync(Dictionary<string, string> contactData, CancellationToken ct = default)
    {
        return await PostFormRawAsync(ScpServerListHandler.ContactAddressUrl, contactData, ct);
    }

    /// <summary>
    /// Executes a central command on the SCP:SL server.
    /// </summary>
    public async Task<string> PostCentralCommandAsync(string command, Dictionary<string, string> commandData, CancellationToken ct = default)
    {
        string endpoint = $"{ScpServerListHandler.MasterUrl}centralcommands/{command}.php";
        return await PostFormRawAsync(endpoint, commandData, ct);
    }

    /// <summary>
    /// Gets the public IP address from the SCP:SL API.
    /// </summary>
    public async Task<string> GetPublicIpAddressAsync(CancellationToken ct = default)
    {
        return await GetRawAsync("https://api.scpslgame.com/ip.php", ct);
    }

    #endregion

    public void Dispose()
    {
        _client?.Dispose();
    }
}
