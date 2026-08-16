using UnityEngine;

/// <summary>Nivel de un ajuste gráfico. El orden coincide con los segmentos de Ajustes.</summary>
public enum GraphicsLevel { Bajo = 0, Medio = 1, Alto = 2 }

/// <summary>
/// Preferencias gráficas, guardadas POR CUENTA (ver AccountPrefs).
///
/// Dos ejes independientes:
///   • Calidad: coste de render (escala, MSAA, sombras, luces).
///   • Efectos: post-procesado y animación (bloom, viñeta, partículas).
///
/// El invitado —y cualquier cuenta que entre por primera vez en este aparato—
/// arranca en Medio, que es el punto seguro para un móvil de gama media.
/// </summary>
public static class GraphicsPrefs
{
    const string KEY_QUALITY = "chemitech_gfx_quality";
    const string KEY_EFFECTS = "chemitech_gfx_effects";

    public const GraphicsLevel DEFAULT_QUALITY = GraphicsLevel.Medio;
    public const GraphicsLevel DEFAULT_EFFECTS = GraphicsLevel.Medio;

    public static GraphicsLevel Quality
    {
        get => Clamp(AccountPrefs.GetInt(KEY_QUALITY, (int)DEFAULT_QUALITY));
        set => AccountPrefs.SetInt(KEY_QUALITY, (int)Clamp((int)value));
    }

    public static GraphicsLevel Effects
    {
        get => Clamp(AccountPrefs.GetInt(KEY_EFFECTS, (int)DEFAULT_EFFECTS));
        set => AccountPrefs.SetInt(KEY_EFFECTS, (int)Clamp((int)value));
    }

    static GraphicsLevel Clamp(int raw)
        => (GraphicsLevel)Mathf.Clamp(raw, (int)GraphicsLevel.Bajo, (int)GraphicsLevel.Alto);
}
