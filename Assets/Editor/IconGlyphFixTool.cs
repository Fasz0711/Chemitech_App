using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Herramienta ADITIVA: limpia los "iconos" que eran glifos de texto rotos
/// (la fuente Fredoka no los tiene y no hay fallback ni sprite asset). Idempotente.
///   • BtnRecentrar: "⟳  Recentrar" → "Recentrar" (sin icono).
///   • SavedToast:   "Guardado ✓"  → "Guardado" (sin icono).
/// Menú: ChemiTech → Fix → Icon Glyphs (ZonaJuego)
/// </summary>
public static class IconGlyphFixTool
{
    [MenuItem("ChemiTech/Fix/Icon Glyphs (ZonaJuego)")]
    public static void Fix()
    {
        var scene  = EditorSceneManager.GetActiveScene();
        var canvas = FindRootWith<Canvas>(scene, "Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Icon Glyphs",
                "Abre primero ZonaJuegoScene (no encontré el Canvas en la escena activa).", "OK");
            return;
        }

        int fixes = 0;

        // ── Recentrar ───────────────────────────────────────────────────────────
        // Solo se limpia el glifo ⟳ roto; queda como texto "Recentrar" (sin icono).
        var recenter = FindDeep(canvas.transform, "BtnRecentrar");
        if (recenter)
        {
            SetLabel(recenter, "Recentrar");
            RemoveChild(recenter, "Icon");   // quita el icono si una corrida previa lo agregó
            fixes++;
        }

        // ── Toast "Guardado" ─────────────────────────────────────────────────────
        // Solo se limpia el glifo ✓ (roto). No se pone icono: el sprite de check es
        // verde y el toast tiene fondo verde, quedaría invisible (el tinte no aclara).
        var toast = FindDeep(canvas.transform, "SavedToast");
        if (toast)
        {
            SetLabel(toast, "Guardado");
            fixes++;
        }

        if (fixes == 0)
        {
            EditorUtility.DisplayDialog("Icon Glyphs",
                "No encontré BtnRecentrar ni SavedToast. ¿Es esta ZonaJuegoScene?", "OK");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[IconGlyphFixTool] ✓ {fixes} glifo(s) roto(s) limpiado(s).");
        EditorUtility.DisplayDialog("¡Listo!",
            "Glifos rotos limpiados: Recentrar y el toast \"Guardado\" ahora son solo texto (sin icono).", "OK");
    }

    // Cambia el texto del hijo "Label" (quita el glifo roto).
    static void SetLabel(Transform parent, string text)
    {
        var label = DirectChild(parent, "Label");
        var tmp = label ? label.GetComponent<TextMeshProUGUI>() : null;
        if (tmp) tmp.text = text;
    }

    static void RemoveChild(Transform parent, string name)
    {
        var c = DirectChild(parent, name);
        if (c) Object.DestroyImmediate(c.gameObject);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    static T FindRootWith<T>(Scene scene, string preferName) where T : Component
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == preferName && go.GetComponent<T>() != null) return go.GetComponent<T>();
        foreach (var go in scene.GetRootGameObjects())
        {
            var c = go.GetComponent<T>(); if (c) return c;
        }
        return null;
    }

    static Transform DirectChild(Transform parent, string name)
    {
        foreach (Transform c in parent) if (c.name == name) return c;
        return null;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform c in parent)
        {
            if (c.name == name) return c;
            var r = FindDeep(c, name);
            if (r) return r;
        }
        return null;
    }
}
