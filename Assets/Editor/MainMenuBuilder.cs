using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;

/// <summary>
/// Construye el Menú Principal desde Figma directo en la escena activa.
/// Menú: ChemiTech → Build Main Menu Scene
/// </summary>
public static class MainMenuBuilder
{
    const float RW = 1600f, RH = 900f;

    // Arregla solo los átomos de la escena actual sin borrar nada
    [MenuItem("ChemiTech/Fix/Atoms Circulares")]
    static void FixAtoms()
    {
        var circleSpr = GenerateCircleSprite();
        if (circleSpr == null)
        {
            Debug.LogError("[MainMenuBuilder] No se pudo generar el sprite circular.");
            return;
        }

        string[] atomNames = { "Atom_O", "Atom_C", "Atom_H", "Atom_N", "Atom_Na" };
        int fixed_ = 0;
        foreach (var atomName in atomNames)
        {
            var go = GameObject.Find(atomName);
            if (go == null) continue;

            var bgTransform = go.transform.Find("BG");
            if (bgTransform == null) continue;

            var img = bgTransform.GetComponent<Image>();
            if (img == null) continue;

            img.sprite = circleSpr;
            img.type   = Image.Type.Simple;
            EditorUtility.SetDirty(img);
            fixed_++;
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("¡Listo!",
            $"{fixed_} átomos actualizados a circular.\nGuarda con Ctrl+S.", "OK");
        Debug.Log($"[MainMenuBuilder] {fixed_} átomos → circulares.");
    }

    [MenuItem("ChemiTech/Build Main Menu Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Menú Principal",
            "Esto borrará todos los objetos de la escena actual y construirá el Menú Principal.\n¿Continuar?",
            "Sí, construir", "Cancelar"))
            return;

        // ── Limpiar escena ───────────────────────────────────────────────────
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var go in roots)
            Object.DestroyImmediate(go);

        // ── Assets ───────────────────────────────────────────────────────────
        var fnt      = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        var circleSpr = GenerateCircleSprite();
        var sprBeaker = Spr("Assets/Sprites/icon-beaker.png");
        var sprPlay   = Spr("Assets/Sprites/play-icon.png");
        var sprDiary  = Spr("Assets/Sprites/diary-icon.png");
        var sprGear   = Spr("Assets/Sprites/gear-icon.png");
        var sprPerson = Spr("Assets/Sprites/person-icon.png");
        var sprCyan   = Spr("Assets/Sprites/cyan-button.png");
        var sprPurple = Spr("Assets/Sprites/purple-button.png");
        var sprYellow = Spr("Assets/Sprites/yellow-button.png");
        var sprGray   = Spr("Assets/Sprites/light-gray-button.png");

        if (fnt == null)
            Debug.LogWarning("[MainMenuBuilder] Fredoka-Medium SDF.asset no encontrado.");

        // ── Main Camera ──────────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = Hex("0A1240");
        cam.orthographic     = true;
        cam.depth            = -1;
        camGo.AddComponent<AudioListener>();

        // ── EventSystem ──────────────────────────────────────────────────────
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();

        // ── Canvas ───────────────────────────────────────────────────────────
        var cGo = new GameObject("Canvas");
        var cv  = cGo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var csc = cGo.AddComponent<CanvasScaler>();
        csc.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        csc.referenceResolution = new Vector2(RW, RH);
        csc.matchWidthOrHeight  = 0.5f;
        cGo.AddComponent<GraphicRaycaster>();

        // ── Background con gradiente radial (igual al Figma) ────────────────
        var bgSprite = GenerateBGGradient();
        var bgGo = MakeImg(cGo.transform, "Background", new Vector2(RW, RH), Vector2.zero, Color.white, bgSprite);
        Stretch(bgGo);

        // ── Destellos (sparkles) ─────────────────────────────────────────────
        MakeSparkle(cGo.transform, F2U(460, 580), 36f);
        MakeSparkle(cGo.transform, F2U(1112, 540), 28f);
        MakeSparkle(cGo.transform, F2U(360, 676), 24f);
        MakeSparkle(cGo.transform, F2U(354, 270), 22f);
        MakeSparkle(cGo.transform, F2U(700, 380), 18f);

