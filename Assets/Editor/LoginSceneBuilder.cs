using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;

/// <summary>
/// Construye la escena de Login desde el diseño Figma.
/// Crea y guarda Assets/Scenes/LoginScene.unity automáticamente.
/// Menú: ChemiTech → Build Login Scene
/// </summary>
public static class LoginSceneBuilder
{
    const float RW = 1600f, RH = 900f;

    // Tamaño del panel central
    const float PANEL_W = 920f, PANEL_H = 740f;
    // Tamaño del input field
    const float INPUT_W = 784f, INPUT_H = 96f;

    [MenuItem("ChemiTech/Build Login Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Login Scene",
            "Esto creará y guardará Assets/Scenes/LoginScene.unity.\n¿Continuar?",
            "Sí, construir", "Cancelar"))
            return;

        // ── Crear escena nueva ────────────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Assets ───────────────────────────────────────────────────────────
        var fnt       = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        var uiSpr     = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var circleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
        var bgSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MainMenuBG.png");

        var sprBeaker = Spr("Assets/Sprites/icon-beaker.png");
        var sprEmail  = Spr("Assets/Sprites/Login/icon-email.png");
        var sprLock   = Spr("Assets/Sprites/Login/icon-lock.png");
        var sprEye    = Spr("Assets/Sprites/Login/icon-eye.png");
        var sprClose  = Spr("Assets/Sprites/Login/icon-close.png");
        var sprCyan   = Spr("Assets/Sprites/cyan-button.png");

        // ── Main Camera ───────────────────────────────────────────────────────
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

        // ── Background ────────────────────────────────────────────────────────
        var bgGo = MakeImg(cGo.transform, "Background", new Vector2(RW, RH), Vector2.zero,
            bgSprite != null ? Color.white : Hex("0A1240"), bgSprite);
        Stretch(bgGo);

        // ── Sparkles ──────────────────────────────────────────────────────────
        MakeSparkle(cGo.transform, F2U(320f, 140f), 28f);
        MakeSparkle(cGo.transform, F2U(1260f, 180f), 20f);
        MakeSparkle(cGo.transform, F2U(280f, 708f), 32f);

        // ── Átomos decorativos (más pequeños que el menú principal) ──────────
        // H – gris, top-left, rot -12°, size 80
        if (circleSpr != null)
        {
            MakeAtom(cGo.transform, fnt, circleSpr, "Atom_H", "H",
                F2U(240f, 240f), 80f, -12f, Hex("9099AE"), Hex("1B1F3A"));
            // O – rojo, center-left, rot +8°, size 70
            MakeAtom(cGo.transform, fnt, circleSpr, "Atom_O", "O",
                F2U(170f, 610f), 70f, 8f, Hex("D62828"), Color.white);
            // C – oscuro, top-right, rot +15°, size 64
            MakeAtom(cGo.transform, fnt, circleSpr, "Atom_C", "C",
                F2U(1388f, 272f), 64f, 15f, Hex("1E2030"), Color.white);
            // N – azul, bottom-right, rot -8°, size 72
            MakeAtom(cGo.transform, fnt, circleSpr, "Atom_N", "N",
                F2U(1319f, 664f), 72f, -8f, Hex("2E45D6"), Color.white);
        }

        // ── Panel de login ────────────────────────────────────────────────────
        // Card centrada en pantalla (Figma: left=340 top=80, 920×740)
        var panel = BuildLoginPanel(cGo.transform, fnt, uiSpr,
            sprBeaker, sprEmail, sprLock, sprEye, sprClose, sprCyan);

        // ── LoginManager ──────────────────────────────────────────────────────
        var mgrGo = MakeEmpty(cGo.transform, "LoginManager");
        var mgr   = mgrGo.AddComponent<LoginManager>();

        // Asignar referencias
        var mSO = new SerializedObject(mgr);
        mSO.FindProperty("inputEmail").objectReferenceValue    = panel.emailField;
        mSO.FindProperty("inputPassword").objectReferenceValue = panel.passwordField;
        mSO.FindProperty("btnIniciarSesion").objectReferenceValue = panel.btnLogin;
        mSO.FindProperty("btnOlvidaste").objectReferenceValue   = panel.btnForgot;
        mSO.FindProperty("btnRegistrate").objectReferenceValue  = panel.btnRegister;
        mSO.FindProperty("btnCerrar").objectReferenceValue      = panel.btnClose;
        mSO.FindProperty("btnTogglePassword").objectReferenceValue = panel.btnToggle;
        mSO.FindProperty("sprOjoOculto").objectReferenceValue   = sprEye;
        mSO.ApplyModifiedProperties();

