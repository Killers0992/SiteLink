namespace SiteLink.API.Models;

/// <summary>
/// Represents the configuration settings for the bridge plugin, including its enabled state and the secret key used for
/// proxy authentication.
/// </summary>
public class BridgeSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the server has installed bridge plugin.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the secret key used for making sure connection to proxy was made by bridge plugin.
    /// </summary>
    public string SecretKey { get; set; } = "---";
}