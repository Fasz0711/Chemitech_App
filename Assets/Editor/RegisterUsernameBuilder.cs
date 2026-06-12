using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public static class RegisterUsernameBuilder
{
    const float RW = 1600f, RH = 900f;
    const float PANEL_W = 760f, PANEL_H = 530f;
    const float INPUT_W = 580f, INPUT_H = 72f;

    [MenuItem("ChemiTech/Build Register Username Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Register Username Scene",
            "Esto creará Assets/Scenes/RegisterUsernameScene.unity.\n¿Continuar?",
            "Sí, construir", "Cancelar"))
            return;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Assets ───────────────────────────────────────────────────────────
        var fnt        = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        var circleSpr  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
        var bgSpr      = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MainMenuBG.png");
        var roundedSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        var uiSpr      = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var sprLock    = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/icon-lock.png");

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

        // ── Átomos ────────────────────────────────────────────────────────────
        MakeAtom(cGo.transform, circleSpr, fnt, "Atom_He",   "He", F2U(220,  265), 52f, Hex("E75480"));
        MakeAtom(cGo.transform, circleSpr, fnt, "Atom_Na",   "Na", F2U(1380, 225), 30f, Hex("FFD23F"));
        MakeAtom(cGo.transform, circleSpr, fnt, "Atom_Cyan", "",   F2U(1430, 680), 68f, Hex("0097B2"));

        // ── Borde del panel ───────────────────────────────────────────────────
        var borderGo  = MakeEmpty(cGo.transform, "PanelBorder");
        SetRT(borderGo, Vector2.zero, new Vector2(PANEL_W + 14f, PANEL_H + 14f));
        var borderImg = borderGo.AddComponent<Image>();
        borderImg.sprite = roundedSpr; borderImg.type = Image.Type.Sliced;
        borderImg.color  = new Color(1f, 1f, 1f, 0.22f);

        // ── Panel principal ───────────────────────────────────────────────────
        var panelGo  = MakeEmpty(cGo.transform, "RegisterPanel");
        SetRT(panelGo, Vector2.zero, new Vector2(PANEL_W, PANEL_H));
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.sprite = roundedSpr; panelImg.type = Image.Type.Sliced;
        panelImg.color  = Hex("242659");

        // ── Puntos de progreso: pasos 1 y 2 completados, 3 activo ─────────────
        MakeDot(panelGo.transform, "Dot1", new Vector2(-22f, 228f), new Color(1f, 1f, 1f, 0.3f));
        MakeDot(panelGo.transform, "Dot2", new Vector2(  0f, 228f), new Color(1f, 1f, 1f, 0.3f));
        MakeDot(panelGo.transform, "Dot3", new Vector2( 22f, 228f), Hex("2ECC71"));

        // ── Título ────────────────────────────────────────────────────────────
        var titleGo  = MakeEmpty(panelGo.transform, "Title");
        SetRT(titleGo, new Vector2(0f, 168f), new Vector2(PANEL_W - 60f, 80f));
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text               = "¡Elige tu nombre de usuario!";
        titleTmp.font               = fnt;
        titleTmp.fontSize           = 48f;
        titleTmp.fontStyle          = FontStyles.Bold;
        titleTmp.color              = Color.white;
        titleTmp.alignment          = TextAlignmentOptions.Center;
        titleTmp.enableWordWrapping = true;

        // ── Subtítulo ─────────────────────────────────────────────────────────
        var subGo  = MakeEmpty(panelGo.transform, "Subtitle");
        SetRT(subGo, new Vector2(0f, 108f), new Vector2(PANEL_W - 80f, 48f));
        var subTmp = subGo.AddComponent<TextMeshProUGUI>();
        subTmp.text               = "Asegurate que sea un nombre único, no uses tu nombre real.";
        subTmp.font               = fnt;
        subTmp.fontSize           = 22f;
        subTmp.color              = new Color(1f, 1f, 1f, 0.7f);
        subTmp.alignment          = TextAlignmentOptions.Center;
        subTmp.enableWordWrapping = true;

        // ── Label campo ───────────────────────────────────────────────────────
        var labelGo  = MakeEmpty(panelGo.transform, "LabelUsername");
        SetRT(labelGo, new Vector2(0f, 58f), new Vector2(PANEL_W - 100f, 28f));
        var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
        labelTmp.text         = "Nombre de usuario";
        labelTmp.font         = fnt;
        labelTmp.fontSize     = 21f;
        labelTmp.color        = Hex("4DD9E8");
        labelTmp.alignment    = TextAlignmentOptions.Left;
        labelTmp.overflowMode = TextOverflowModes.Overflow;

        // ── Input nombre de usuario ───────────────────────────────────────────
        var inputField = MakeUsernameInput(panelGo.transform, fnt, uiSpr, sprLock,
            "InputUsername", "Nombre de usuario", new Vector2(0f, 10f));

        // ── Label de validación "Nombre inválido" ─────────────────────────────
        var validGo  = MakeEmpty(panelGo.transform, "ValidationLabel");
        SetRT(validGo, new Vector2(0f, -38f), new Vector2(INPUT_W, 28f));
        var validTmp = validGo.AddComponent<TextMeshProUGUI>();
        validTmp.text         = "Nombre inválido";
        validTmp.font         = fnt;
        validTmp.fontSize     = 22f;
        validTmp.color        = Hex("E53535");
        validTmp.fontStyle    = FontStyles.Bold;
        validTmp.alignment    = TextAlignmentOptions.Center;
        validTmp.overflowMode = TextOverflowModes.Overflow;
        validGo.SetActive(false);

        // ── Requisitos (2 columnas × 2 filas) ────────────────────────────────
        const float REQ_W = 272f, REQ_H = 38f, REQ_GAP_X = 16f, REQ_GAP_Y = 12f;
        float col1X = -(REQ_W / 2f + REQ_GAP_X / 2f);
        float col2X =   REQ_W / 2f + REQ_GAP_X / 2f;

        var (bgLen, txtLen) = MakeReqItem(panelGo.transform, fnt, roundedSpr, "ReqLength",
            "8+ caracteres", new Vector2(col1X, -88f),              new Vector2(REQ_W, REQ_H));
        var (bgUpp, txtUpp) = MakeReqItem(panelGo.transform, fnt, roundedSpr, "ReqUpper",
            "Una mayúscula", new Vector2(col2X, -88f),              new Vector2(REQ_W, REQ_H));
        var (bgNum, txtNum) = MakeReqItem(panelGo.transform, fnt, roundedSpr, "ReqNumber",
            "Un número",     new Vector2(col1X, -88f - REQ_H - REQ_GAP_Y), new Vector2(REQ_W, REQ_H));
        var (bgSym, txtSym) = MakeReqItem(panelGo.transform, fnt, roundedSpr, "ReqSymbol",
            "Un símbolo",    new Vector2(col2X, -88f - REQ_H - REQ_GAP_Y), new Vector2(REQ_W, REQ_H));

        // ── Botones ───────────────────────────────────────────────────────────
        const float BTN_W = 220f, BTN_H = 66f, BTN_GAP = 20f;
        const float btnY = -212f;

        var btn1Go  = MakeEmpty(panelGo.transform, "BtnAtras");
        SetRT(btn1Go, new Vector2(-(BTN_W / 2f + BTN_GAP / 2f), btnY), new Vector2(BTN_W, BTN_H));
        var btn1Img = btn1Go.AddComponent<Image>();
        btn1Img.sprite = roundedSpr; btn1Img.type = Image.Type.Sliced; btn1Img.color = Hex("3A3B6B");
        var btnAtras = btn1Go.AddComponent<Button>();
        btnAtras.targetGraphic = btn1Img;
        MakeLabel(btn1Go.transform, fnt, "Atrás", 36f, FontStyles.Bold, Color.white);

        var btn2Go  = MakeEmpty(panelGo.transform, "BtnSiguiente");
        SetRT(btn2Go, new Vector2(BTN_W / 2f + BTN_GAP / 2f, btnY), new Vector2(BTN_W, BTN_H));
        var btn2Img = btn2Go.AddComponent<Image>();
        btn2Img.sprite = roundedSpr; btn2Img.type = Image.Type.Sliced; btn2Img.color = Hex("7B8096");
        var btnSiguiente = btn2Go.AddComponent<Button>();
        btnSiguiente.targetGraphic = btn2Img;
        MakeLabel(btn2Go.transform, fnt, "Siguiente", 36f, FontStyles.Bold, Color.white);

        // ── RegisterUsernameManager ───────────────────────────────────────────
        var mgrGo = MakeEmpty(cGo.transform, "RegisterUsernameManager");
        var mgr   = mgrGo.AddComponent<RegisterUsernameManager>();
        var so    = new SerializedObject(mgr);
        so.FindProperty("inputUsername").objectReferenceValue  = inputField;
        so.FindProperty("validationLabel").objectReferenceValue = validTmp;
        so.FindProperty("reqLengthBg").objectReferenceValue    = bgLen;
        so.FindProperty("reqUpperBg").objectReferenceValue     = bgUpp;
        so.FindProperty("reqNumberBg").objectReferenceValue    = bgNum;
        so.FindProperty("reqSymbolBg").objectReferenceValue    = bgSym;
        so.FindProperty("reqLengthTxt").objectReferenceValue   = txtLen;
        so.FindProperty("reqUpperTxt").objectReferenceValue    = txtUpp;
        so.FindProperty("reqNumberTxt").objectReferenceValue   = txtNum;
        so.FindProperty("reqSymbolTxt").objectReferenceValue   = txtSym;
        so.FindProperty("btnAtras").objectReferenceValue       = btnAtras;
        so.FindProperty("btnSiguiente").objectReferenceValue   = btnSiguiente;
        so.ApplyModifiedProperties();

        // ── Guardar ───────────────────────────────────────────────────────────
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/RegisterUsernameScene.unity");
        AssetDatabase.Refresh();

        Debug.Log("[RegisterUsernameBuilder] ✓ RegisterUsernameScene creada.");
        EditorUtility.DisplayDialog("¡Listo!",
            "RegisterUsernameScene creada.\n\n" +
            "Pasos finales:\n" +
            "1. File → Build Settings → Add Open Scenes\n" +
            "2. Si hay error de Input: ChemiTech → Fix → Input System",
            "OK");
    }

    // ── Input nombre de usuario (Standard, sin toggle) ────────────────────────
    static TMP_InputField MakeUsernameInput(Transform parent, TMP_FontAsset fnt,
        Sprite uiSpr, Sprite iconSpr, string name, string placeholder, Vector2 pos)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, new Vector2(INPUT_W, INPUT_H));
        var bg = go.AddComponent<Image>();
        bg.color = Hex("0D1238"); bg.sprite = uiSpr; bg.type = Image.Type.Sliced;

        if (iconSpr != null)
        {
            var iconGo = MakeEmpty(go.transform, "Icon");
            SetRT(iconGo, new Vector2(-INPUT_W / 2f + 40f, 0f), new Vector2(30f, 30f));
            var iImg = iconGo.AddComponent<Image>();
            iImg.sprite = iconSpr; iImg.color = new Color(1f, 1f, 1f, 0.8f); iImg.preserveAspect = true;
        }

        var area   = new GameObject("Text Area", typeof(RectTransform));
        area.transform.SetParent(go.transform, false);
        var areaRT = area.GetComponent<RectTransform>();
        areaRT.anchorMin = Vector2.zero; areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = new Vector2(72f, 6f); areaRT.offsetMax = new Vector2(-16f, -6f);
        area.AddComponent<RectMask2D>();

        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.transform.SetParent(area.transform, false);
        Stretch(phGo);
        var ph = phGo.AddComponent<TextMeshProUGUI>();
        ph.text               = placeholder;
        ph.font               = fnt;
        ph.fontSize           = 26f;
        ph.color              = new Color(1f, 1f, 1f, 0.3f);
        ph.alignment          = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        ph.enableWordWrapping = false;

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(area.transform, false);
        Stretch(txtGo);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text               = "";
        txt.font               = fnt;
        txt.fontSize           = 26f;
        txt.color              = Color.white;
        txt.alignment          = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        txt.enableWordWrapping = false;

        var field = go.AddComponent<TMP_InputField>();
        var so    = new SerializedObject(field);
        so.FindProperty("m_TextViewport").objectReferenceValue  = areaRT;
        so.FindProperty("m_TextComponent").objectReferenceValue = txt;
        so.FindProperty("m_Placeholder").objectReferenceValue   = ph;
        so.FindProperty("m_TargetGraphic").objectReferenceValue = bg;
        so.FindProperty("m_ContentType").enumValueIndex         = (int)TMP_InputField.ContentType.Standard;
        so.FindProperty("m_LineType").enumValueIndex            = 0;
        so.ApplyModifiedProperties();
        field.interactable     = true;
        field.customCaretColor = true;
        field.caretColor       = Color.white;
        field.caretWidth       = 2;
        field.caretBlinkRate   = 0.85f;

        return field;
    }

    // ── Requisito individual ──────────────────────────────────────────────────
    static (Image bg, TextMeshProUGUI txt) MakeReqItem(Transform parent, TMP_FontAsset fnt,
        Sprite rounded, string name, string label, Vector2 pos, Vector2 size)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, size);
        var bg = go.AddComponent<Image>();
        bg.sprite = rounded; bg.type = Image.Type.Sliced;
        bg.color  = new Color(1f, 1f, 1f, 0.10f);

        var lblGo = MakeEmpty(go.transform, "Label");
        Stretch(lblGo);
        var txt = lblGo.AddComponent<TextMeshProUGUI>();
        txt.text               = "○  " + label;
        txt.font               = fnt;
        txt.fontSize           = 20f;
        txt.color              = new Color(1f, 1f, 1f, 0.85f);
        txt.alignment          = TextAlignmentOptions.Center;
        txt.overflowMode       = TextOverflowModes.Overflow;
        txt.enableWordWrapping = false;

        return (bg, txt);
    }

    // ── Punto de progreso ─────────────────────────────────────────────────────
    static void MakeDot(Transform parent, string name, Vector2 pos, Color color)
    {
        var go  = MakeEmpty(parent, name);
        SetRT(go, pos, new Vector2(12f, 12f));
        var img = go.AddComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
        img.type   = Image.Type.Simple;
        img.color  = color;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
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

    static void MakeLabel(Transform parent, TMP_FontAsset fnt,
        string text, float size, FontStyles style, Color color)
    {
        var go  = MakeEmpty(parent, "Label");
        Stretch(go);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.font               = fnt;
        tmp.fontSize           = size;
        tmp.fontStyle          = style;
        tmp.color              = color;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.enableWordWrapping = false;
    }

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

    static Vector2 F2U(float fx, float fy) => new Vector2(fx - RW / 2f, -(fy - RH / 2f));
    static Color   Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
