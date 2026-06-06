using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Construye la escena "¡Guarda tus descubrimientos!" que aparece al pulsar Jugar.
/// Menú: ChemiTech → Build Guest Prompt Scene
/// </summary>
public static class GuestPromptBuilder
{
    const float RW = 1600f, RH = 900f;
    const float PANEL_W = 760f, PANEL_H = 430f;

    [MenuItem("ChemiTech/Build Guest Prompt Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Guest Prompt Scene",
            "Esto creará Assets/Scenes/GuestPromptScene.unity.\n¿Continuar?",
            "Sí, construir", "Cancelar"))
            return;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Assets ───────────────────────────────────────────────────────────
        var fnt        = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        var circleSpr  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
        var bgSpr      = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MainMenuBG.png");
        var roundedSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        var cyanSpr    = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/cyan-button.png");

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
        var bgGo = MakeEmpty(cGo.transform, "Background");
        var bgRT = bgGo.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = bgSpr;
        bgImg.color  = Color.white;
        bgImg.type   = Image.Type.Simple;

        // ── Átomos flotantes ─────────────────────────────────────────────────
        MakeAtom(cGo.transform, circleSpr, fnt, "Atom_He",   "He", F2U(220,  265), 52f, Hex("E75480"));
        MakeAtom(cGo.transform, circleSpr, fnt, "Atom_Na",   "Na", F2U(1380, 225), 30f, Hex("FFD23F"));
        MakeAtom(cGo.transform, circleSpr, fnt, "Atom_Cyan", "",   F2U(1430, 680), 68f, Hex("0097B2"));

        // ── Borde del panel (anillo blanco sutil) ─────────────────────────────
        var borderGo = MakeEmpty(cGo.transform, "PanelBorder");
        SetRT(borderGo, Vector2.zero, new Vector2(PANEL_W + 14f, PANEL_H + 14f));
        var borderImg = borderGo.AddComponent<Image>();
        borderImg.sprite = roundedSpr;
        borderImg.type   = Image.Type.Sliced;
        borderImg.color  = new Color(1f, 1f, 1f, 0.22f);

        // ── Panel principal ───────────────────────────────────────────────────
        var panelGo = MakeEmpty(cGo.transform, "GuestPanel");
        SetRT(panelGo, Vector2.zero, new Vector2(PANEL_W, PANEL_H));
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.sprite = roundedSpr;
        panelImg.type   = Image.Type.Sliced;
        panelImg.color  = Hex("242659");

        // ── Título ────────────────────────────────────────────────────────────
        var titleGo = MakeEmpty(panelGo.transform, "Title");
        SetRT(titleGo, new Vector2(0f, 148f), new Vector2(PANEL_W - 80f, 90f));
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text              = "¡Guarda tus descubrimientos!";
        titleTmp.font              = fnt;
        titleTmp.fontSize          = 52f;
        titleTmp.fontStyle         = FontStyles.Bold;
        titleTmp.color             = Color.white;
        titleTmp.alignment         = TextAlignmentOptions.Center;
        titleTmp.enableWordWrapping = true;

        // ── Cuerpo ────────────────────────────────────────────────────────────
        var bodyGo = MakeEmpty(panelGo.transform, "Body");
        SetRT(bodyGo, new Vector2(0f, 22f), new Vector2(PANEL_W - 100f, 140f));
        var bodyTmp = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyTmp.text = "Puedes explorar sin cuenta, pero para conservar\ntu diario de moléculas entre sesiones necesitas\niniciar sesión o crear una cuenta.";
        bodyTmp.font              = fnt;
        bodyTmp.fontSize          = 26f;
        bodyTmp.color             = new Color(1f, 1f, 1f, 0.85f);
        bodyTmp.alignment         = TextAlignmentOptions.Center;
        bodyTmp.enableWordWrapping = true;

