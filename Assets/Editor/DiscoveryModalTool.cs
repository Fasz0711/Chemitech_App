using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Herramienta ADITIVA: agrega el modal "¡Nuevo descubrimiento!" a ZonaJuegoScene
/// sin tocar el HUD ni el layout existente. Crea (find-or-refresh) el objeto
/// "DiscoveryModal" como hijo directo del Canvas (hermano del HUD, para sobrevivir
/// a un rebuild del HUD) y cablea las referencias en ZonaJuegoManager.
/// Menú: ChemiTech → Add → Discovery Modal (ZonaJuego)
/// </summary>
public static class DiscoveryModalTool
{
    const float RW = 1600f, RH = 900f;

    static TMP_FontAsset fnt;
    static Sprite rounded, diaryIcon;

    static readonly Color CYAN = Hex("2FD2E0");
    static readonly Color GRAY = new Color(1f, 1f, 1f, 0.72f);

    [MenuItem("ChemiTech/Add/Discovery Modal (ZonaJuego)")]
    public static void Add()
    {
        var scene = EditorSceneManager.GetActiveScene();

        var canvas = FindRootWith<Canvas>(scene, "Canvas");
        var mgr    = FindInScene<ZonaJuegoManager>(scene);
        if (canvas == null || mgr == null)
        {
            EditorUtility.DisplayDialog("Discovery Modal",
                "Abre primero ZonaJuegoScene (no encontré Canvas + ZonaJuegoManager en la escena activa).", "OK");
            return;
        }

        fnt       = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded   = Spr("Assets/Sprites/Login/rounded-panel.png");
        diaryIcon = Spr("Assets/Sprites/diary-icon.png");

        // Refresca: elimina un modal previo (es nuestro propio objeto) y lo re-crea.
        var prev = FindChild(canvas.transform, "DiscoveryModal");
        if (prev) Object.DestroyImmediate(prev);

        // ── Modal (pantalla completa) ──────────────────────────────────────────
        var root = MakeEmpty(canvas.transform, "DiscoveryModal"); Stretch(root);
        root.transform.SetAsLastSibling();   // por encima del HUD

        var backdrop = MakeImg(root.transform, "Backdrop", Vector2.zero, new Vector2(RW, RH), Hex("4B2E8F"), null);
        Stretch(backdrop);
        backdrop.GetComponent<Image>().raycastTarget = true;   // bloquea toques al juego

        var badge = MakePanel(root.transform, "Badge", new Vector2(0f, 255f), new Vector2(440f, 58f), Hex("F6C945"));
        MakeText(badge.transform, "Label", "¡NUEVO DESCUBRIMIENTO!", Vector2.zero, new Vector2(420f, 40f), 22f, Hex("5A3A0A"), TextAlignmentOptions.Center, FontStyles.Bold);

        MakeText(root.transform, "Title", "¡Has descubierto\nuna nueva molécula!", new Vector2(0f, 140f), new Vector2(1120f, 170f), 58f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var card = MakePanel(root.transform, "Card", new Vector2(0f, -75f), new Vector2(540f, 240f), Hex("12152E"));
        AddBorder(card, CYAN, 0.6f);
        var formulaTmp = MakeText(card.transform, "Formula", "", new Vector2(0f, 30f), new Vector2(500f, 110f), 80f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        var nameTmp    = MakeText(card.transform, "Name",    "", new Vector2(0f, -72f), new Vector2(500f, 44f), 28f, GRAY, TextAlignmentOptions.Center, FontStyles.Normal);

        var btnVerDiario = MakeButton(root.transform, "BtnVerDiario", "Ver en el diario", new Vector2(-190f, -315f), new Vector2(330f, 96f), Hex("C9CDF2"), Hex("2A2D5A"));
        if (diaryIcon)
        {
            MakeImg(btnVerDiario.transform, "Icon", new Vector2(-118f, 0f), new Vector2(30f, 30f), Hex("2A2D5A"), diaryIcon)
                .GetComponent<Image>().preserveAspect = true;
            var lbl = btnVerDiario.transform.Find("Label") as RectTransform;
            if (lbl) lbl.anchoredPosition = new Vector2(22f, 0f);
        }
        var btnContinuar = MakeButton(root.transform, "BtnContinuar", "Continuar", new Vector2(190f, -315f), new Vector2(330f, 96f), CYAN, Hex("0A2F44"));

        root.SetActive(false);

        // ── Cableado en ZonaJuegoManager ───────────────────────────────────────
        var so = new SerializedObject(mgr);
        SetRef(so, "discoveryModal",   root);
        SetRef(so, "discoveryFormula", formulaTmp);
        SetRef(so, "discoveryName",    nameTmp);
        SetRef(so, "btnVerDiario",     btnVerDiario);
        SetRef(so, "btnContinuar",     btnContinuar);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[DiscoveryModalTool] ✓ Modal de descubrimiento agregado y cableado.");
        EditorUtility.DisplayDialog("¡Listo!",
            "Modal \"¡Nuevo descubrimiento!\" agregado al Canvas y cableado a ZonaJuegoManager.\n\n" +
            "Aparecerá al descubrir una molécula nueva (isNewDiscovery).", "OK");
    }

    // ── Helpers de escena ─────────────────────────────────────────────────────────
    static T FindRootWith<T>(Scene scene, string preferName) where T : Component
    {
        // Primero por nombre; si no, el primer root que tenga el componente.
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

    // ── Helpers de UI (self-contained) ────────────────────────────────────────────
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

    static void AddBorder(Image panel, Color color, float alpha)
    {
        var rt = panel.rectTransform;
        var b = MakeImg(panel.transform.parent, panel.name + "_Border", rt.anchoredPosition, rt.sizeDelta + new Vector2(8f, 8f),
            new Color(color.r, color.g, color.b, alpha), rounded);
        b.GetComponent<Image>().type = Image.Type.Sliced;
        b.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());
    }

    static GameObject MakeImg(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite spr)
    {
        var go = MakeEmpty(parent, name); SetRT(go, pos, size);
        var img = go.AddComponent<Image>(); img.color = color; if (spr != null) img.sprite = spr;
        return go;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size,
        float fontSize, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var go = MakeEmpty(parent, name); SetRT(go, pos, size);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = fnt; tmp.fontSize = fontSize; tmp.color = color;
        tmp.alignment = align; tmp.fontStyle = style;
        tmp.enableWordWrapping = true; tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bg, Color textColor)
    {
        var img = MakePanel(parent, name, pos, size, bg);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        var cb = btn.colors; cb.highlightedColor = new Color(1f, 1f, 1f, 0.92f); cb.pressedColor = new Color(0.85f, 0.85f, 0.9f, 1f); btn.colors = cb;
        MakeText(img.transform, "Label", label, Vector2.zero, size, 28f, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
        return btn;
    }

    static void SetRT(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Sprite Spr(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
}
