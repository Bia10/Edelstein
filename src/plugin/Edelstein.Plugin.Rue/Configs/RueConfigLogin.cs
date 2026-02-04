namespace Edelstein.Plugin.Rue.Configs;

/// <summary>
/// Configuration for Rue login plugin auto-login and auto-registration features.
/// </summary>
public record RueConfigLogin
{
    public bool IsAutoRegister { get; set; }
    public bool IsAutoLogin { get; set; }
    public bool IsFlippedUsername { get; set; }

    public RueConfigLoginCredentials? LoginCredentials { get; set; }

    public byte? AutoSelectWorldID { get; set; }
    public byte? AutoSelectChannelID { get; set; }

    public string? AutoSelectCharacterName { get; set; }
    public int? AutoSelectCharacterIndex { get; set; }

    /// <summary>Second password used for auto-login when account has SPW set.</summary>
    public string? AutoSPW { get; set; }

    public bool AutoCreateCharacter { get; set; }
    public RueConfigAutoCharacter? AutoCharacterConfig { get; set; }
}
