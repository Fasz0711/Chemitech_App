using UnityEngine;

/// <summary>
/// Fuente única de los colores e íconos de avatar, para que Perfil y el menú
/// muestren el MISMO avatar a partir del índice guardado (ver AvatarStore).
/// IMPORTANTE: el orden debe coincidir con la cuadrícula de PerfilBuilder
/// (palette + innerIcons). Los íconos se ciclan: índice % IconNames.Length.
/// </summary>
public static class AvatarCatalog
{
    public static readonly Color[] Colors =
    {
        Hex("4A90E2"), Hex("E86AA6"), Hex("3FB979"), Hex("8B5CF6"), Hex("F2922B"),
        Hex("E8731E"), Hex("2FD2E0"), Hex("8A93A8"), Hex("F0A03C"), Hex("D95C9A"),
    };

    public static readonly string[] IconNames =
    {
        "icon_galaxy", "icon_star", "icon_comet", "icon_planet",
        "icon_satellite", "icon_telescope", "icon_globe", "icon_burst",
    };

    public static Color ColorAt(int idx)
        => (idx >= 0 && idx < Colors.Length) ? Colors[idx] : Colors[0];

    /// <summary>Índice del ícono interno (ciclado) para un avatar dado.</summary>
    public static int IconIndexAt(int idx)
        => ((idx % IconNames.Length) + IconNames.Length) % IconNames.Length;

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
}
