using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Helpers compartidos por los builders del flujo "Olvidé mi contraseña"
/// (ForgotPassword Email / Code / Reset / Success). Reproducen el estilo de
/// RegisterEmailBuilder / RegisterPasswordBuilder: fondo, átomos flotantes,
/// panel redondeado, puntos de progreso, inputs y botones.
/// </summary>
public static class ForgotPasswordUI
{
    public const float RW = 1600f, RH = 900f;

    public static TMP_FontAsset Font   => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
    public static Sprite Rounded       => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
    public static Sprite Circle        => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
    public static Sprite UiSprite      => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    public static Sprite IconEmail     => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/icon-email.png");
    public static Sprite IconLock      => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/icon-lock.png");
    public static Sprite IconEye       => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/icon-eye.png");
    public static Sprite CheckGreen    => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/check-green.png");

    // ── Escena base (cámara + EventSystem + Canvas + fondo + átomos) ───────────
    public static GameObject NewSceneCanvas()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("0A1240");
        cam.orthographic    = true;
        cam.depth           = -1;
        camGo.AddComponent<AudioListener>();

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();

        var cGo = new GameObject("Canvas");
        var cv  = cGo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var csc = cGo.AddComponent<CanvasScaler>();
        csc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        csc.referenceResolution = new Vector2(RW, RH);
        csc.matchWidthOrHeight  = 0.5f;
        cGo.AddComponent<GraphicRaycaster>();

        var bgGo = MakeEmpty(cGo.transform, "Background");
        var bgRT = bgGo.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MainMenuBG.png");
        bgImg.color = Color.white; bgImg.type = Image.Type.Simple;

        MakeAtom(cGo.transform, "Atom_He",   "He", F2U(220,  265), 52f, Hex("E75480"));
        MakeAtom(cGo.transform, "Atom_Na",   "Na", F2U(1380, 225), 30f, Hex("FFD23F"));
        MakeAtom(cGo.transform, "Atom_Cyan", "",   F2U(1430, 680), 68f, Hex("0097B2"));

