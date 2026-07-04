using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Herramienta ADITIVA: agrega el indicador discreto "detección de moléculas no
/// disponible" a ZonaJuegoScene sin tocar el HUD. Crea (find-or-refresh) el objeto
/// "DetectionOfflineIndicator" como hijo del Canvas (arriba-centro, anclado al borde
/// superior) y lo cablea a BondManager.detectionOfflineIndicator.
/// Menú: ChemiTech → Add → Detection Offline Indicator (ZonaJuego)
/// </summary>
public static class DetectionIndicatorTool
{
    static TMP_FontAsset fnt;
    static Sprite rounded, circle;

    static readonly Color AMBER = Hex("F1C40F");

    [MenuItem("ChemiTech/Add/Detection Offline Indicator (ZonaJuego)")]
    public static void Add()
    {
        var scene = EditorSceneManager.GetActiveScene();

        var canvas  = FindRootWith<Canvas>(scene, "Canvas");
        var bondMgr = FindInScene<BondManager>(scene);
        if (canvas == null || bondMgr == null)
        {
            EditorUtility.DisplayDialog("Detection Indicator",
                "Abre primero ZonaJuegoScene (no encontré Canvas + BondManager en la escena activa).", "OK");
            return;
        }

        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = Spr("Assets/Sprites/Login/rounded-panel.png");
        circle  = Spr("Assets/Sprites/AtomCircle.png");

        var prev = FindChild(canvas.transform, "DetectionOfflineIndicator");
        if (prev) Object.DestroyImmediate(prev);

        // ── Pill discreta, anclada arriba-centro (todo cuelga del root) ─────────
        var root = MakeEmpty(canvas.transform, "DetectionOfflineIndicator");
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -92f);
        rt.sizeDelta = new Vector2(440f, 50f);
        root.transform.SetAsLastSibling();

        var border = MakeImg(root.transform, "Border", Vector2.zero, new Vector2(446f, 56f),
                             new Color(AMBER.r, AMBER.g, AMBER.b, 0.5f), rounded);
        border.GetComponent<Image>().type = Image.Type.Sliced;
        MakePanel(root.transform, "Bg", Vector2.zero, new Vector2(440f, 50f), new Color(0.07f, 0.08f, 0.16f, 0.95f));

        MakeImg(root.transform, "Dot", new Vector2(-192f, 0f), new Vector2(18f, 18f), AMBER, circle)
            .GetComponent<Image>().preserveAspect = true;
        MakeText(root.transform, "Label", "Detección de moléculas no disponible", new Vector2(20f, 0f),
                 new Vector2(400f, 40f), 19f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Left);

        root.SetActive(false);

        // ── Cableado en BondManager ────────────────────────────────────────────
        var so = new SerializedObject(bondMgr);
        SetRef(so, "detectionOfflineIndicator", root);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[DetectionIndicatorTool] ✓ Indicador de detección agregado y cableado.");
        EditorUtility.DisplayDialog("¡Listo!",
            "Indicador \"detección no disponible\" agregado al Canvas y cableado a BondManager.\n\n" +
            "Aparece si el servicio de IA no responde (timeout/red/5xx) y se oculta al recuperarse.", "OK");
    }

    // ── Helpers de escena ─────────────────────────────────────────────────────────
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

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            var c = go.GetComponentInChildren<T>(true); if (c) return c;
        }
        return null;
    }

    static GameObject FindChild(Transform parent, string name)
    {
        foreach (Transform t in parent) if (t.name == name) return t.gameObject;
        return null;
    }

    static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
    }

    // ── Helpers de UI ─────────────────────────────────────────────────────────────
    static GameObject MakeEmpty(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static Image MakePanel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = MakeEmpty(parent, name); SetRT(go, pos, size);
        var img = go.AddComponent<Image>(); img.sprite = rounded; img.type = Image.Type.Sliced; img.color = color;
        return img;
    }

    static GameObject MakeImg(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite spr)
    {
        var go = MakeEmpty(parent, name); SetRT(go, pos, size);
        var img = go.AddComponent<Image>(); img.color = color; if (spr != null) img.sprite = spr;
        img.raycastTarget = false;
        return go;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size,
        float fontSize, Color color, TextAlignmentOptions align)
    {
        var go = MakeEmpty(parent, name); SetRT(go, pos, size);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = fnt; tmp.fontSize = fontSize; tmp.color = color;
        tmp.alignment = align; tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false; tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void SetRT(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static Sprite Spr(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
}
