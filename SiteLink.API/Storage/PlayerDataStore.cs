using Newtonsoft.Json;

namespace SiteLink.API.Storage;

/// <summary>
/// A namespaced typed key/value store for player data.
/// </summary>
public sealed class PlayerDataStore
{
    internal PlayerDataStore(string scope)
    {
        Scope = StorageManager.NormalizeSegment(scope, nameof(scope));
    }

    public string Scope { get; }

    public PlayerDataRecord For(string userId) => new(this, userId);

    public Task<IReadOnlyDictionary<string, string>> GetRawAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        StorageManager.Provider.GetAllAsync(
            Scope,
            StorageManager.NormalizeSegment(userId, nameof(userId)),
            cancellationToken);
}

public sealed class PlayerDataRecord
{
    private readonly PlayerDataStore _store;

    internal PlayerDataRecord(PlayerDataStore store, string userId)
    {
        _store = store;
        UserId = StorageManager.NormalizeSegment(userId, nameof(userId));
    }

    public string Scope => _store.Scope;
    public string UserId { get; }

    public async Task<T> GetAsync<T>(
        string key,
        T defaultValue = default,
        CancellationToken cancellationToken = default)
    {
        string value = await StorageManager.Provider.GetAsync(
            Scope,
            UserId,
            StorageManager.NormalizeSegment(key, nameof(key)),
            cancellationToken);

        return value == null
            ? defaultValue
            : JsonConvert.DeserializeObject<T>(value);
    }

    public T Get<T>(string key, T defaultValue = default) =>
        GetAsync(key, defaultValue).GetAwaiter().GetResult();

    public Task SetAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken = default) =>
        StorageManager.Provider.SetAsync(
            Scope,
            UserId,
            StorageManager.NormalizeSegment(key, nameof(key)),
            JsonConvert.SerializeObject(value),
            cancellationToken);

    public void Set<T>(string key, T value) =>
        SetAsync(key, value).GetAwaiter().GetResult();

    public Task<bool> RemoveAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        StorageManager.Provider.RemoveAsync(
            Scope,
            UserId,
            StorageManager.NormalizeSegment(key, nameof(key)),
            cancellationToken);

    public bool Remove(string key) =>
        RemoveAsync(key).GetAwaiter().GetResult();

    public Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        StorageManager.Provider.ExistsAsync(
            Scope,
            UserId,
            StorageManager.NormalizeSegment(key, nameof(key)),
            cancellationToken);

    public bool Exists(string key) =>
        ExistsAsync(key).GetAwaiter().GetResult();

    public Task<IReadOnlyDictionary<string, string>> GetRawAsync(
        CancellationToken cancellationToken = default) =>
        StorageManager.Provider.GetAllAsync(Scope, UserId, cancellationToken);
}