        return cGo;
    }

    // ── Panel + borde ──────────────────────────────────────────────────────────
    public static GameObject MakePanel(Transform parent, float w, float h)
    {
        var borderGo = MakeEmpty(parent, "PanelBorder");
        SetRT(borderGo, Vector2.zero, new Vector2(w + 14f, h + 14f));
        var borderImg = borderGo.AddComponent<Image>();
        borderImg.sprite = Rounded; borderImg.type = Image.Type.Sliced;
        borderImg.color  = new Color(1f, 1f, 1f, 0.22f);

        var panelGo = MakeEmpty(parent, "Panel");
        SetRT(panelGo, Vector2.zero, new Vector2(w, h));
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.sprite = Rounded; panelImg.type = Image.Type.Sliced;
        panelImg.color  = Hex("242659");
        return panelGo;
    }

    // Puntos de progreso. activeStep 0-based: previos = verde, actual = cian, siguientes = tenue.
    public static void MakeDots(Transform panel, float y, int total, int activeStep)
    {
        const float gap = 22f;
        float startX = -(total - 1) * gap / 2f;
        for (int i = 0; i < total; i++)
        {
            Color c = i < activeStep ? Hex("2ECC71")
                    : i == activeStep ? Hex("4DD9E8")
                    : new Color(1f, 1f, 1f, 0.3f);
            var go = MakeEmpty(panel, $"Dot{i + 1}");
            SetRT(go, new Vector2(startX + i * gap, y), new Vector2(12f, 12f));
            var img = go.AddComponent<Image>();
            img.sprite = Circle; img.type = Image.Type.Simple; img.color = c;
        }
    }

    // ── Texto ──────────────────────────────────────────────────────────────────
    public static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size,
        Color color, Vector2 pos, Vector2 sizeD, TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal, bool wrap = false)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, sizeD);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.font = Font; t.fontSize = size; t.color = color;
        t.alignment = align; t.fontStyle = style; t.enableWordWrapping = wrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    // ── Botón (con label como hijo estirado) ───────────────────────────────────
    public static Button MakeButton(Transform parent, string name, string label, Vector2 pos,
        Vector2 size, Color bg, out TextMeshProUGUI labelTmp, float labelSize = 36f)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, size);
        var img = go.AddComponent<Image>();
        img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        labelTmp = MakeLabel(go.transform, label, labelSize, FontStyles.Bold, Color.white);
        return btn;
    }

    public static TextMeshProUGUI MakeLabel(Transform parent, string text, float size, FontStyles style, Color color)
    {
        var go = MakeEmpty(parent, "Label");
        Stretch(go);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = Font; tmp.fontSize = size; tmp.fontStyle = style;
        tmp.color = color; tmp.alignment = TextAlignmentOptions.Center;
        tmp.overflowMode = TextOverflowModes.Overflow; tmp.enableWordWrapping = false;
        return tmp;
    }

    // ── Input de texto (ícono opcional, texto alineado a la izquierda) ─────────
    public static TMP_InputField MakeInput(Transform parent, string name, string placeholder, Vector2 pos,
        Sprite icon, TMP_InputField.ContentType contentType, float w = 580f, float h = 72f, float rightPad = 16f)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, new Vector2(w, h));
        var bg = go.AddComponent<Image>();
        bg.color = Hex("0D1238"); bg.sprite = UiSprite; bg.type = Image.Type.Sliced;

        if (icon != null)
        {
            var iconGo = MakeEmpty(go.transform, "Icon");
            SetRT(iconGo, new Vector2(-w / 2f + 40f, 0f), new Vector2(30f, 30f));
            var iImg = iconGo.AddComponent<Image>();
            iImg.sprite = icon; iImg.color = new Color(1f, 1f, 1f, 0.8f); iImg.preserveAspect = true;
        }

        var area = new GameObject("Text Area", typeof(RectTransform));
        area.transform.SetParent(go.transform, false);
        var areaRT = area.GetComponent<RectTransform>();
        areaRT.anchorMin = Vector2.zero; areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = new Vector2(icon != null ? 72f : 16f, 6f);
        areaRT.offsetMax = new Vector2(-rightPad, -6f);
        area.AddComponent<RectMask2D>();

        var ph = MakeChildText(area.transform, "Placeholder", placeholder,
            new Color(1f, 1f, 1f, 0.3f), TextAlignmentOptions.Left | TextAlignmentOptions.Midline);
        var txt = MakeChildText(area.transform, "Text", "",
            Color.white, TextAlignmentOptions.Left | TextAlignmentOptions.Midline);

        WireInputField(go, areaRT, txt, ph, bg, contentType);
        return go.GetComponent<TMP_InputField>();
    }

    // Casilla de código: cuadrada, texto centrado, sin ícono. characterLimit libre
    // para permitir pegar el código completo (el manager reparte 1 char por casilla).
    public static TMP_InputField MakeCodeBox(Transform parent, string name, Vector2 pos, float size)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, new Vector2(size, size * 1.18f));
        var bg = go.AddComponent<Image>();
        bg.color = Hex("0D1238"); bg.sprite = Rounded; bg.type = Image.Type.Sliced;

        var area = new GameObject("Text Area", typeof(RectTransform));
        area.transform.SetParent(go.transform, false);
        var areaRT = area.GetComponent<RectTransform>();
        areaRT.anchorMin = Vector2.zero; areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = new Vector2(4f, 2f); areaRT.offsetMax = new Vector2(-4f, -2f);
        area.AddComponent<RectMask2D>();

        var ph  = MakeChildText(area.transform, "Placeholder", "", new Color(1f, 1f, 1f, 0.25f), TextAlignmentOptions.Center);
        var txt = MakeChildText(area.transform, "Text", "", Color.white, TextAlignmentOptions.Center);
        txt.fontSize = 34f; ph.fontSize = 34f; txt.fontStyle = FontStyles.Bold;

        WireInputField(go, areaRT, txt, ph, bg, TMP_InputField.ContentType.Standard);
        return go.GetComponent<TMP_InputField>();
    }

    static TextMeshProUGUI MakeChildText(Transform parent, string name, string text, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.font = Font; t.fontSize = 26f; t.color = color;
        t.alignment = align; t.enableWordWrapping = false;
        return t;
    }

    static void WireInputField(GameObject go, RectTransform areaRT, TextMeshProUGUI txt,
        TextMeshProUGUI ph, Image bg, TMP_InputField.ContentType contentType)
    {
        var field = go.AddComponent<TMP_InputField>();
        var so = new SerializedObject(field);
        so.FindProperty("m_TextViewport").objectReferenceValue  = areaRT;
        so.FindProperty("m_TextComponent").objectReferenceValue = txt;
        so.FindProperty("m_Placeholder").objectReferenceValue   = ph;
        so.FindProperty("m_TargetGraphic").objectReferenceValue = bg;
        so.FindProperty("m_ContentType").enumValueIndex         = (int)contentType;
        so.FindProperty("m_LineType").enumValueIndex            = 0;
        so.ApplyModifiedProperties();
        field.interactable     = true;
        field.customCaretColor = true;
        field.caretColor       = Color.white;
        field.caretWidth       = 2;
        field.caretBlinkRate    = 0.85f;
    }

    // Ojo para mostrar/ocultar, anclado a la derecha del input.
    public static Button AddEyeToggle(TMP_InputField field, float inputW = 580f)
    {
        var toggleGo = MakeEmpty(field.transform, "BtnToggle");
        SetRT(toggleGo, new Vector2(inputW / 2f - 36f, 0f), new Vector2(32f, 32f));
        var img = toggleGo.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.55f);
        if (IconEye != null) { img.sprite = IconEye; img.preserveAspect = true; }
        var btn = toggleGo.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    // ── Átomo decorativo ───────────────────────────────────────────────────────
    public static void MakeAtom(Transform parent, string goName, string label, Vector2 pos, float size, Color color)
    {
        var go = MakeEmpty(parent, goName);
        SetRT(go, pos, new Vector2(size, size));
        var img = go.AddComponent<Image>();
        img.sprite = Circle; img.color = color; img.type = Image.Type.Simple; img.preserveAspect = true;
        go.AddComponent<FloatingAtom>();

        if (string.IsNullOrEmpty(label)) return;
        var lblGo = MakeEmpty(go.transform, "Label");
        Stretch(lblGo);
        var tmp = lblGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.font = Font; tmp.fontSize = size * 0.42f;
        tmp.fontStyle = FontStyles.Bold; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center; tmp.overflowMode = TextOverflowModes.Overflow;
    }

    // ── Guardar / confirmar ────────────────────────────────────────────────────
    public static void Save(string scenePath)
    {
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();
    }

    public static bool ConfirmBuild(string sceneName, string scenePath)
    {
        bool exists = System.IO.File.Exists(scenePath);
        string msg = exists
            ? $"La escena {sceneName} YA EXISTE y se regenerará desde cero (perderás ajustes manuales del layout).\n¿Continuar?"
            : $"Esto creará {scenePath}.\n¿Continuar?";
        return EditorUtility.DisplayDialog($"Construir {sceneName}", msg, "Sí, construir", "Cancelar");
    }

    public static void Done(string sceneName)
    {
        Debug.Log($"[ForgotPassword] ✓ {sceneName} creada.");
        EditorUtility.DisplayDialog("¡Listo!",
            $"{sceneName} creada.\n\n" +
            "Pasos finales:\n" +
            "1. File → Build Settings → Add Open Scenes\n" +
            "2. Si hay error de Input System: ChemiTech → Fix → Input System",
            "OK");
    }

    // ── Primitivas ─────────────────────────────────────────────────────────────
    public static GameObject MakeEmpty(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    public static void SetRT(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    public static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f); rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static Vector2 F2U(float fx, float fy) => new Vector2(fx - RW / 2f, -(fy - RH / 2f));
    public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
