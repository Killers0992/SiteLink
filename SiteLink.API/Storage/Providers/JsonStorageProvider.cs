using Newtonsoft.Json;

namespace SiteLink.API.Storage.Providers;

public sealed class JsonStorageProvider : IStorageProvider
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, Dictionary<string, Dictionary<string, string>>> _data =
        new(StringComparer.OrdinalIgnoreCase);

    public JsonStorageProvider(string path)
    {
        _path = Path.GetFullPath(path);
    }

    public string Name => "json";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (File.Exists(_path))
            {
                _data = JsonConvert.DeserializeObject<
                    Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(
                        File.ReadAllText(_path))
                    ?? new(StringComparer.OrdinalIgnoreCase);
            }
            else
                SaveUnsafe();

            NormalizeComparers();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetAsync(
        string scope,
        string userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return TryGetUser(scope, userId, out Dictionary<string, string> values) &&
                   values.TryGetValue(key, out string value)
                ? value
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetAsync(
        string scope,
        string userId,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            GetOrCreateUser(scope, userId)[key] = value;
            SaveUnsafe();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoveAsync(
        string scope,
        string userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            bool removed = TryGetUser(scope, userId, out Dictionary<string, string> values) &&
                           values.Remove(key);
            if (removed)
                SaveUnsafe();

            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(
        string scope,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return TryGetUser(scope, userId, out Dictionary<string, string> values)
                ? new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ExistsAsync(
        string scope,
        string userId,
        string key,
        CancellationToken cancellationToken = default) =>
        await GetAsync(scope, userId, key, cancellationToken) != null;

    public void Dispose() => _gate.Dispose();

    private Dictionary<string, string> GetOrCreateUser(string scope, string userId)
    {
        if (!_data.TryGetValue(scope, out Dictionary<string, Dictionary<string, string>> users))
        {
            users = new(StringComparer.OrdinalIgnoreCase);
            _data[scope] = users;
        }

        if (!users.TryGetValue(userId, out Dictionary<string, string> values))
        {
            values = new(StringComparer.OrdinalIgnoreCase);
            users[userId] = values;
        }

        return values;
    }

    private bool TryGetUser(
        string scope,
        string userId,
        out Dictionary<string, string> values)
    {
        values = null;
        return _data.TryGetValue(scope, out Dictionary<string, Dictionary<string, string>> users) &&
               users.TryGetValue(userId, out values);
    }

    private void SaveUnsafe()
    {
        string temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonConvert.SerializeObject(_data, Formatting.Indented));

        if (File.Exists(_path))
            File.Replace(temporary, _path, null);
        else
            File.Move(temporary, _path);
    }

    private void NormalizeComparers()
    {
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> normalized =
            new(StringComparer.OrdinalIgnoreCase);

        foreach ((string scope, Dictionary<string, Dictionary<string, string>> users) in _data)
        {
            Dictionary<string, Dictionary<string, string>> normalizedUsers =
                new(StringComparer.OrdinalIgnoreCase);

            foreach ((string userId, Dictionary<string, string> values) in users)
                normalizedUsers[userId] = new(values, StringComparer.OrdinalIgnoreCase);

            normalized[scope] = normalizedUsers;
        }

        _data = normalized;
    }
}
