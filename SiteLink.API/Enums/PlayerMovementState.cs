namespace SiteLink.API.Enums;

public enum PlayerMovementState : byte
{
    /// <summary>
    /// Player is currently crouching.
    /// </summary>
    Crouching,

    /// <summary>
    /// Player is walking slowly, not making any sound.
    /// </summary>
    Sneaking,

    /// <summary>
    /// Player is walking.
    /// </summary>
    Walking,

    /// <summary>
    /// Player is sprinting, consuming stamina.
    /// </summary>
    Sprinting,
}