        // ── Átomos decorativos ───────────────────────────────────────────────
        // Figma center (fx, fy) → Unity anchored position via F2U
        var atomO  = MakeAtom(cGo.transform, fnt, circleSpr, "Atom_O",  "O",
            F2U(159.5f, 649.5f), 140f, -15f, Hex("D62828"), Color.white);
        var atomC  = MakeAtom(cGo.transform, fnt, circleSpr, "Atom_C",  "C",
            F2U(280f, 780f),    120f,  20f, Hex("1E2030"), Color.white);
        var atomH  = MakeAtom(cGo.transform, fnt, circleSpr, "Atom_H",  "H",
            F2U(195f, 795f),    110f,  -5f, Hex("9099AE"), Hex("1B1F3A"));
        var atomN  = MakeAtom(cGo.transform, fnt, circleSpr, "Atom_N",  "N",
            F2U(1412f, 511f),   130f,  10f, Hex("2E45D6"), Color.white);
        var atomNa = MakeAtom(cGo.transform, fnt, circleSpr, "Atom_Na", "Na",
            F2U(1297f, 386.5f), 100f,  -8f, Hex("E8B216"), Hex("1B1F3A"));

        // ── Título ───────────────────────────────────────────────────────────
        var titleGo = MakeEmpty(cGo.transform, "Title");
        RT(titleGo).anchoredPosition = F2U(800f, 218f);
        RT(titleGo).sizeDelta        = new Vector2(760f, 150f);

        MakeImg(titleGo.transform, "BeakerIcon",
            new Vector2(90f, 90f), new Vector2(-315f, 5f), Color.white, sprBeaker);

        MakeTMP(titleGo.transform, fnt, "TitleText", "ChemiTech",
            new Vector2(630f, 145f), new Vector2(45f, 0f),
            108f, Color.white, FontStyles.Bold, 5f);

        // ── Botones principales ──────────────────────────────────────────────
        // Jugar  – cyan  – Figma center (800, 480) – 580×140
        var btnJugar = MakeButton(cGo.transform, fnt,
            "BtnJugar", "Jugar",
            F2U(800f, 480f), new Vector2(580f, 140f),
            sprCyan, sprPlay, Hex("0A2F44"), 56f,
            new Vector2(-82f, 0f), 56f);

        // Diario – purple – Figma center (800, 630) – 580×116
        var btnDiario = MakeButton(cGo.transform, fnt,
            "BtnDiario", "Diario",
            F2U(800f, 630f), new Vector2(580f, 116f),
            sprPurple, sprDiary, Color.white, 46f,
            new Vector2(-71f, 0f), 44f);

        // Ajustes – gray  – Figma center (800, 768) – 580×116
        var btnAjustes = MakeButton(cGo.transform, fnt,
            "BtnAjustes", "Ajustes",
            F2U(800f, 768f), new Vector2(580f, 116f),
            sprGray, sprGear, Hex("2A2F66"), 46f,
            new Vector2(-87f, 0f), 44f);

        // Iniciar Sesión – yellow – Figma center (1367, 787.5) – 354×93
        var btnLogin = MakeButton(cGo.transform, fnt,
            "BtnIniciarSesion", "Iniciar Sesión",
            F2U(1367f, 787.5f), new Vector2(354f, 93f),
            sprYellow, sprPerson, Color.white, 32f,
            new Vector2(-105f, 0f), 44f);

        // ── MainMenuManager + MenuAnimator ───────────────────────────────────
        var mgrGo = MakeEmpty(cGo.transform, "MainMenuManager");
        var mgr   = mgrGo.AddComponent<MainMenuManager>();
        var anim  = mgrGo.AddComponent<MenuAnimator>();

        // Asignar referencias via SerializedObject (respeta [SerializeField] private)
        var mSO = new SerializedObject(mgr);
        mSO.FindProperty("btnJugar").objectReferenceValue         = btnJugar.GetComponent<Button>();
        mSO.FindProperty("btnDiario").objectReferenceValue        = btnDiario.GetComponent<Button>();
        mSO.FindProperty("btnAjustes").objectReferenceValue       = btnAjustes.GetComponent<Button>();
        mSO.FindProperty("btnIniciarSesion").objectReferenceValue = btnLogin.GetComponent<Button>();
        mSO.FindProperty("menuAnimator").objectReferenceValue     = anim;
        mSO.ApplyModifiedProperties();

        // CanvasGroups para animaciones de entrada
        var titleCG = titleGo.AddComponent<CanvasGroup>();
        var jCG     = btnJugar.AddComponent<CanvasGroup>();
        var dCG     = btnDiario.AddComponent<CanvasGroup>();
        var aCG     = btnAjustes.AddComponent<CanvasGroup>();
        var lCG     = btnLogin.AddComponent<CanvasGroup>();
        var aO      = atomO.AddComponent<CanvasGroup>();
        var aC      = atomC.AddComponent<CanvasGroup>();
        var aH      = atomH.AddComponent<CanvasGroup>();
        var aN      = atomN.AddComponent<CanvasGroup>();
        var aNa     = atomNa.AddComponent<CanvasGroup>();

