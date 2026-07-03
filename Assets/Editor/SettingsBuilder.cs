using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builder ADITIVO de SettingsScene ("Ajustes").
/// Regenera solo el subárbol "SettingsRoot". Header con avatar (HeaderAvatar),
/// pestañas Gráficos/Audio/Cuenta (navegables), segmentos y sliders.
/// Menú: ChemiTech → Build Settings Scene / Rebuild → Settings Scene (desde cero)
/// </summary>
public static class SettingsBuilder
{
    const string ScenePath = "Assets/Scenes/SettingsScene.unity";
    const float  RW = 1600f, RH = 900f;

    static TMP_FontAsset fnt;
    static Sprite rounded, circle, person;

    static readonly Color CYAN   = Hex("2FD2E0");
    static readonly Color PURPLE = Hex("8B5CF6");
    static readonly Color PINK   = Hex("E575B5");
    static readonly Color SEL_TXT = Hex("0A2F44");
    static readonly Color UNSEL_BG = Hex("2E3372");
    static readonly Color GRAY   = new Color(1f, 1f, 1f, 0.85f);

    [MenuItem("ChemiTech/Build Settings Scene")]
    public static void Build() => Build(false);

    [MenuItem("ChemiTech/Rebuild/Settings Scene (desde cero)")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Regenerar Settings desde cero",
            "Esto DESTRUYE la UI de Ajustes actual y la regenera por código.\n¿Continuar?",
            "Sí, regenerar", "Cancelar")) return;
        Build(true);
    }

    static void Build(bool force)
    {
        if (!force && !EditorUtility.DisplayDialog("Construir Settings Scene (aditivo)",
            "Crea la escena si no existe. Si ya existe, NO se reconstruye\npara no perder cambios manuales.\n\n¿Continuar?",
            "Sí, continuar", "Cancelar")) return;

        Scene scene;
        if (File.Exists(ScenePath))
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else scene = EditorSceneManager.GetActiveScene();
        }
        else scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        if (!force)
        {
            var existing = FindRoot(scene, "Canvas");
            if (existing != null && existing.transform.Find("SettingsRoot") != null)
            {
                EditorUtility.DisplayDialog("Settings ya existe",
                    "La UI de Ajustes ya está en la escena. No se reconstruyó para no perder cambios manuales.\n\n" +
                    "Para regenerar: ChemiTech → Rebuild → Settings Scene (desde cero).", "OK");
                return;
            }
        }

        // ── Assets ────────────────────────────────────────────────────────────
        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = Spr("Assets/Sprites/Login/rounded-panel.png");
        circle  = Spr("Assets/Sprites/AtomCircle.png");
        person  = Spr("Assets/Sprites/person-icon.png");

        // ── Cámara / EventSystem / Canvas ──────────────────────────────────────
        var camGo = EnsureRoot(scene, "Main Camera");
        camGo.tag = "MainCamera";
        var cam = Ensure<Camera>(camGo);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("0A1233");
        cam.orthographic = true; cam.depth = -1;
        Ensure<AudioListener>(camGo);

        var esGo = EnsureRoot(scene, "EventSystem");
        Ensure<EventSystem>(esGo); Ensure<StandaloneInputModule>(esGo);

        var canvasGo = EnsureRoot(scene, "Canvas");
        var cv = Ensure<Canvas>(canvasGo); cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var csc = Ensure<CanvasScaler>(canvasGo);
        csc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        csc.referenceResolution = new Vector2(RW, RH); csc.matchWidthOrHeight = 0.5f;
        Ensure<GraphicRaycaster>(canvasGo);

        var old = canvasGo.transform.Find("SettingsRoot");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = MakeFill(canvasGo.transform, "SettingsRoot");

        var bg = MakeImg(root.transform, "Background", new Vector2(RW, RH), Vector2.zero, Color.white, Spr("Assets/Sprites/MainMenuBG.png"));
        Stretch(bg);

        // ── Header ──────────────────────────────────────────────────────────────
        var header = MakePanel(root.transform, "Header", new Vector2(0f, 376f), new Vector2(1480f, 100f), Hex("2E3270"));
        AddBorder(header, PURPLE, 0.5f);
        var backGo = MakePanel(header.transform, "BtnBack", new Vector2(-672f, 0f), new Vector2(76f, 76f), Hex("171A3E"));
        var btnBack = backGo.gameObject.AddComponent<Button>(); btnBack.targetGraphic = backGo;
        MakeText(backGo.transform, "Arrow", "<", Vector2.zero, new Vector2(76f, 76f), 48f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        MakeImg(header.transform, "AvatarRing", new Vector2(86f, 86f), new Vector2(-566f, 0f), CYAN, circle);
        var avatarGo = MakeImg(header.transform, "AvatarIcon", new Vector2(78f, 78f), new Vector2(-566f, 0f), Color.white, circle);
        MakeImg(avatarGo.transform, "Icon", new Vector2(44f, 44f), Vector2.zero, Color.white, person).GetComponent<Image>().preserveAspect = true;
        HeaderAvatarTool.Setup(avatarGo);

        MakeText(header.transform, "Title", "Ajustes", new Vector2(-120f, 0f), new Vector2(760f, 60f), 44f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        // ── Panel principal ───────────────────────────────────────────────────
        var panel = MakePanel(root.transform, "MainPanel", new Vector2(0f, -60f), new Vector2(1480f, 720f), Hex("242659"));
        AddBorder(panel, CYAN, 0.22f);

        // ── Pestañas ──────────────────────────────────────────────────────────
        var tabBar = MakeEmpty(panel.transform, "TabBar");
        var tabs = new Button[3];
        string[] tabNames = { "Gráficos", "Audio", "Cuenta" };
        float[] tabX = { -480f, 0f, 480f };
        for (int i = 0; i < 3; i++)
            tabs[i] = MakeButton(tabBar.transform, "Tab_" + tabNames[i], tabNames[i], new Vector2(tabX[i], 280f), new Vector2(460f, 72f), UNSEL_BG, Color.white);

        // ── Vistas por pestaña (contenedores transparentes) ───────────────────
        var pGraf = MakeFill(panel.transform, "PanelGraficos");
        var pAudio = MakeFill(panel.transform, "PanelAudio");
        var pCuenta = MakeFill(panel.transform, "PanelCuenta");

        // ── Gráficos ──────────────────────────────────────────────────────────
        var quality = BuildSegmentRow(pGraf.transform, "Calidad gráfica", 120f);
        var fx      = BuildSegmentRow(pGraf.transform, "Efectos visuales", 25f);
        MakeText(pGraf.transform, "BrilloLabel", "Brillo", new Vector2(-600f, -90f), new Vector2(260f, 56f), 34f, Hex("F1C40F"), TextAlignmentOptions.Left, FontStyles.Bold);
        var sBrillo = MakeSlider(pGraf.transform, "SliderBrillo", new Vector2(140f, -90f), 720f, Hex("F1C40F"), 60);
        var lBrillo = MakeText(pGraf.transform, "ValBrillo", "60", new Vector2(640f, -90f), new Vector2(80f, 44f), 28f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);

        // ── Audio ─────────────────────────────────────────────────────────────
        MakeText(pAudio.transform, "MusicaLabel", "Música de fondo", new Vector2(-600f, 90f), new Vector2(360f, 44f), 26f, CYAN, TextAlignmentOptions.Left, FontStyles.Bold);
        var sMusica = MakeSlider(pAudio.transform, "SliderMusica", new Vector2(150f, 90f), 700f, Hex("4DD9E8"), 75);
        var lMusica = MakeText(pAudio.transform, "ValMusica", "75", new Vector2(640f, 90f), new Vector2(80f, 44f), 28f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
        MakeText(pAudio.transform, "EfectosLabel", "Efectos Visuales", new Vector2(-600f, -40f), new Vector2(360f, 60f), 26f, PINK, TextAlignmentOptions.Left, FontStyles.Bold, true);
        var sEfectos = MakeSlider(pAudio.transform, "SliderEfectos", new Vector2(150f, -40f), 700f, Hex("E575B5"), 60);
        var lEfectos = MakeText(pAudio.transform, "ValEfectos", "60", new Vector2(640f, -40f), new Vector2(80f, 44f), 28f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);

        // ── Cuenta ────────────────────────────────────────────────────────────
        MakeText(pCuenta.transform, "UsuarioLabel", "Usuario", new Vector2(-600f, 140f), new Vector2(200f, 44f), 26f, CYAN, TextAlignmentOptions.Left, FontStyles.Bold);
        MakeText(pCuenta.transform, "UsuarioValue", "Jugador12345", new Vector2(-330f, 140f), new Vector2(500f, 44f), 26f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        MakeText(pCuenta.transform, "CorreoLabel", "Correo", new Vector2(-600f, 60f), new Vector2(200f, 44f), 26f, PINK, TextAlignmentOptions.Left, FontStyles.Bold);
        MakeText(pCuenta.transform, "CorreoValue", "alex@chemitech.com", new Vector2(-330f, 60f), new Vector2(560f, 44f), 26f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        var btnCerrar   = MakeButton(pCuenta.transform, "BtnCerrarSesion",      "Cerrar Sesión",       new Vector2(-440f, -90f), new Vector2(360f, 92f), CYAN, SEL_TXT);
        var btnCambiar  = MakeButton(pCuenta.transform, "BtnCambiarContrasena", "Cambiar Contraseña",  new Vector2(   0f, -90f), new Vector2(360f, 92f), CYAN, SEL_TXT);
        var btnEliminar = MakeButton(pCuenta.transform, "BtnEliminarCuenta",    "Eliminar Cuenta",     new Vector2( 440f, -90f), new Vector2(360f, 92f), Hex("E74C3C"), Color.white);

        pAudio.SetActive(false);
        pCuenta.SetActive(false);

        // ── Manager ─────────────────────────────────────────────────────────────
        var mgrGo = EnsureRoot(scene, "SettingsManager");
        var mgr = Ensure<SettingsManager>(mgrGo);
        var so = new SerializedObject(mgr);
        so.FindProperty("btnBack").objectReferenceValue = btnBack;
        WireArray(so, "tabButtons", tabs);
        WireArray(so, "tabPanels",  new Object[] { pGraf, pAudio, pCuenta });
        WireArray(so, "qualityButtons", quality);
        WireArray(so, "fxButtons",      fx);
        so.FindProperty("sliderBrillo").objectReferenceValue  = sBrillo;
        so.FindProperty("lblBrillo").objectReferenceValue     = lBrillo;
        so.FindProperty("sliderMusica").objectReferenceValue  = sMusica;
        so.FindProperty("lblMusica").objectReferenceValue     = lMusica;
        so.FindProperty("sliderEfectos").objectReferenceValue = sEfectos;
        so.FindProperty("lblEfectos").objectReferenceValue    = lEfectos;
        so.FindProperty("btnCerrarSesion").objectReferenceValue      = btnCerrar;
        so.FindProperty("btnCambiarContrasena").objectReferenceValue = btnCambiar;
        so.FindProperty("btnEliminarCuenta").objectReferenceValue    = btnEliminar;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[SettingsBuilder] ✓ SettingsScene generada.");
        EditorUtility.DisplayDialog("¡Listo!",
            "SettingsScene generada y agregada a Build Settings.\n" +
            "Si hay error de Input System: ChemiTech → Fix → Input System.", "OK");
    }

    // Fila: label a la izquierda + 3 segmentos (Bajo/Medio/Alto) a la derecha.
    static Button[] BuildSegmentRow(Transform parent, string label, float y)
    {
        MakeText(parent, label + "_Label", label, new Vector2(-540f, y), new Vector2(420f, 44f), 26f, CYAN, TextAlignmentOptions.Left, FontStyles.Bold);
        var seg = new Button[3];
        string[] names = { "Bajo", "Medio", "Alto" };
        float[] sx = { 140f, 370f, 600f };
        for (int i = 0; i < 3; i++)
            seg[i] = MakeButton(parent, "Seg_" + label + "_" + names[i], names[i], new Vector2(sx[i], y), new Vector2(210f, 64f), UNSEL_BG, Color.white);
        return seg;
    }

    // ── Slider (estructura estándar de Unity) ─────────────────────────────────
    static Slider MakeSlider(Transform parent, string name, Vector2 pos, float width, Color fillColor, int value)
    {
        var go = MakeEmpty(parent, name); SetRT(go, pos, new Vector2(width, 32f));
        var slider = go.AddComponent<Slider>();

        var bgGo = MakeEmpty(go.transform, "Background");
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.anchorMin = new Vector2(0f, 0.5f); bgRt.anchorMax = new Vector2(1f, 0.5f);
        bgRt.sizeDelta = new Vector2(0f, 14f); bgRt.anchoredPosition = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>(); bgImg.sprite = rounded; bgImg.type = Image.Type.Sliced; bgImg.color = Hex("141738");

        var fillArea = MakeEmpty(go.transform, "Fill Area");
        var faRt = (RectTransform)fillArea.transform;
        faRt.anchorMin = new Vector2(0f, 0.5f); faRt.anchorMax = new Vector2(1f, 0.5f);
        faRt.offsetMin = new Vector2(8f, -7f); faRt.offsetMax = new Vector2(-8f, 7f);
        var fillGo = MakeEmpty(fillArea.transform, "Fill");
        var fillRt = (RectTransform)fillGo.transform;
        fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(1f, 1f); fillRt.sizeDelta = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>(); fillImg.sprite = rounded; fillImg.type = Image.Type.Sliced; fillImg.color = fillColor;

        var handleArea = MakeEmpty(go.transform, "Handle Slide Area");
        var haRt = (RectTransform)handleArea.transform;
        haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(14f, 0f); haRt.offsetMax = new Vector2(-14f, 0f);
        var handleGo = MakeEmpty(handleArea.transform, "Handle");
        var hRt = (RectTransform)handleGo.transform; hRt.sizeDelta = new Vector2(32f, 32f);
        var handleImg = handleGo.AddComponent<Image>(); handleImg.sprite = circle; handleImg.color = Color.white;

        slider.fillRect = fillRt;
        slider.handleRect = hRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
        slider.value = value;
        return slider;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────
    static void WireArray(SerializedObject so, string prop, Object[] items)
    {
        var p = so.FindProperty(prop);
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    static GameObject EnsureRoot(Scene scene, string name)
    {
        var found = FindRoot(scene, name);
        if (found != null) return found;
        var n = new GameObject(name);
        SceneManager.MoveGameObjectToScene(n, scene);
        return n;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects()) if (go.name == name) return go;
        return null;
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>();
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

    static GameObject MakeFill(Transform parent, string name)
    {
        var go = MakeEmpty(parent, name); Stretch(go); return go;
    }

    static Image MakePanel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = MakeEmpty(parent, name); SetRT(go, pos, size);
        var img = go.AddComponent<Image>(); img.sprite = rounded; img.type = Image.Type.Sliced; img.color = color;
        return img;
    }

    static void AddBorder(Image panel, Color color, float alpha)
    {
        var parent = panel.transform.parent;
        var rt = panel.rectTransform;
        var b = MakeImg(parent, panel.name + "_Border", rt.sizeDelta + new Vector2(8f, 8f), rt.anchoredPosition,
            new Color(color.r, color.g, color.b, alpha), rounded);
        var brt = b.GetComponent<RectTransform>();
        brt.anchorMin = rt.anchorMin; brt.anchorMax = rt.anchorMax; brt.pivot = rt.pivot; brt.anchoredPosition = rt.anchoredPosition;
        b.GetComponent<Image>().type = Image.Type.Sliced;
        b.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());
    }

    static GameObject MakeImg(Transform parent, string name, Vector2 size, Vector2 pos, Color color, Sprite spr)
    {
        var go = MakeEmpty(parent, name); SetRT(go, pos, size);
        var img = go.AddComponent<Image>(); img.color = color; if (spr != null) img.sprite = spr;
        return go;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size,
        float fontSize, Color color, TextAlignmentOptions align, FontStyles style, bool wrap = false)
    {
        var go = MakeEmpty(parent, name); SetRT(go, pos, size);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = fnt; tmp.fontSize = fontSize; tmp.color = color;
        tmp.alignment = align; tmp.fontStyle = style;
        tmp.enableWordWrapping = wrap; tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor, Color textColor)
    {
        var img = MakePanel(parent, name, pos, size, bgColor);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
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

    static void AddSceneToBuildSettings(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in list) if (s.path == path) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
