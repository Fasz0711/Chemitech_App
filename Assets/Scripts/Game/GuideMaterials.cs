using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Materiales de las guías e indicadores 3D (marca de suelo, línea de altura,
/// anillo de selección, pulsos de enlace).
///
/// Parten de Assets/Resources/Materials/Guide_Unlit.mat en vez de resolverse con
/// Shader.Find. El motivo es de build: Unity solo incluye un shader si algún
/// MATERIAL del proyecto lo referencia, y estos materiales se creaban en runtime,
/// así que URP/Unlit podía quedarse fuera del APK. Al existir el material como
/// asset dentro de Resources —que siempre entra en la build— el shader viaja con
/// él, sin depender de que alguien recuerde la lista de Always Included Shaders.
///
/// Cada llamada devuelve una INSTANCIA nueva: cada guía necesita su propio color
/// y textura, y modificar el asset compartido lo dejaría alterado en el proyecto.
/// </summary>
public static class GuideMaterials
{
    const string RESOURCE_PATH = "Materials/Guide_Unlit";

    static Material _template;
    static bool     _warned;

    /// <summary>Mezcla alfa normal: el objeto tapa lo que hay detrás según su opacidad.</summary>
    public static Material NewAlpha(Texture2D tex)
    {
        var m = NewInstance();
        if (tex) m.SetTexture("_BaseMap", tex);
        return m;
    }

    /// <summary>
    /// Mezcla aditiva: suma luz en vez de tapar. Es lo que hace que los pulsos de
    /// enlace se lean como energía y no como una calcomanía pegada encima.
    /// </summary>
    public static Material NewAdditive(Texture2D tex)
    {
        var m = NewAlpha(tex);
        m.SetInt("_DstBlend", (int)BlendMode.One);
        return m;
    }

    static Material NewInstance()
    {
        if (_template == null) _template = Resources.Load<Material>(RESOURCE_PATH);

        if (_template != null) return new Material(_template);

        // Red de seguridad por si alguien borra el material del proyecto.
        if (!_warned)
        {
            _warned = true;
            Debug.LogWarning($"[GuideMaterials] Falta Assets/Resources/{RESOURCE_PATH}.mat; " +
                             "se recurre a Shader.Find, que puede fallar en la build.");
        }
        return BuildFallback();
    }

    static Material BuildFallback()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Universal Render Pipeline/Lit");

        var m = new Material(shader);
        m.SetFloat("_Surface", 1f);   // Transparent
        m.SetFloat("_Blend", 0f);     // Alpha
        m.SetFloat("_ZWrite", 0f);
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return m;
    }
}