        var aSO = new SerializedObject(anim);
        aSO.FindProperty("tituloRT").objectReferenceValue    = RT(titleGo);
        aSO.FindProperty("tituloGroup").objectReferenceValue = titleCG;
        aSO.FindProperty("loginRT").objectReferenceValue     = RT(btnLogin);
        aSO.FindProperty("loginGroup").objectReferenceValue  = lCG;

        var bRT = aSO.FindProperty("botonesRT");
        bRT.arraySize = 3;
        bRT.GetArrayElementAtIndex(0).objectReferenceValue = RT(btnJugar);
        bRT.GetArrayElementAtIndex(1).objectReferenceValue = RT(btnDiario);
        bRT.GetArrayElementAtIndex(2).objectReferenceValue = RT(btnAjustes);

        var bCG = aSO.FindProperty("botonesGroup");
        bCG.arraySize = 3;
        bCG.GetArrayElementAtIndex(0).objectReferenceValue = jCG;
        bCG.GetArrayElementAtIndex(1).objectReferenceValue = dCG;
        bCG.GetArrayElementAtIndex(2).objectReferenceValue = aCG;

        var atG = aSO.FindProperty("atomosGroup");
        atG.arraySize = 5;
        atG.GetArrayElementAtIndex(0).objectReferenceValue = aO;
        atG.GetArrayElementAtIndex(1).objectReferenceValue = aC;
        atG.GetArrayElementAtIndex(2).objectReferenceValue = aH;
        atG.GetArrayElementAtIndex(3).objectReferenceValue = aN;
        atG.GetArrayElementAtIndex(4).objectReferenceValue = aNa;
        aSO.ApplyModifiedProperties();

