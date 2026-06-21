using MySqlConnector;

namespace SiteLink.API.Storage.Providers;

public sealed class MySqlStorageProvider : IStorageProvider
{
    private readonly string _connectionString;
    private readonly string _table;

    public MySqlStorageProvider(string connectionString, string tableName)
    {
        _connectionString = connectionString;
        _table = ValidateTableName(tableName);
    }

    public string Name => "mysql";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using MySqlConnection connection = await OpenAsync(cancellationToken);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText =
            $"CREATE TABLE IF NOT EXISTS `{_table}` (" +
            "`scope` VARCHAR(128) NOT NULL, `user_id` VARCHAR(191) NOT NULL, " +
            "`data_key` VARCHAR(191) NOT NULL, `data_value` LONGTEXT NULL, " +
            "`updated_at` TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) " +
            "ON UPDATE CURRENT_TIMESTAMP(6), " +
            "PRIMARY KEY (`scope`,`user_id`,`data_key`)) CHARACTER SET utf8mb4;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string> GetAsync(
        string scope,
        string userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        using MySqlConnection connection = await OpenAsync(cancellationToken);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT `data_value` FROM `{_table}` WHERE `scope`=@scope AND `user_id`=@user AND `data_key`=@key;";
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
        using MySqlConnection connection = await OpenAsync(cancellationToken);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO `{_table}` (`scope`,`user_id`,`data_key`,`data_value`) " +
            "VALUES (@scope,@user,@key,@value) " +
            "ON DUPLICATE KEY UPDATE `data_value`=@value;";
        AddKeys(command, scope, userId, key);
        command.Parameters.AddWithValue("@value", (object)value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(
        string scope,
        string userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        using MySqlConnection connection = await OpenAsync(cancellationToken);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText =
            $"DELETE FROM `{_table}` WHERE `scope`=@scope AND `user_id`=@user AND `data_key`=@key;";
        AddKeys(command, scope, userId, key);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(
        string scope,
        string userId,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        using MySqlConnection connection = await OpenAsync(cancellationToken);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT `data_key`,`data_value` FROM `{_table}` WHERE `scope`=@scope AND `user_id`=@user;";
        command.Parameters.AddWithValue("@scope", scope);
        command.Parameters.AddWithValue("@user", userId);
        using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
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
        using MySqlConnection connection = await OpenAsync(cancellationToken);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT 1 FROM `{_table}` WHERE `scope`=@scope AND `user_id`=@user AND `data_key`=@key LIMIT 1;";
        AddKeys(command, scope, userId, key);
        return await command.ExecuteScalarAsync(cancellationToken) != null;
    }

    public void Dispose()
    {
    }

    private async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        MySqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddKeys(MySqlCommand command, string scope, string userId, string key)
    {
        command.Parameters.AddWithValue("@scope", scope);
        command.Parameters.AddWithValue("@user", userId);
        command.Parameters.AddWithValue("@key", key);
    }

    private static string ValidateTableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character =>
                !char.IsLetterOrDigit(character) && character != '_'))
            throw new ArgumentException("SQL table names may contain only letters, numbers, and underscores.");

        return value;
    }
}
