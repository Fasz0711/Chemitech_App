using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Herramienta ADITIVA: reemplaza los "iconos" que eran glifos de texto rotos
/// (la fuente Fredoka no los tiene y no hay fallback ni sprite asset) por sprites
/// reales (Image), igual que los demás iconos del HUD. Idempotente.
///   • BtnRecentrar: "⟳  Recentrar" → ícono orbit-ring + "Recentrar".
///   • SavedToast:   "Guardado ✓"  → ícono check-green + "Guardado".
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
        var recenter = FindDeep(canvas.transform, "BtnRecentrar");
        if (recenter)
        {
            SetLabel(recenter, "Recentrar");
            EnsureIcon(recenter, "Assets/Sprites/orbit-ring.png",
                       new Vector2(-70f, 0f), new Vector2(24f, 24f), Hex("23204A"));
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

        Debug.Log($"[IconGlyphFixTool] ✓ {fixes} icono(s) reemplazado(s) por sprites.");
        EditorUtility.DisplayDialog("¡Listo!",
            $"{fixes} icono(s) de glifo roto reemplazado(s) por sprite (Recentrar, toast Guardado).", "OK");
    }

    // Cambia el texto del hijo "Label" (quita el glifo roto).
    static void SetLabel(Transform parent, string text)
    {
        var label = DirectChild(parent, "Label");
        var tmp = label ? label.GetComponent<TextMeshProUGUI>() : null;
        if (tmp) tmp.text = text;
    }

    // Crea (o refresca) un hijo "Icon" con el sprite dado, anclado al centro.
    static void EnsureIcon(Transform parent, string spritePath, Vector2 pos, Vector2 size, Color color)
    {
        var iconT = DirectChild(parent, "Icon");
        GameObject go = iconT ? iconT.gameObject : null;
        if (go == null)
        {
            go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);
        }
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
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

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
}