        // ── Guardar escena ───────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[MainMenuBuilder] ✓ Menú Principal construido correctamente.");
        EditorUtility.DisplayDialog("¡Listo!", "Menú Principal construido.\nGuarda la escena con Ctrl+S.", "OK");
    }

    // ── Builders ─────────────────────────────────────────────────────────────

    static GameObject MakeAtom(Transform parent, TMP_FontAsset fnt, Sprite circleSpr,
        string name, string letter, Vector2 pos, float size, float rot,
        Color bgColor, Color txtColor)
    {
        var go = MakeEmpty(parent, name);
        RT(go).anchoredPosition    = pos;
        RT(go).sizeDelta           = new Vector2(size, size);
        RT(go).localEulerAngles    = new Vector3(0f, 0f, rot);

        var bg = MakeImg(go.transform, "BG", new Vector2(size, size), Vector2.zero, bgColor, circleSpr);
        bg.GetComponent<Image>().type = Image.Type.Simple;

        MakeTMP(go.transform, fnt, "Letter", letter,
            new Vector2(size, size), Vector2.zero,
            size * 0.44f, txtColor, FontStyles.Bold, 0f);

        go.AddComponent<FloatingAtom>();
        return go;
    }

    static GameObject MakeButton(Transform parent, TMP_FontAsset fnt,
        string name, string label,
        Vector2 pos, Vector2 size,
        Sprite bgSpr, Sprite iconSpr, Color txtColor, float fontSize,
        Vector2 iconOffset, float iconSize)
    {
        var go = MakeEmpty(parent, name);
        RT(go).anchoredPosition = pos;
        RT(go).sizeDelta        = size;

        var bgImg    = go.AddComponent<Image>();
        bgImg.sprite = bgSpr;
        bgImg.type   = bgSpr != null ? Image.Type.Sliced : Image.Type.Simple;
        bgImg.color  = Color.white;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        var bc = btn.colors;
        bc.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
        bc.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
        btn.colors = bc;

        go.AddComponent<ButtonPressEffect>();

        if (iconSpr != null)
            MakeImg(go.transform, "Icon",
                new Vector2(iconSize, iconSize), iconOffset, Color.white, iconSpr);

        float txX = iconOffset.x + iconSize * 0.5f + 55f;
        MakeTMP(go.transform, fnt, "Label", label,
            new Vector2(size.x - 90f, size.y),
            new Vector2(txX, 0f),
            fontSize, txtColor, FontStyles.Bold, 0.5f);

        return go;
    }

    static void MakeSparkle(Transform parent, Vector2 pos, float size)
    {
        var go = MakeImg(parent, "Sparkle", new Vector2(size, size), pos, Color.white, null);
        go.AddComponent<SparkleEffect>();
    }

    // ── Primitivas ────────────────────────────────────────────────────────────

    static GameObject MakeEmpty(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static GameObject MakeImg(Transform parent, string name, Vector2 size, Vector2 pos, Color color, Sprite spr)
    {
        var go = MakeEmpty(parent, name);
        RT(go).sizeDelta        = size;
        RT(go).anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = color;
        if (spr != null) img.sprite = spr;
        return go;
    }

    static TextMeshProUGUI MakeTMP(Transform parent, TMP_FontAsset fnt,
        string name, string text, Vector2 size, Vector2 pos,
        float fontSize, Color color, FontStyles style, float charSpacing)
    {
        var go = MakeEmpty(parent, name);
        RT(go).sizeDelta        = size;
        RT(go).anchoredPosition = pos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.font             = fnt;
        tmp.fontSize         = fontSize;
        tmp.fontStyle        = style;
        tmp.color            = color;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.characterSpacing = charSpacing;
        tmp.overflowMode     = TextOverflowModes.Overflow;
        return tmp;
    }

    // ── Utils ─────────────────────────────────────────────────────────────────

    static Sprite Spr(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    static RectTransform RT(GameObject go) => go.GetComponent<RectTransform>();

    // Coordenadas Figma (origen top-left) → Unity Canvas (origen centro)
    static Vector2 F2U(float fx, float fy) => new Vector2(fx - RW * 0.5f, -(fy - RH * 0.5f));

    static void Stretch(GameObject go)
    {
        var rt = RT(go);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }

    // Genera un sprite circular blanco con borde suave (anti-aliasing)
    static Sprite GenerateCircleSprite()
    {
        const string path = "Assets/Sprites/AtomCircle.png";
        const int size = 256;
        float center = size / 2f;
        float radius = center - 1f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius - dist + 1f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType          = TextureImporterType.Sprite;
        imp.spriteImportMode     = SpriteImportMode.Single;
        imp.alphaIsTransparency  = true;
        imp.mipmapEnabled        = false;
        imp.filterMode           = FilterMode.Bilinear;
        imp.wrapMode             = TextureWrapMode.Clamp;
        imp.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Genera la textura del gradiente radial exacto del Figma y la guarda como sprite
    static Sprite GenerateBGGradient()
    {
        const string texPath = "Assets/Sprites/MainMenuBG.png";
        const int W = 512, H = 288;

        // Stops del gradiente radial del Figma (t, color)
        var stops = new (float t, Color c)[]
        {
            (0.000f, new Color(1.000f, 0.933f, 0.722f)),
            (0.110f, new Color(0.894f, 0.706f, 0.537f)),
            (0.220f, new Color(0.788f, 0.478f, 0.353f)),
            (0.385f, new Color(0.541f, 0.329f, 0.427f)),
            (0.468f, new Color(0.416f, 0.255f, 0.467f)),
            (0.550f, new Color(0.290f, 0.180f, 0.502f)),
            (0.700f, new Color(0.165f, 0.125f, 0.376f)),
            (0.775f, new Color(0.102f, 0.098f, 0.314f)),
            (0.850f, new Color(0.039f, 0.071f, 0.251f)),
            (1.000f, new Color(0.020f, 0.031f, 0.161f)),
        };

        // Centro del gradiente en UV (Figma: cx=800, cy=360 sobre 1600x900)
        float cx = 800f / RW;
        float cy = 360f / RH;

        // Radios normalizados del gradiente (de la matriz del Figma)
        // gradientTransform: matrix(113.14 0 0 76.368 800 360) → r*10
        float rx = (113.14f * 10f) / RW;  // ≈ 0.707
        float ry = (76.368f * 10f) / RH;  // ≈ 0.849

        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float u = (float)x / (W - 1);
                float v = (float)y / (H - 1);

                // Distancia elíptica normalizada desde el centro
                float dx = (u - cx) / rx;
                float dy = (v - cy) / ry;
                float dist = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));

                // Interpolar entre stops
                Color col = stops[stops.Length - 1].c;
                for (int i = 0; i < stops.Length - 1; i++)
                {
                    if (dist >= stops[i].t && dist <= stops[i + 1].t)
                    {
                        float t = (dist - stops[i].t) / (stops[i + 1].t - stops[i].t);
                        col = Color.Lerp(stops[i].c, stops[i + 1].c, t);
                        break;
                    }
                }
                tex.SetPixel(x, H - 1 - y, col); // flip Y (Unity vs Figma)
            }
        }
        tex.Apply();

        // Guardar como PNG
        System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(texPath);

        // Configurar como Sprite
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.textureType       = TextureImporterType.Sprite;
        importer.spriteImportMode  = SpriteImportMode.Single;
        importer.mipmapEnabled     = false;
        importer.filterMode        = FilterMode.Bilinear;
        importer.wrapMode          = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
    }
}