        // ── Botón Iniciar Sesión (cian) ───────────────────────────────────────
        var btn1Go = MakeEmpty(panelGo.transform, "BtnIniciarSesion");
        SetRT(btn1Go, new Vector2(0f, -112f), new Vector2(440f, 70f));
        var btn1Img = btn1Go.AddComponent<Image>();
        btn1Img.sprite = cyanSpr;
        btn1Img.type   = cyanSpr != null ? Image.Type.Sliced : Image.Type.Simple;
        btn1Img.color  = Color.white;
        var btn1 = btn1Go.AddComponent<Button>();
        btn1.targetGraphic = btn1Img;
        btn1Go.AddComponent<ButtonPressEffect>();
        MakeLabel(btn1Go.transform, fnt, "Iniciar Sesión", 40f, FontStyles.Bold, Hex("0A2F44"));

        // ── Botón Continuar sin cuenta (ghost) ────────────────────────────────
        var btn2Go = MakeEmpty(panelGo.transform, "BtnContinuarSinCuenta");
        SetRT(btn2Go, new Vector2(0f, -196f), new Vector2(440f, 62f));
        // Borde blanco
        var btn2Img = btn2Go.AddComponent<Image>();
        btn2Img.sprite = roundedSpr;
        btn2Img.type   = Image.Type.Sliced;
        btn2Img.color  = new Color(1f, 1f, 1f, 0.45f);
        var btn2 = btn2Go.AddComponent<Button>();
        btn2.targetGraphic = btn2Img;
        // Relleno oscuro interior
        var fillGo = MakeEmpty(btn2Go.transform, "Fill");
        var fillRT = fillGo.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(2f,  2f);
        fillRT.offsetMax = new Vector2(-2f, -2f);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = roundedSpr;
        fillImg.type   = Image.Type.Sliced;
        fillImg.color  = Hex("242659");
        // Texto encima del relleno
        MakeLabel(btn2Go.transform, fnt, "Continuar sin cuenta", 34f, FontStyles.Bold, Color.white);

        // ── GuestPromptManager ────────────────────────────────────────────────
        var mgrGo = MakeEmpty(cGo.transform, "GuestPromptManager");
        var mgr   = mgrGo.AddComponent<GuestPromptManager>();
        var mgrSO = new SerializedObject(mgr);
        mgrSO.FindProperty("btnIniciarSesion").objectReferenceValue      = btn1;
        mgrSO.FindProperty("btnContinuarSinCuenta").objectReferenceValue = btn2;
        mgrSO.ApplyModifiedProperties();

        // ── Guardar escena ────────────────────────────────────────────────────
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/GuestPromptScene.unity");
        AssetDatabase.Refresh();

        Debug.Log("[GuestPromptBuilder] ✓ GuestPromptScene creada.");
        EditorUtility.DisplayDialog("¡Listo!",
            "GuestPromptScene creada correctamente.\n\n" +
            "Pasos finales:\n" +
            "1. File → Build Settings → Add Open Scenes\n" +
            "2. Si hay error de Input System: ChemiTech → Fix → Input System",
            "OK");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    static void MakeAtom(Transform parent, Sprite spr, TMP_FontAsset fnt,
        string goName, string label, Vector2 pos, float size, Color color)
    {
        var go = MakeEmpty(parent, goName);
        SetRT(go, pos, new Vector2(size, size));
        var img = go.AddComponent<Image>();
        img.sprite = spr;
        img.color  = color;
        img.type   = Image.Type.Simple;
        img.preserveAspect = true;
        go.AddComponent<FloatingAtom>();

        if (string.IsNullOrEmpty(label)) return;

        var lblGo = MakeEmpty(go.transform, "Label");
        var lblRT = lblGo.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
        var tmp = lblGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.font      = fnt;
        tmp.fontSize  = size * 0.42f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.overflowMode = TextOverflowModes.Overflow;
    }

    static void MakeLabel(Transform parent, TMP_FontAsset fnt,
        string text, float size, FontStyles style, Color color)
    {
        var go = MakeEmpty(parent, "Label");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.font             = fnt;
        tmp.fontSize         = size;
        tmp.fontStyle        = style;
        tmp.color            = color;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.overflowMode     = TextOverflowModes.Overflow;
        tmp.enableWordWrapping = false;
    }

    static void SetRT(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
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

    static Vector2 F2U(float fx, float fy) => new Vector2(fx - RW / 2f, -(fy - RH / 2f));

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