        // ── Guardar escena ────────────────────────────────────────────────────
        string scenePath = "Assets/Scenes/LoginScene.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();

        Debug.Log($"[LoginSceneBuilder] ✓ LoginScene guardada en {scenePath}");
        EditorUtility.DisplayDialog("¡Listo!",
            $"LoginScene creada en:\n{scenePath}\n\nAgrégala al Build Settings para poder navegar a ella.",
            "OK");
    }

    // ── Construye el panel de login y devuelve referencias ────────────────────
    struct PanelRefs
    {
        public TMP_InputField emailField, passwordField;
        public Button btnLogin, btnForgot, btnRegister, btnClose, btnToggle;
    }

    static PanelRefs BuildLoginPanel(Transform parent, TMP_FontAsset fnt, Sprite uiSpr,
        Sprite sprBeaker, Sprite sprEmail, Sprite sprLock, Sprite sprEye, Sprite sprClose, Sprite sprCyan)
    {
        var refs = new PanelRefs();

        // Contenedor del panel centrado
        var panel = MakeEmpty(parent, "LoginPanel");
        RT(panel).sizeDelta = new Vector2(PANEL_W, PANEL_H);
        RT(panel).anchoredPosition = Vector2.zero; // centro de pantalla

        // Fondo del panel: sombra exterior (borde blanco)
        var bgImg = panel.AddComponent<Image>();
        bgImg.sprite = uiSpr;
        bgImg.type   = Image.Type.Sliced;
        bgImg.color  = Color.white;

        // Área interior oscura
        var inner = MakeImg(panel.transform, "PanelInner",
            new Vector2(PANEL_W - 14f, PANEL_H - 14f), Vector2.zero,
            Hex("252858"), uiSpr);
        inner.GetComponent<Image>().type = Image.Type.Sliced;

        // ── Botón X (cerrar) – top-right del panel ────────────────────────────
        var closeGo = MakeEmpty(inner.transform, "BtnCerrar");
        RT(closeGo).anchoredPosition = new Vector2(388f, 289f);
        RT(closeGo).sizeDelta        = new Vector2(56f, 56f);
        var closeBg = closeGo.AddComponent<Image>();
        closeBg.color = Hex("3A3F6A");
        if (uiSpr != null) { closeBg.sprite = uiSpr; closeBg.type = Image.Type.Sliced; }
        refs.btnClose = closeGo.AddComponent<Button>();
        refs.btnClose.targetGraphic = closeBg;
        closeGo.AddComponent<ButtonPressEffect>();
        if (sprClose != null)
            MakeImg(closeGo.transform, "Icon", new Vector2(24f, 24f), Vector2.zero, Color.white, sprClose);

        // ── Encabezado: beaker + título ───────────────────────────────────────
        // Beaker icon: offset izquierdo del título
        MakeImg(inner.transform, "BeakerIcon",
            new Vector2(72f, 72f), new Vector2(-186f, 248f), Color.white, sprBeaker);

        // Título
        MakeTMP(inner.transform, fnt, "TitleText", "ChemiTech",
            new Vector2(360f, 90f), new Vector2(44f, 248f),
            68f, Color.white, FontStyles.Bold, 3f);

        // ── Etiqueta Correo ───────────────────────────────────────────────────
        MakeTMP(inner.transform, fnt, "LabelEmail", "Correo electrónico",
            new Vector2(300f, 36f), new Vector2(-232f, 145f),
            24f, Hex("A2A2A2"), FontStyles.Normal, 0f);

        // ── Campo Email ───────────────────────────────────────────────────────
        refs.emailField = MakeInputField(inner.transform, fnt, uiSpr,
            "EmailField", "alex@chemitech.com",
            new Vector2(0f, 73f), sprEmail,
            TMP_InputField.ContentType.EmailAddress);

        // ── Etiqueta Contraseña ───────────────────────────────────────────────
        MakeTMP(inner.transform, fnt, "LabelPassword", "Contraseña",
            new Vector2(200f, 36f), new Vector2(-302f, -13f),
            24f, Hex("A2A2A2"), FontStyles.Normal, 0f);

        // ── Campo Contraseña ──────────────────────────────────────────────────
        refs.passwordField = MakeInputField(inner.transform, fnt, uiSpr,
            "PasswordField", "••••••••",
            new Vector2(0f, -86f), sprLock,
            TMP_InputField.ContentType.Password);

        // Toggle mostrar/ocultar contraseña
        var toggleGo = MakeEmpty(refs.passwordField.transform, "BtnToggle");
        RT(toggleGo).anchoredPosition = new Vector2(358f, 0f);
        RT(toggleGo).sizeDelta        = new Vector2(40f, 40f);
        var toggleImg = toggleGo.AddComponent<Image>();
        toggleImg.color = new Color(1,1,1,0.6f);
        if (sprEye != null) toggleImg.sprite = sprEye;
        refs.btnToggle = toggleGo.AddComponent<Button>();
        refs.btnToggle.targetGraphic = toggleImg;

        // ── ¿Olvidaste tu contraseña? ─────────────────────────────────────────
        var forgotGo = MakeEmpty(inner.transform, "BtnOlvidaste");
        RT(forgotGo).anchoredPosition = new Vector2(210f, -167f);
        RT(forgotGo).sizeDelta        = new Vector2(270f, 40f);
        var forgotTxt = forgotGo.AddComponent<TextMeshProUGUI>();
        forgotTxt.text             = "¿Olvidaste tu contraseña?";
        forgotTxt.font             = fnt;
        forgotTxt.fontSize         = 22f;
        forgotTxt.color            = Hex("B7EFFF");
        forgotTxt.alignment        = TextAlignmentOptions.Right;
        forgotTxt.fontStyle        = FontStyles.Normal;
        forgotTxt.overflowMode     = TextOverflowModes.Overflow;
        forgotGo.AddComponent<Image>().color = Color.clear;
        refs.btnForgot = forgotGo.AddComponent<Button>();
        refs.btnForgot.targetGraphic = forgotGo.GetComponent<Image>();

        // ── Botón Iniciar Sesión ──────────────────────────────────────────────
        var loginGo = MakeEmpty(inner.transform, "BtnIniciarSesion");
        RT(loginGo).anchoredPosition = new Vector2(0f, -233f);
        RT(loginGo).sizeDelta        = new Vector2(INPUT_W, 68f);
        var loginBg = loginGo.AddComponent<Image>();
        loginBg.sprite = sprCyan;
        loginBg.type   = sprCyan != null ? Image.Type.Sliced : Image.Type.Simple;
        loginBg.color  = Color.white;
        refs.btnLogin  = loginGo.AddComponent<Button>();
        refs.btnLogin.targetGraphic = loginBg;
        loginGo.AddComponent<ButtonPressEffect>();
        MakeTMP(loginGo.transform, fnt, "Label", "Iniciar Sesión",
            new Vector2(INPUT_W, 68f), Vector2.zero,
            44f, Hex("0A2F44"), FontStyles.Bold, 0.5f);

        // ── ¿No tienes cuenta? Regístrate ────────────────────────────────────
        var regGo = MakeEmpty(inner.transform, "BtnRegistrate");
        RT(regGo).anchoredPosition = new Vector2(0f, -308f);
        RT(regGo).sizeDelta        = new Vector2(400f, 44f);
        var regTxt = regGo.AddComponent<TextMeshProUGUI>();
        regTxt.text         = "¿No tienes cuenta? <color=#FFD23F><b>Regístrate</b></color>";
        regTxt.font         = fnt;
        regTxt.fontSize     = 22f;
        regTxt.color        = new Color(1f, 1f, 1f, 0.8f);
        regTxt.alignment    = TextAlignmentOptions.Center;
        regTxt.richText     = true;
        regTxt.overflowMode = TextOverflowModes.Overflow;
        regGo.AddComponent<Image>().color = Color.clear;
        refs.btnRegister = regGo.AddComponent<Button>();
        refs.btnRegister.targetGraphic = regGo.GetComponent<Image>();

        return refs;
    }

    // ── Crea un TMP_InputField con ícono ──────────────────────────────────────
    static TMP_InputField MakeInputField(Transform parent, TMP_FontAsset fnt, Sprite uiSpr,
        string name, string placeholder,
        Vector2 pos, Sprite iconSpr,
        TMP_InputField.ContentType contentType)
    {
        var go = MakeEmpty(parent, name);
        RT(go).anchoredPosition = pos;
        RT(go).sizeDelta        = new Vector2(INPUT_W, INPUT_H);

        // Fondo del field
        var bgImg = go.AddComponent<Image>();
        bgImg.color  = Hex("0D1238");
        bgImg.sprite = uiSpr;
        bgImg.type   = Image.Type.Sliced;

        // Ícono izquierdo
        if (iconSpr != null)
            MakeImg(go.transform, "Icon", new Vector2(36f, 36f),
                new Vector2(-350f, 0f), Color.white, iconSpr);

        // Viewport
        var viewport = MakeEmpty(go.transform, "TextViewport");
        var vpRT = RT(viewport);
        vpRT.anchorMin  = Vector2.zero;
        vpRT.anchorMax  = Vector2.one;
        vpRT.offsetMin  = new Vector2(iconSpr != null ? 74f : 20f, 8f);
        vpRT.offsetMax  = new Vector2(-20f, -8f);
        viewport.AddComponent<RectMask2D>();

        // Texto placeholder
        var phGo = MakeEmpty(viewport.transform, "Placeholder");
        RT(phGo).anchorMin = Vector2.zero;
        RT(phGo).anchorMax = Vector2.one;
        RT(phGo).offsetMin = RT(phGo).offsetMax = Vector2.zero;
        var phTmp = phGo.AddComponent<TextMeshProUGUI>();
        phTmp.text             = placeholder;
        phTmp.font             = fnt;
        phTmp.fontSize         = 30f;
        phTmp.color            = new Color(1f, 1f, 1f, 0.35f);
        phTmp.alignment        = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        phTmp.enableWordWrapping = false;

        // Texto real
        var txtGo = MakeEmpty(viewport.transform, "Text");
        RT(txtGo).anchorMin = Vector2.zero;
        RT(txtGo).anchorMax = Vector2.one;
        RT(txtGo).offsetMin = RT(txtGo).offsetMax = Vector2.zero;
        var txtTmp = txtGo.AddComponent<TextMeshProUGUI>();
        txtTmp.text             = "";
        txtTmp.font             = fnt;
        txtTmp.fontSize         = 30f;
        txtTmp.color            = Color.white;
        txtTmp.alignment        = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        txtTmp.enableWordWrapping = false;

        // TMP_InputField
        var inputField = go.AddComponent<TMP_InputField>();
        inputField.textViewport   = vpRT;
        inputField.textComponent  = txtTmp;
        inputField.placeholder    = phTmp;
        inputField.contentType    = contentType;
        inputField.targetGraphic  = bgImg;
        inputField.fontAsset      = fnt;
        inputField.pointSize      = 30f;

        var ic = inputField.colors;
        ic.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        ic.pressedColor     = new Color(1f, 1f, 1f, 0.06f);
        ic.selectedColor    = new Color(0.4f, 0.6f, 1f, 0.2f);
        inputField.colors   = ic;

        return inputField;
    }

    // ── Átomo decorativo ──────────────────────────────────────────────────────
    static void MakeAtom(Transform parent, TMP_FontAsset fnt, Sprite circleSpr,
        string name, string letter, Vector2 pos, float size, float rot,
        Color bgColor, Color txtColor)
    {
        var go = MakeEmpty(parent, name);
        RT(go).anchoredPosition = pos;
        RT(go).sizeDelta        = new Vector2(size, size);
        RT(go).localEulerAngles = new Vector3(0f, 0f, rot);

        var bg = MakeImg(go.transform, "BG", new Vector2(size, size), Vector2.zero, bgColor, circleSpr);
        bg.GetComponent<Image>().type = Image.Type.Simple;

        MakeTMP(go.transform, fnt, "Letter", letter,
            new Vector2(size, size), Vector2.zero,
            size * 0.44f, txtColor, FontStyles.Bold, 0f);

        go.AddComponent<FloatingAtom>();
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
}
