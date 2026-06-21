namespace SiteLink.API.Storage;

public interface IStorageProvider : IDisposable
{
    string Name { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<string> GetAsync(
        string scope,
        string userId,
        string key,
        CancellationToken cancellationToken = default);
    Task SetAsync(
        string scope,
        string userId,
        string key,
        string value,
        CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(
        string scope,
        string userId,
        string key,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(
        string scope,
        string userId,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(
        string scope,
        string userId,
        string key,
        CancellationToken cancellationToken = default);
}
