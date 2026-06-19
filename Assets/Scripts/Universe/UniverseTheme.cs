using System;
using UnityEngine;

/// <summary>
/// Fuente única de los colores de tema y nombres de ícono usados tanto en
/// CrearUniversoScene como en MisUniversosScene, más utilidades de formato.
/// </summary>
public static class UniverseTheme
{
    public static readonly Color[] Colors =
    {
        new Color(0.30f, 0.85f, 0.91f, 1f), // 0 teal   #4DD9E8
        new Color(0.90f, 0.46f, 0.71f, 1f), // 1 pink   #E575B5
        new Color(0.61f, 0.35f, 0.71f, 1f), // 2 purple #9B59B6
        new Color(0.95f, 0.77f, 0.06f, 1f), // 3 yellow #F1C40F
        new Color(0.18f, 0.80f, 0.44f, 1f), // 4 green  #2ECC71
        new Color(0.91f, 0.30f, 0.24f, 1f), // 5 red    #E74C3C
    };

    // Orden coincide con CrearUniversoBuilder (índice = botón de ícono)
    public static readonly string[] IconNames =
    {
        "icon_galaxy",    // 0
        "icon_planet",    // 1
        "icon_comet",     // 2
        "icon_star",      // 3
        "icon_burst",     // 4
        "icon_satellite", // 5
        "icon_globe",     // 6
        "icon_telescope", // 7
    };

    public static Color ColorAt(int idx)
        => (idx >= 0 && idx < Colors.Length) ? Colors[idx] : Colors[0];

    /// <summary>Texto relativo tipo "Hace 2 días".</summary>
    public static string TimeAgo(long createdAtTicks)
    {
        var created = new DateTime(createdAtTicks, DateTimeKind.Utc);
        var span    = DateTime.UtcNow - created;

        if (span.TotalDays >= 1)
        {
            int d = (int)span.TotalDays;
            return d == 1 ? "Hace 1 día" : $"Hace {d} días";
        }
        if (span.TotalHours >= 1)
        {
            int h = (int)span.TotalHours;
            return h == 1 ? "Hace 1 hora" : $"Hace {h} horas";
        }
        if (span.TotalMinutes >= 1)
        {
            int m = (int)span.TotalMinutes;
            return m == 1 ? "Hace 1 minuto" : $"Hace {m} minutos";
        }
        return "Hace un momento";
    }
}
