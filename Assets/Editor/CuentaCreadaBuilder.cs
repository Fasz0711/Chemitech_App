using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public static class CuentaCreadaBuilder
{
    const float RW = 1600f, RH = 900f;
    const float PANEL_W = 560f;
    const float PANEL_H = 480f;

    [MenuItem("ChemiTech/Build Cuenta Creada Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Cuenta Creada Scene",
            "Esto creará Assets/Scenes/CuentaCreadaScene.unity.\n¿Continuar?",
            "Sí, construir", "Cancelar"))
            return;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Assets ───────────────────────────────────────────────────────────
        var fnt        = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        var circleSpr  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
        var bgSpr      = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MainMenuBG.png");
        var roundedSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        var planetSpr  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/planet-blue.png");
        var checkSpr   = GenerateCheckSprite();

        // ── Cámara ───────────────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("0A1240");
        cam.orthographic    = true;
        cam.depth           = -1;
        camGo.AddComponent<AudioListener>();

        // ── EventSystem ───────────────────────────────────────────────────────
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();

        // ── Canvas ────────────────────────────────────────────────────────────
        var cGo = new GameObject("Canvas");
        var cv  = cGo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var csc = cGo.AddComponent<CanvasScaler>();
        csc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        csc.referenceResolution = new Vector2(RW, RH);
        csc.matchWidthOrHeight  = 0.5f;
        cGo.AddComponent<GraphicRaycaster>();

        // ── Fondo ─────────────────────────────────────────────────────────────
        var bgGo  = MakeEmpty(cGo.transform, "Background");
        var bgRT  = bgGo.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = bgSpr; bgImg.color = Color.white; bgImg.type = Image.Type.Simple;

        // ── Planetas decorativos de fondo ─────────────────────────────────────
        if (planetSpr != null)
        {
            MakePlanet(cGo.transform, planetSpr, "PlanetLeft",  new Vector2(-640f, 150f), 240f, new Color(0.55f, 0.45f, 0.85f, 0.9f));
            MakePlanet(cGo.transform, planetSpr, "PlanetRight", new Vector2( 610f, -190f), 150f, new Color(0.30f, 0.45f, 0.80f, 0.85f));
        }

        // ── Átomos flotantes (fuera del panel) ────────────────────────────────
        MakeAtom(cGo.transform, circleSpr, fnt, "Atom_He", "He", new Vector2(-560f, 120f), 64f, Hex("E0608A"));
        MakeAtom(cGo.transform, circleSpr, fnt, "Atom_Na", "Na", new Vector2( 600f, 185f), 64f, Hex("FFD23F"));

        // ── Borde del panel ───────────────────────────────────────────────────
        var borderGo  = MakeEmpty(cGo.transform, "PanelBorder");
        SetRT(borderGo, Vector2.zero, new Vector2(PANEL_W + 12f, PANEL_H + 12f));
        var borderImg = borderGo.AddComponent<Image>();
        borderImg.sprite = roundedSpr; borderImg.type = Image.Type.Sliced;
        borderImg.color  = Color.white;

        // ── Panel principal ───────────────────────────────────────────────────
        var panelGo  = MakeEmpty(cGo.transform, "Panel");
        SetRT(panelGo, Vector2.zero, new Vector2(PANEL_W, PANEL_H));
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.sprite = roundedSpr; panelImg.type = Image.Type.Sliced;
        panelImg.color  = Hex("242659");

        // ── Título ────────────────────────────────────────────────────────────
        var titleGo  = MakeEmpty(panelGo.transform, "TitleLabel");
        SetRT(titleGo, new Vector2(0f, 150f), new Vector2(PANEL_W - 60f, 130f));
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text               = "¡ La cuenta se ha creado exitosamente !";
        titleTmp.font               = fnt;
        titleTmp.fontSize           = 40f;
        titleTmp.fontStyle          = FontStyles.Bold;
        titleTmp.color              = Color.white;
        titleTmp.alignment          = TextAlignmentOptions.Center;
        titleTmp.enableWordWrapping = true;

        // ── Checkmark verde ───────────────────────────────────────────────────
        var checkGo  = MakeEmpty(panelGo.transform, "CheckIcon");
        SetRT(checkGo, new Vector2(0f, 10f), new Vector2(150f, 150f));
        var checkImg = checkGo.AddComponent<Image>();
        checkImg.sprite = checkSpr; checkImg.type = Image.Type.Simple; checkImg.preserveAspect = true;

        // ── Botón "¡Empezar a explorar!" ──────────────────────────────────────
        var btnGo  = MakeEmpty(panelGo.transform, "BtnExplorar");
        SetRT(btnGo, new Vector2(0f, -160f), new Vector2(440f, 70f));
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = roundedSpr; btnImg.type = Image.Type.Sliced; btnImg.color = Hex("19A7CE");
        var btnExplorar = btnGo.AddComponent<Button>();
        btnExplorar.targetGraphic = btnImg;
        var btnLbl = MakeEmpty(btnGo.transform, "Label");
        Stretch(btnLbl);
        var btnTmp = btnLbl.AddComponent<TextMeshProUGUI>();
        btnTmp.text               = "¡Empezar a explorar!";
        btnTmp.font               = fnt;
        btnTmp.fontSize           = 30f;
        btnTmp.fontStyle          = FontStyles.Bold;
        btnTmp.color              = Color.white;
        btnTmp.alignment          = TextAlignmentOptions.Center;
        btnTmp.overflowMode       = TextOverflowModes.Overflow;
        btnTmp.enableWordWrapping = false;

        // ── Manager ───────────────────────────────────────────────────────────
        var mgrGo = MakeEmpty(cGo.transform, "CuentaCreadaManager");
        var mgr   = mgrGo.AddComponent<CuentaCreadaManager>();
        var so    = new SerializedObject(mgr);
        so.FindProperty("btnExplorar").objectReferenceValue = btnExplorar;
        so.ApplyModifiedProperties();

        // ── Guardar ───────────────────────────────────────────────────────────
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/CuentaCreadaScene.unity");
        AssetDatabase.Refresh();

        Debug.Log("[CuentaCreadaBuilder] ✓ CuentaCreadaScene creada.");
        EditorUtility.DisplayDialog("¡Listo!",
            "CuentaCreadaScene creada.\n\nFile → Build Settings → Add Open Scenes",
            "OK");
    }

    // ── Generador del checkmark (anillo verde + tilde) ────────────────────────
    static Sprite GenerateCheckSprite()
    {
        const string path = "Assets/Sprites/check-green.png";
        const int size = 160;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px  = new Color[size * size];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        var green = new Color(0.49f, 0.78f, 0.30f, 1f); // #7DC74D aprox

        float cx = (size - 1) / 2f, cy = (size - 1) / 2f;
        float outerR = size / 2f - 6f;
        float ringThick = 10f;
        float innerR = outerR - ringThick;

        // Anillo
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d >= innerR && d <= outerR) px[y * size + x] = green;
            }

        // Tilde (✓) — dos segmentos de línea gruesa
        // Coordenadas en espacio de imagen (y hacia arriba al dibujar)
        Vector2 p1 = new Vector2(0.30f * size, 0.50f * size);
        Vector2 p2 = new Vector2(0.45f * size, 0.36f * size);
        Vector2 p3 = new Vector2(0.72f * size, 0.64f * size);
        DrawThickLine(px, size, p1, p2, 9f, green);
        DrawThickLine(px, size, p2, p3, 9f, green);

        tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.spritePixelsPerUnit = 100; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void DrawThickLine(Color[] px, int size, Vector2 a, Vector2 b, float thick, Color col)
    {
        float minX = Mathf.Min(a.x, b.x) - thick, maxX = Mathf.Max(a.x, b.x) + thick;
        float minY = Mathf.Min(a.y, b.y) - thick, maxY = Mathf.Max(a.y, b.y) + thick;
        Vector2 ab = b - a;
        float abLen2 = ab.sqrMagnitude;
        for (int y = Mathf.Max(0, (int)minY); y <= Mathf.Min(size - 1, (int)maxY); y++)
            for (int x = Mathf.Max(0, (int)minX); x <= Mathf.Min(size - 1, (int)maxX); x++)
            {
                Vector2 p = new Vector2(x, y);
                float t = abLen2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLen2) : 0f;
                Vector2 proj = a + t * ab;
                if ((p - proj).sqrMagnitude <= thick * thick) px[y * size + x] = col;
            }
    }

    // ── Planeta decorativo ────────────────────────────────────────────────────
    static void MakePlanet(Transform parent, Sprite spr, string name, Vector2 pos, float size, Color tint)
    {
        var go  = MakeEmpty(parent, name);
        SetRT(go, pos, new Vector2(size, size));
        var img = go.AddComponent<Image>();
        img.sprite = spr; img.color = tint; img.type = Image.Type.Simple; img.preserveAspect = true;
    }

    // ── Átomo con label ───────────────────────────────────────────────────────
    static void MakeAtom(Transform parent, Sprite spr, TMP_FontAsset fnt,
        string goName, string label, Vector2 pos, float size, Color color)
    {
        var go  = MakeEmpty(parent, goName);
        SetRT(go, pos, new Vector2(size, size));
        var img = go.AddComponent<Image>();
        img.sprite = spr; img.color = color; img.type = Image.Type.Simple; img.preserveAspect = true;
        go.AddComponent<FloatingAtom>();

        if (string.IsNullOrEmpty(label)) return;
        var lblGo = MakeEmpty(go.transform, "Label");
        Stretch(lblGo);
        var tmp = lblGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.font      = fnt;
        tmp.fontSize  = size * 0.40f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.overflowMode = TextOverflowModes.Overflow;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetRT(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static GameObject MakeEmpty(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
