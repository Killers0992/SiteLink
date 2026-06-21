using System.ComponentModel;

namespace SiteLink.API.Storage;

public sealed class StorageSettings
{
    [Description("Storage provider: json, sqlite, or mysql.")]
    public string Provider { get; set; } = "json";

    [Description("JSON storage file. Used when provider is json.")]
    public string JsonPath { get; set; } = "Data/player_data.json";

    [Description("SQLite database file. Used when provider is sqlite.")]
    public string SqlitePath { get; set; } = "Data/sitelink.db";

    [Description("MySQL connection string. Used when provider is mysql.")]
    public string MysqlConnectionString { get; set; } =
        "Server=127.0.0.1;Port=3306;Database=sitelink;User ID=sitelink;Password=change-me;";

    [Description("Table used by SQL providers.")]
    public string TableName { get; set; } = "sitelink_player_data";
}
