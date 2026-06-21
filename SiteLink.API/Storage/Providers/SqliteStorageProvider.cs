using Microsoft.Data.Sqlite;

namespace SiteLink.API.Storage.Providers;

public sealed class SqliteStorageProvider : IStorageProvider
{
    private static int _sqliteInitialized;
    private readonly string _connectionString;
    private readonly string _table;

    public SqliteStorageProvider(string path, string tableName)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString();
        _table = ValidateTableName(tableName);
    }

    public string Name => "sqlite";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _sqliteInitialized, 1) == 0)
            SQLitePCL.Batteries_V2.Init();

        using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"CREATE TABLE IF NOT EXISTS `{_table}` (" +
            "scope TEXT NOT NULL, user_id TEXT NOT NULL, data_key TEXT NOT NULL, " +
            "data_value TEXT NULL, updated_at TEXT NOT NULL, " +
            "PRIMARY KEY (scope, user_id, data_key));";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string> GetAsync(
        string scope,
        string userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = await OpenAsync(cancellationToken);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT data_value FROM `{_table}` WHERE scope=$scope AND user_id=$user AND data_key=$key;";
        AddKeys(command, scope, userId, key);
        object value = await command.ExecuteScalarAsync(cancellationToken);
        return value == null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    public async Task SetAsync(
        string scope,
        string userId,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = await OpenAsync(cancellationToken);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO `{_table}` (scope,user_id,data_key,data_value,updated_at) " +
            "VALUES ($scope,$user,$key,$value,$updated) " +
            "ON CONFLICT(scope,user_id,data_key) DO UPDATE SET " +
            "data_value=excluded.data_value, updated_at=excluded.updated_at;";
        AddKeys(command, scope, userId, key);
        command.Parameters.AddWithValue("$value", (object)value ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(
        string scope,
        string userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = await OpenAsync(cancellationToken);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"DELETE FROM `{_table}` WHERE scope=$scope AND user_id=$user AND data_key=$key;";
        AddKeys(command, scope, userId, key);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(
        string scope,
        string userId,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        using SqliteConnection connection = await OpenAsync(cancellationToken);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT data_key,data_value FROM `{_table}` WHERE scope=$scope AND user_id=$user;";
        command.Parameters.AddWithValue("$scope", scope);
        command.Parameters.AddWithValue("$user", userId);
        using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            values[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);

        return values;
    }

    public async Task<bool> ExistsAsync(
        string scope,
        string userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = await OpenAsync(cancellationToken);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT 1 FROM `{_table}` WHERE scope=$scope AND user_id=$user AND data_key=$key LIMIT 1;";
        AddKeys(command, scope, userId, key);
        return await command.ExecuteScalarAsync(cancellationToken) != null;
    }

    public void Dispose()
    {
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddKeys(SqliteCommand command, string scope, string userId, string key)
    {
        command.Parameters.AddWithValue("$scope", scope);
        command.Parameters.AddWithValue("$user", userId);
        command.Parameters.AddWithValue("$key", key);
    }

    private static string ValidateTableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character =>
                !char.IsLetterOrDigit(character) && character != '_'))
            throw new ArgumentException("SQL table names may contain only letters, numbers, and underscores.");

        return value;
    }
}
