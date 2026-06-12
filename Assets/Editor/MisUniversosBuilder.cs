using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public static class MisUniversosBuilder
{
    const float RW = 1600f, RH = 900f;
    const float PANEL_W   = 760f;
    const float HEADER_H  = 82f;
    const float CONTENT_H = 440f;
    const float GAP       = 16f;

    [MenuItem("ChemiTech/Build Mis Universos Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Mis Universos Scene",
            "Esto creará Assets/Scenes/MisUniversosScene.unity.\n¿Continuar?",
            "Sí, construir", "Cancelar"))
            return;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Assets ───────────────────────────────────────────────────────────
        var fnt        = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        var circleSpr  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
        var bgSpr      = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MainMenuBG.png");
        var roundedSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        var planetSpr  = GeneratePlanetSprite();
        var ringSpr    = GenerateRingSprite();

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

        // ── Posiciones verticales del layout ──────────────────────────────────
        float totalH    = HEADER_H + GAP + CONTENT_H;   // 538
        float headerCY  =  totalH / 2f - HEADER_H  / 2f; // +228
        float contentCY = -totalH / 2f + CONTENT_H / 2f; // -49

        // ── Header: borde ─────────────────────────────────────────────────────
        var hBorderGo  = MakeEmpty(cGo.transform, "HeaderBorder");
        SetRT(hBorderGo, new Vector2(0f, headerCY), new Vector2(PANEL_W + 8f, HEADER_H + 8f));
        var hBorderImg = hBorderGo.AddComponent<Image>();
        hBorderImg.sprite = roundedSpr; hBorderImg.type = Image.Type.Sliced;
        hBorderImg.color  = new Color(1f, 1f, 1f, 0.22f);

        // ── Header: panel ─────────────────────────────────────────────────────
        var headerGo  = MakeEmpty(cGo.transform, "HeaderPanel");
        SetRT(headerGo, new Vector2(0f, headerCY), new Vector2(PANEL_W, HEADER_H));
        var headerImg = headerGo.AddComponent<Image>();
        headerImg.sprite = roundedSpr; headerImg.type = Image.Type.Sliced;
        headerImg.color  = Hex("1E2050");

        // Botón Atrás
        const float BTN_SZ = 52f, H_PAD = 14f;
        float backX = -PANEL_W / 2f + H_PAD + BTN_SZ / 2f;
        var backGo  = MakeEmpty(headerGo.transform, "BtnAtras");
        SetRT(backGo, new Vector2(backX, 0f), new Vector2(BTN_SZ, BTN_SZ));
        var backImg = backGo.AddComponent<Image>();
        backImg.sprite = roundedSpr; backImg.type = Image.Type.Sliced; backImg.color = Hex("2A2D5A");
        var btnAtras = backGo.AddComponent<Button>();
        btnAtras.targetGraphic = backImg;
        var backLbl = MakeEmpty(backGo.transform, "Label");
        Stretch(backLbl);
        var backTmp = backLbl.AddComponent<TextMeshProUGUI>();
        backTmp.text = "<"; backTmp.font = fnt; backTmp.fontSize = 28f;
        backTmp.fontStyle = FontStyles.Bold; backTmp.color = Color.white;
        backTmp.alignment = TextAlignmentOptions.Center;

        // Avatar (placeholder)
        float avatarX = backX + BTN_SZ / 2f + 8f + BTN_SZ / 2f;
        var avatarGo  = MakeEmpty(headerGo.transform, "AvatarIcon");
        SetRT(avatarGo, new Vector2(avatarX, 0f), new Vector2(BTN_SZ, BTN_SZ));
        var avatarImg = avatarGo.AddComponent<Image>();
        avatarImg.sprite = roundedSpr; avatarImg.type = Image.Type.Sliced;
        avatarImg.color  = Hex("3DBE6C");

        // Título del header
        float titleStartX = avatarX + BTN_SZ / 2f + 14f;
        float titleW      = PANEL_W / 2f - titleStartX - H_PAD;
        float titleCX     = titleStartX + titleW / 2f;
        var hTitleGo  = MakeEmpty(headerGo.transform, "TitleLabel");
        SetRT(hTitleGo, new Vector2(titleCX, 0f), new Vector2(titleW, HEADER_H));
        var hTitleTmp = hTitleGo.AddComponent<TextMeshProUGUI>();
        hTitleTmp.text      = "Mis Universos";
        hTitleTmp.font      = fnt;
        hTitleTmp.fontSize  = 36f;
        hTitleTmp.fontStyle = FontStyles.Bold;
        hTitleTmp.color     = Color.white;
        hTitleTmp.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        hTitleTmp.overflowMode = TextOverflowModes.Overflow;

        // ── Contenido: borde ──────────────────────────────────────────────────
        var cBorderGo  = MakeEmpty(cGo.transform, "PanelBorder");
        SetRT(cBorderGo, new Vector2(0f, contentCY), new Vector2(PANEL_W + 14f, CONTENT_H + 14f));
        var cBorderImg = cBorderGo.AddComponent<Image>();
        cBorderImg.sprite = roundedSpr; cBorderImg.type = Image.Type.Sliced;
        cBorderImg.color  = new Color(1f, 1f, 1f, 0.22f);

        // ── Contenido: panel ──────────────────────────────────────────────────
        var contentGo  = MakeEmpty(cGo.transform, "ContentPanel");
        SetRT(contentGo, new Vector2(0f, contentCY), new Vector2(PANEL_W, CONTENT_H));
        var contentImg = contentGo.AddComponent<Image>();
        contentImg.sprite = roundedSpr; contentImg.type = Image.Type.Sliced;
        contentImg.color  = Hex("242659");

        // ── Átomos ────────────────────────────────────────────────────────────
        MakeAtom(contentGo.transform, circleSpr, fnt, "Atom_H",  "H",  new Vector2(-268f,  55f), 70f, Hex("888888"));
        MakeAtom(contentGo.transform, circleSpr, fnt, "Atom_O",  "O",  new Vector2( 263f,  55f), 70f, Hex("E53535"));
        MakeAtom(contentGo.transform, circleSpr, fnt, "Atom_C",  "C",  new Vector2(-233f, -118f), 56f, Hex("555568"));
        MakeAtom(contentGo.transform, circleSpr, fnt, "Atom_Na", "Na", new Vector2( 258f, -118f), 56f, Hex("FFD23F"));

        // ── Anillo orbital (debe estar ANTES del planeta en jerarquía) ─────────
        var ringGo  = MakeEmpty(contentGo.transform, "OrbitRing");
        SetRT(ringGo, new Vector2(10f, 82f), new Vector2(162f, 52f));
        ringGo.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        var ringImg = ringGo.AddComponent<Image>();
        ringImg.sprite = ringSpr; ringImg.type = Image.Type.Simple; ringImg.preserveAspect = false;
        ringImg.color  = new Color(0.45f, 0.82f, 1f, 0.65f);

        // ── Planeta ───────────────────────────────────────────────────────────
        var planetGo  = MakeEmpty(contentGo.transform, "Planet");
        SetRT(planetGo, new Vector2(0f, 92f), new Vector2(100f, 100f));
        var planetImg = planetGo.AddComponent<Image>();
        planetImg.sprite = planetSpr; planetImg.type = Image.Type.Simple; planetImg.preserveAspect = true;

        // ── Destellos decorativos ─────────────────────────────────────────────
        MakeSparkle(contentGo.transform, "Sparkle1", new Vector2(-192f, 128f), 22f, new Color(0.7f, 0.9f, 1f, 0.9f));
        MakeSparkle(contentGo.transform, "Sparkle2", new Vector2( 178f,  18f), 16f, new Color(1f,   1f,  1f, 0.8f));
        MakeSparkle(contentGo.transform, "Sparkle3", new Vector2( -55f, -42f), 14f, new Color(0.6f, 0.8f, 1f, 0.7f));

        // ── Título principal ──────────────────────────────────────────────────
        var mainTitleGo  = MakeEmpty(contentGo.transform, "MainTitle");
        SetRT(mainTitleGo, new Vector2(0f, -16f), new Vector2(PANEL_W - 40f, 68f));
        var mainTitleTmp = mainTitleGo.AddComponent<TextMeshProUGUI>();
        mainTitleTmp.text               = "Aún no tienes universos";
        mainTitleTmp.font               = fnt;
        mainTitleTmp.fontSize           = 46f;
        mainTitleTmp.fontStyle          = FontStyles.Bold;
        mainTitleTmp.color              = Color.white;
        mainTitleTmp.alignment          = TextAlignmentOptions.Center;
        mainTitleTmp.enableWordWrapping = false;

        // ── Subtítulo ─────────────────────────────────────────────────────────
        var subGo  = MakeEmpty(contentGo.transform, "Subtitle");
        SetRT(subGo, new Vector2(0f, -90f), new Vector2(PANEL_W - 180f, 66f));
        var subTmp = subGo.AddComponent<TextMeshProUGUI>();
        subTmp.text = "¡Crea tu primer universo y empieza a combinar\nátomos para descubrir moléculas increíbles! 🌎✨";
        subTmp.font               = fnt;
        subTmp.fontSize           = 22f;
        subTmp.color              = new Color(1f, 1f, 1f, 0.75f);
        subTmp.alignment          = TextAlignmentOptions.Center;
        subTmp.enableWordWrapping = true;

        // ── Botón "Crear mi primer Universo" ──────────────────────────────────
        var btnGo  = MakeEmpty(contentGo.transform, "BtnCrear");
        SetRT(btnGo, new Vector2(0f, -170f), new Vector2(370f, 62f));
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = roundedSpr; btnImg.type = Image.Type.Sliced; btnImg.color = Hex("00BCD4");
        var btnCrear = btnGo.AddComponent<Button>();
        btnCrear.targetGraphic = btnImg;
        var btnLbl = MakeEmpty(btnGo.transform, "Label");
        Stretch(btnLbl);
        var btnTmp = btnLbl.AddComponent<TextMeshProUGUI>();
        btnTmp.text               = "+ Crear mi primer Universo";
        btnTmp.font               = fnt;
        btnTmp.fontSize           = 25f;
        btnTmp.fontStyle          = FontStyles.Bold;
        btnTmp.color              = Color.white;
        btnTmp.alignment          = TextAlignmentOptions.Center;
        btnTmp.overflowMode       = TextOverflowModes.Overflow;
        btnTmp.enableWordWrapping = false;

        // ── Manager ───────────────────────────────────────────────────────────
        var mgrGo = MakeEmpty(cGo.transform, "MisUniversosManager");
        var mgr   = mgrGo.AddComponent<MisUniversosManager>();
        var so    = new SerializedObject(mgr);
        so.FindProperty("btnAtras").objectReferenceValue = btnAtras;
        so.FindProperty("btnCrear").objectReferenceValue = btnCrear;
        so.ApplyModifiedProperties();

        // ── Guardar ───────────────────────────────────────────────────────────
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MisUniversosScene.unity");
        AssetDatabase.Refresh();

        Debug.Log("[MisUniversosBuilder] ✓ MisUniversosScene creada.");
        EditorUtility.DisplayDialog("¡Listo!",
            "MisUniversosScene creada.\n\n" +
            "Pasos finales:\n" +
            "1. File → Build Settings → Add Open Scenes\n" +
            "2. Reemplaza AvatarIcon con la imagen real en el Inspector",
            "OK");
    }

    // ── Generador de sprite planeta (esfera azul con sombreado) ───────────────
    static Sprite GeneratePlanetSprite()
    {
        const string path = "Assets/Sprites/planet-blue.png";
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px  = new Color[size * size];
        float cx = (size - 1) / 2f, cy = (size - 1) / 2f;
        float r  = size / 2f - 2f;
        var baseDark  = new Color(0.04f, 0.22f, 0.52f, 1f);
        var baseLight = new Color(0.28f, 0.78f, 0.96f, 1f);
        // Normalized light direction (upper-left)
        float lx = -0.4f, ly = 0.6f, lz = 0.7f;
        float lLen = Mathf.Sqrt(lx*lx + ly*ly + lz*lz);
        lx /= lLen; ly /= lLen; lz /= lLen;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float d  = Mathf.Sqrt(dx*dx + dy*dy);
                if (d > r) { px[y*size+x] = Color.clear; continue; }
                float nx = dx / r, ny = dy / r;
                float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx*nx - ny*ny));
                float dot = nx*lx + ny*ly + nz*lz;
                float lum = Mathf.Clamp01(dot * 0.7f + 0.38f);
                px[y*size+x] = Color.Lerp(baseDark, baseLight, lum);
            }
        tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.spritePixelsPerUnit = 100; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ── Generador de sprite anillo orbital (círculo hueco) ────────────────────
    static Sprite GenerateRingSprite()
    {
        const string path = "Assets/Sprites/orbit-ring.png";
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px  = new Color[size * size];
        float cx = (size - 1) / 2f, cy = (size - 1) / 2f;
        float outerR = size / 2f - 2f;
        float innerR = outerR - 9f;
        var col = new Color(0.5f, 0.85f, 1f, 1f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - cx)*(x - cx) + (y - cy)*(y - cy));
                px[y*size+x] = (d >= innerR && d <= outerR) ? col : Color.clear;
            }
        tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.spritePixelsPerUnit = 100; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
        tmp.fontSize  = size * 0.42f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.overflowMode = TextOverflowModes.Overflow;
    }

    // ── Destello decorativo ───────────────────────────────────────────────────
    static void MakeSparkle(Transform parent, string name, Vector2 pos, float fontSize, Color color)
    {
        var go  = MakeEmpty(parent, name);
        SetRT(go, pos, new Vector2(30f, 30f));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text         = "✦";
        tmp.font         = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        tmp.fontSize     = fontSize;
        tmp.color        = color;
        tmp.alignment    = TextAlignmentOptions.Center;
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
