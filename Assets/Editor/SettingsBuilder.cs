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
    static Sprite rounded, circle, person, lockSpr, eyeSpr;

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

        LoadAssets();

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
        var cuentaLogged = MakeFill(pCuenta.transform, "CuentaLoggedView");
        MakeText(cuentaLogged.transform, "UsuarioLabel", "Usuario", new Vector2(-600f, 140f), new Vector2(200f, 44f), 26f, CYAN, TextAlignmentOptions.Left, FontStyles.Bold);
        var usuarioValue = MakeText(cuentaLogged.transform, "UsuarioValue", "Jugador12345", new Vector2(-330f, 140f), new Vector2(500f, 44f), 26f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        MakeText(cuentaLogged.transform, "CorreoLabel", "Correo", new Vector2(-600f, 60f), new Vector2(200f, 44f), 26f, PINK, TextAlignmentOptions.Left, FontStyles.Bold);
        var correoValue = MakeText(cuentaLogged.transform, "CorreoValue", "alex@chemitech.com", new Vector2(-330f, 60f), new Vector2(560f, 44f), 26f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        var btnCerrar   = MakeButton(cuentaLogged.transform, "BtnCerrarSesion",      "Cerrar Sesión",       new Vector2(-440f, -90f), new Vector2(360f, 92f), CYAN, SEL_TXT);
        var btnCambiar  = MakeButton(cuentaLogged.transform, "BtnCambiarContrasena", "Cambiar Contraseña",  new Vector2(   0f, -90f), new Vector2(360f, 92f), CYAN, SEL_TXT);
        var btnEliminar = MakeButton(cuentaLogged.transform, "BtnEliminarCuenta",    "Eliminar Cuenta",     new Vector2( 440f, -90f), new Vector2(360f, 92f), Hex("E74C3C"), Color.white);

        var cuentaGuest = BuildCuentaGuestView(pCuenta.transform, out var guestLogin, out var guestRegister);
        cuentaGuest.SetActive(false);

        pAudio.SetActive(false);
        pCuenta.SetActive(false);

        // ── Modal Cerrar Sesión ───────────────────────────────────────────────
        var logoutModal = BuildLogoutModal(root.transform, out var btnLogoutCancel, out var btnLogoutConfirm);
        logoutModal.SetActive(false);

        // ── Modal Eliminar Cuenta ─────────────────────────────────────────────
        var deleteModal = BuildDeleteModal(root.transform, out var deleteInput, out var btnDeleteCancel, out var btnDeleteConfirm);
        deleteModal.SetActive(false);

        // ── Modales Cambiar Contraseña ────────────────────────────────────────
        var changePassword = BuildChangePassword(root.transform);

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
        so.FindProperty("cuentaLoggedView").objectReferenceValue     = cuentaLogged;
        so.FindProperty("cuentaGuestView").objectReferenceValue      = cuentaGuest;
        so.FindProperty("btnGuestLogin").objectReferenceValue        = guestLogin;
        so.FindProperty("btnGuestRegister").objectReferenceValue     = guestRegister;
        so.FindProperty("txtUsuario").objectReferenceValue           = usuarioValue;
        so.FindProperty("txtCorreo").objectReferenceValue            = correoValue;
        so.FindProperty("logoutModal").objectReferenceValue          = logoutModal;
        so.FindProperty("btnLogoutCancel").objectReferenceValue      = btnLogoutCancel;
        so.FindProperty("btnLogoutConfirm").objectReferenceValue     = btnLogoutConfirm;
        so.FindProperty("deleteModal").objectReferenceValue          = deleteModal;
        so.FindProperty("deleteInput").objectReferenceValue          = deleteInput;
        so.FindProperty("btnDeleteCancel").objectReferenceValue      = btnDeleteCancel;
        so.FindProperty("btnDeleteConfirm").objectReferenceValue     = btnDeleteConfirm;
        so.FindProperty("changePassword").objectReferenceValue       = changePassword;
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

    // Vista de invitado de la pestaña Cuenta: llamado a iniciar sesión / crear cuenta.
    static GameObject BuildCuentaGuestView(Transform parent, out Button btnLogin, out Button btnRegister)
    {
        var view = MakeFill(parent, "CuentaGuestView");
        var iconBox = MakePanel(view.transform, "IconBox", new Vector2(0f, 130f), new Vector2(110f, 110f), PURPLE);
        MakeImg(iconBox.transform, "Icon", new Vector2(58f, 58f), Vector2.zero, Color.white, person).GetComponent<Image>().preserveAspect = true;
        MakeText(view.transform, "Title", "Inicia sesión o crea una cuenta",
            new Vector2(0f, 40f), new Vector2(1000f, 56f), 36f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold, true);
        MakeText(view.transform, "Subtitle", "Guarda tu progreso y gestiona tu cuenta desde cualquier dispositivo.",
            new Vector2(0f, -25f), new Vector2(820f, 70f), 24f, GRAY, TextAlignmentOptions.Center, FontStyles.Normal, true);
        btnLogin    = MakeButton(view.transform, "BtnGuestLogin",    "Iniciar sesión", new Vector2(-200f, -120f), new Vector2(320f, 90f), Hex("C9CDF2"), SEL_TXT);
        btnRegister = MakeButton(view.transform, "BtnGuestRegister", "Crear cuenta",   new Vector2( 200f, -120f), new Vector2(320f, 90f), CYAN, SEL_TXT);
        return view;
    }

    // Agrega la vista de invitado a la pestaña Cuenta en la ESCENA ACTUAL, agrupando
    // el contenido logueado ya presente (aditivo, no destruye tus cambios manuales).
    [MenuItem("ChemiTech/Fix/Settings Cuenta Invitado")]
    static void FixCuentaInvitado()
    {
        LoadAssets();
        var canvas   = GameObject.Find("Canvas");
        var pCuenta  = canvas != null ? FindChildRecursive(canvas.transform, "PanelCuenta") : null;
        var mgr      = Object.FindObjectOfType<SettingsManager>();
        if (pCuenta == null || mgr == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró 'PanelCuenta' o 'SettingsManager' (¿abriste SettingsScene?).", "OK");
            return;
        }

        // 1) Agrupar lo logueado existente (solo una vez)
        var loggedT = FindChildRecursive(pCuenta, "CuentaLoggedView");
        if (loggedT == null)
        {
            var loggedGo = MakeFill(pCuenta, "CuentaLoggedView");
            loggedGo.transform.SetAsFirstSibling();
            var toMove = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in pCuenta)
                if (child.gameObject != loggedGo) toMove.Add(child);
            foreach (var t in toMove) t.SetParent(loggedGo.transform, true); // preserva posiciones
            loggedT = loggedGo.transform;
        }

        // 2) Crear (o reutilizar) la vista de invitado
        var guestT = FindChildRecursive(pCuenta, "CuentaGuestView");
        Button btnLogin, btnRegister;
        if (guestT == null)
            guestT = BuildCuentaGuestView(pCuenta, out btnLogin, out btnRegister).transform;
        else
        {
            btnLogin    = FindChildRecursive(guestT, "BtnGuestLogin")?.GetComponent<Button>();
            btnRegister = FindChildRecursive(guestT, "BtnGuestRegister")?.GetComponent<Button>();
        }
        guestT.gameObject.SetActive(false);

        // 3) Cablear
        var so = new SerializedObject(mgr);
        so.FindProperty("cuentaLoggedView").objectReferenceValue = loggedT.gameObject;
        so.FindProperty("cuentaGuestView").objectReferenceValue  = guestT.gameObject;
        so.FindProperty("btnGuestLogin").objectReferenceValue    = btnLogin;
        so.FindProperty("btnGuestRegister").objectReferenceValue = btnRegister;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[SettingsBuilder] ✓ Cuenta: vista de invitado agregada/cableada.");
        EditorUtility.DisplayDialog("¡Listo!",
            "Pestaña Cuenta: invitado verá el llamado a iniciar sesión / crear cuenta; logueado, su info.\nGuarda con Ctrl+S.", "OK");
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var r = FindChildRecursive(c, name);
            if (r) return r;
        }
        return null;
    }

    // Modal de confirmación de cierre de sesión.
    static GameObject BuildLogoutModal(Transform parent, out Button btnCancel, out Button btnConfirm)
    {
        var modal = MakeFill(parent, "LogoutModal");
        var dim = modal.AddComponent<Image>(); dim.color = new Color(0f, 0f, 0f, 0.62f);

        var panel = MakePanel(modal.transform, "Panel", Vector2.zero, new Vector2(640f, 360f), Hex("242659"));
        AddBorder(panel, Color.white, 1f);

        MakeText(panel.transform, "Title", "¿Cerrar Sesión?", new Vector2(0f, 110f), new Vector2(560f, 60f), 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        MakeText(panel.transform, "Body",
            "Volverás a la pantalla inicial. Podrás iniciar sesión otra vez con tu usuario y tus datos guardados no se perderán.",
            new Vector2(0f, 15f), new Vector2(540f, 120f), 24f, GRAY, TextAlignmentOptions.Center, FontStyles.Normal, true);
        btnCancel  = MakeButton(panel.transform, "BtnLogoutCancel",  "Cancelar",          new Vector2(-145f, -115f), new Vector2(240f, 84f), Hex("3A3B6B"), Color.white);
        btnConfirm = MakeButton(panel.transform, "BtnLogoutConfirm", "Si, Cerrar Sesión", new Vector2( 150f, -115f), new Vector2(290f, 84f), CYAN, SEL_TXT);
        return modal;
    }

    // Agrega el modal de cerrar sesión a la ESCENA ACTUAL (aditivo, sin destruir nada).
    [MenuItem("ChemiTech/Fix/Settings Logout Modal")]
    static void FixLogoutModal()
    {
        LoadAssets();
        var canvas = GameObject.Find("Canvas");
        var mgr    = Object.FindObjectOfType<SettingsManager>();
        if (canvas == null || mgr == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró 'Canvas' o 'SettingsManager' (¿abriste SettingsScene?).", "OK");
            return;
        }

        var rootT = FindChildRecursive(canvas.transform, "SettingsRoot");
        Transform parent = rootT != null ? rootT : canvas.transform;

        var existing = FindChildRecursive(parent, "LogoutModal");
        GameObject modal; Button cancel, confirm;
        if (existing != null)
        {
            modal   = existing.gameObject;
            cancel  = FindChildRecursive(modal.transform, "BtnLogoutCancel")?.GetComponent<Button>();
            confirm = FindChildRecursive(modal.transform, "BtnLogoutConfirm")?.GetComponent<Button>();
        }
        else modal = BuildLogoutModal(parent, out cancel, out confirm);
        modal.SetActive(false);

        var so = new SerializedObject(mgr);
        so.FindProperty("logoutModal").objectReferenceValue      = modal;
        so.FindProperty("btnLogoutCancel").objectReferenceValue  = cancel;
        so.FindProperty("btnLogoutConfirm").objectReferenceValue = confirm;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[SettingsBuilder] ✓ Modal de cerrar sesión agregado/cableado.");
        EditorUtility.DisplayDialog("¡Listo!",
            "Modal de Cerrar Sesión agregado y cableado.\nGuarda con Ctrl+S.", "OK");
    }

    // Modal de confirmación de eliminar cuenta (con campo "ELIMINAR").
    static GameObject BuildDeleteModal(Transform parent, out TMP_InputField input, out Button btnCancel, out Button btnConfirm)
    {
        var modal = MakeFill(parent, "DeleteAccountModal");
        var dim = modal.AddComponent<Image>(); dim.color = new Color(0f, 0f, 0f, 0.62f);

        var panel = MakePanel(modal.transform, "Panel", Vector2.zero, new Vector2(700f, 440f), Hex("242659"));
        AddBorder(panel, Color.white, 1f);

        MakeText(panel.transform, "Title", "¿Eliminar tu cuenta?", new Vector2(0f, 165f), new Vector2(600f, 60f), 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        MakeText(panel.transform, "Body",
            "Perderás <b>todos</b> tus universos, moléculas descubiertas y datos personales.\nEsta acción <color=#E74C3C><b>no se puede deshacer.</b></color>",
            new Vector2(0f, 82f), new Vector2(600f, 110f), 23f, GRAY, TextAlignmentOptions.Center, FontStyles.Normal, true);
        MakeText(panel.transform, "InputLabel", "Escribe <color=#E75480>ELIMINAR</color> para confirmar",
            new Vector2(-165f, 12f), new Vector2(520f, 32f), 20f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        input = MakeInputField(panel.transform, "DeleteInput", new Vector2(0f, -35f), new Vector2(580f, 66f));

        btnCancel  = MakeButton(panel.transform, "BtnDeleteCancel",  "Cancelar",     new Vector2(-165f, -140f), new Vector2(260f, 84f), Hex("3A3B6B"), Color.white);
        btnConfirm = MakeButton(panel.transform, "BtnDeleteConfirm", "Si, eliminar", new Vector2( 165f, -140f), new Vector2(280f, 84f), Hex("E74C3C"), Color.white);
        return modal;
    }

    static TMP_InputField MakeInputField(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = MakeEmpty(parent, name); SetRT(go, pos, size);
        var bg = go.AddComponent<Image>(); bg.sprite = rounded; bg.type = Image.Type.Sliced; bg.color = Hex("141738");

        var area = MakeEmpty(go.transform, "Text Area");
        var art = (RectTransform)area.transform;
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
        art.offsetMin = new Vector2(20f, 6f); art.offsetMax = new Vector2(-20f, -6f);
        area.AddComponent<RectMask2D>();

        var ph  = MakeStretchTMP(area.transform, "Placeholder", "", new Color(1f, 1f, 1f, 0.3f));
        var txt = MakeStretchTMP(area.transform, "Text", "", Color.white);

        var field = go.AddComponent<TMP_InputField>();
        var fso = new SerializedObject(field);
        fso.FindProperty("m_TextViewport").objectReferenceValue  = art;
        fso.FindProperty("m_TextComponent").objectReferenceValue = txt;
        fso.FindProperty("m_Placeholder").objectReferenceValue   = ph;
        fso.FindProperty("m_TargetGraphic").objectReferenceValue = bg;
        fso.ApplyModifiedProperties();
        field.customCaretColor = true; field.caretColor = Color.white; field.caretWidth = 2;
        return field;
    }

    static TextMeshProUGUI MakeStretchTMP(Transform parent, string name, string text, Color color)
    {
        var go = MakeEmpty(parent, name); Stretch(go);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = fnt; tmp.fontSize = 26f; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        tmp.enableWordWrapping = false; tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    // Agrega el modal de eliminar cuenta a la ESCENA ACTUAL (aditivo).
    [MenuItem("ChemiTech/Fix/Settings Delete Account Modal")]
    static void FixDeleteModal()
    {
        LoadAssets();
        var canvas = GameObject.Find("Canvas");
        var mgr    = Object.FindObjectOfType<SettingsManager>();
        if (canvas == null || mgr == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró 'Canvas' o 'SettingsManager' (¿abriste SettingsScene?).", "OK");
            return;
        }

        var rootT = FindChildRecursive(canvas.transform, "SettingsRoot");
        Transform parent = rootT != null ? rootT : canvas.transform;

        var existing = FindChildRecursive(parent, "DeleteAccountModal");
        GameObject modal; TMP_InputField input; Button cancel, confirm;
        if (existing != null)
        {
            modal   = existing.gameObject;
            input   = FindChildRecursive(modal.transform, "DeleteInput")?.GetComponent<TMP_InputField>();
            cancel  = FindChildRecursive(modal.transform, "BtnDeleteCancel")?.GetComponent<Button>();
            confirm = FindChildRecursive(modal.transform, "BtnDeleteConfirm")?.GetComponent<Button>();
        }
        else modal = BuildDeleteModal(parent, out input, out cancel, out confirm);
        modal.SetActive(false);

        var so = new SerializedObject(mgr);
        so.FindProperty("deleteModal").objectReferenceValue      = modal;
        so.FindProperty("deleteInput").objectReferenceValue      = input;
        so.FindProperty("btnDeleteCancel").objectReferenceValue  = cancel;
        so.FindProperty("btnDeleteConfirm").objectReferenceValue = confirm;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[SettingsBuilder] ✓ Modal de eliminar cuenta agregado/cableado.");
        EditorUtility.DisplayDialog("¡Listo!",
            "Modal de Eliminar Cuenta agregado y cableado.\nGuarda con Ctrl+S.", "OK");
    }

    // ══ Cambiar Contraseña (formulario + éxito + controller) ═══════════════════
    static ChangePasswordController BuildChangePassword(Transform parent)
    {
        var container = MakeFill(parent, "ChangePassword");
        var ctrl = container.AddComponent<ChangePasswordController>();

        // ── Formulario ────────────────────────────────────────────────────────
        var form = MakeFill(container.transform, "ChangePasswordForm");
        form.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
        var pf = MakePanel(form.transform, "Panel", Vector2.zero, new Vector2(720f, 720f), Hex("242659"));
        AddBorder(pf, Color.white, 1f);

        MakeText(pf.transform, "Title", "Cambia tu contraseña", new Vector2(0f, 316f), new Vector2(640f, 56f), 38f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var banner = MakePanel(pf.transform, "Banner", new Vector2(0f, 262f), new Vector2(662f, 46f), Hex("592330"));
        var bannerTxt = MakeText(banner.transform, "Text", "", Vector2.zero, new Vector2(630f, 42f), 18f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold, true);
        banner.gameObject.SetActive(false);

        MakeText(pf.transform, "LabelCurrent", "Contraseña actual", new Vector2(-320f, 222f), new Vector2(420f, 30f), 20f, GRAY, TextAlignmentOptions.Left, FontStyles.Normal);
        var fieldCurrent = MakePasswordField(pf.transform, new Vector2(0f, 180f), "Escribe tu contraseña actual", out var eyeCurrent);

        MakeText(pf.transform, "LabelNew", "Nueva Contraseña", new Vector2(-320f, 130f), new Vector2(420f, 30f), 20f, GRAY, TextAlignmentOptions.Left, FontStyles.Normal);
        var fieldNew = MakePasswordField(pf.transform, new Vector2(0f, 88f), "Escribe tu nueva contraseña", out var eyeNew);

        // Fuerza (5 segmentos + label)
        var strengthRow = MakeEmpty(pf.transform, "Strength"); SetRT(strengthRow, new Vector2(0f, 46f), new Vector2(620f, 14f));
        var segments = new Image[5];
        const float SEGW = 116f, SEGGAP = 10f;
        float segStart = -(5f * SEGW + 4f * SEGGAP) / 2f + SEGW / 2f;
        for (int i = 0; i < 5; i++)
        {
            var s = MakeImg(strengthRow.transform, "Seg" + i, new Vector2(SEGW, 10f), new Vector2(segStart + i * (SEGW + SEGGAP), 0f), Hex("3A3B6B"), rounded);
            segments[i] = s.GetComponent<Image>(); segments[i].type = Image.Type.Sliced;
        }
        var strengthLabel = MakeText(pf.transform, "StrengthLabel", "", new Vector2(-268f, 24f), new Vector2(220f, 26f), 18f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        // Checklist (oculto por defecto)
        var reqPanel = MakeEmpty(pf.transform, "ReqPanel"); SetRT(reqPanel, new Vector2(0f, -35f), new Vector2(640f, 84f));
        MakeReqItem(reqPanel.transform, "ReqLength", new Vector2(-160f, 20f),  out var rlBg, out var rlTxt);
        MakeReqItem(reqPanel.transform, "ReqUpper",  new Vector2( 160f, 20f),  out var ruBg, out var ruTxt);
        MakeReqItem(reqPanel.transform, "ReqNumber", new Vector2(-160f, -20f), out var rnBg, out var rnTxt);
        MakeReqItem(reqPanel.transform, "ReqSymbol", new Vector2( 160f, -20f), out var rsBg, out var rsTxt);
        reqPanel.SetActive(false);

        MakeText(pf.transform, "LabelConfirm", "Confirmar Nueva Contraseña", new Vector2(-320f, -100f), new Vector2(460f, 30f), 20f, GRAY, TextAlignmentOptions.Left, FontStyles.Normal);
        var fieldConfirm = MakePasswordField(pf.transform, new Vector2(0f, -142f), "Escribe tu nueva contraseña", out var eyeConfirm);

        var btnCancel = MakeButton(pf.transform, "BtnCancel", "Cancelar",           new Vector2(-165f, -250f), new Vector2(260f, 84f), Hex("3A3B6B"), Color.white);
        var btnSubmit = MakeButton(pf.transform, "BtnSubmit", "Cambiar Contraseña", new Vector2( 160f, -250f), new Vector2(320f, 84f), CYAN, SEL_TXT);
        form.SetActive(false);

        // ── Éxito ─────────────────────────────────────────────────────────────
        var success = MakeFill(container.transform, "ChangePasswordSuccess");
        success.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
        var ps = MakePanel(success.transform, "Panel", Vector2.zero, new Vector2(660f, 340f), Hex("242659"));
        AddBorder(ps, Color.white, 1f);
        MakeText(ps.transform, "Title", "¡Contraseña actualizada!", new Vector2(0f, 95f), new Vector2(600f, 56f), 36f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        MakeText(ps.transform, "Body", "Tu contraseña se cambió correctamente. Tu sesión sigue activa, no necesitas iniciar sesión otra vez.",
            new Vector2(0f, 12f), new Vector2(560f, 100f), 23f, GRAY, TextAlignmentOptions.Center, FontStyles.Normal, true);
        var btnSuccessBack = MakeButton(ps.transform, "BtnSuccessBack", "Volver a ajustes", new Vector2(0f, -95f), new Vector2(360f, 84f), CYAN, SEL_TXT);
        success.SetActive(false);

        // ── Cablear controller ────────────────────────────────────────────────
        var so = new SerializedObject(ctrl);
        so.FindProperty("formModal").objectReferenceValue     = form;
        so.FindProperty("successModal").objectReferenceValue  = success;
        so.FindProperty("inputCurrent").objectReferenceValue  = fieldCurrent;
        so.FindProperty("inputNew").objectReferenceValue      = fieldNew;
        so.FindProperty("inputConfirm").objectReferenceValue  = fieldConfirm;
        so.FindProperty("toggleCurrent").objectReferenceValue = eyeCurrent;
        so.FindProperty("toggleNew").objectReferenceValue     = eyeNew;
        so.FindProperty("toggleConfirm").objectReferenceValue = eyeConfirm;
        WireArray(so, "strengthSegments", segments);
        so.FindProperty("strengthLabel").objectReferenceValue = strengthLabel;
        so.FindProperty("reqPanel").objectReferenceValue      = reqPanel;
        so.FindProperty("reqLengthBg").objectReferenceValue   = rlBg;
        so.FindProperty("reqUpperBg").objectReferenceValue    = ruBg;
        so.FindProperty("reqNumberBg").objectReferenceValue   = rnBg;
        so.FindProperty("reqSymbolBg").objectReferenceValue   = rsBg;
        so.FindProperty("reqLengthTxt").objectReferenceValue  = rlTxt;
        so.FindProperty("reqUpperTxt").objectReferenceValue   = ruTxt;
        so.FindProperty("reqNumberTxt").objectReferenceValue  = rnTxt;
        so.FindProperty("reqSymbolTxt").objectReferenceValue  = rsTxt;
        so.FindProperty("banner").objectReferenceValue        = banner.gameObject;
        so.FindProperty("bannerTxt").objectReferenceValue     = bannerTxt;
        so.FindProperty("btnCancel").objectReferenceValue     = btnCancel;
        so.FindProperty("btnSubmit").objectReferenceValue     = btnSubmit;
        so.FindProperty("btnSuccessBack").objectReferenceValue= btnSuccessBack;
        so.ApplyModifiedProperties();

        return ctrl;
    }

    static TMP_InputField MakePasswordField(Transform parent, Vector2 pos, string placeholder, out Button eyeToggle)
    {
        const float W = 620f, H = 62f;
        var go = MakeEmpty(parent, "Field"); SetRT(go, pos, new Vector2(W, H));
        var bg = go.AddComponent<Image>(); bg.sprite = rounded; bg.type = Image.Type.Sliced; bg.color = Hex("141738");

        MakeImg(go.transform, "Lock", new Vector2(26f, 26f), new Vector2(-W / 2f + 34f, 0f), new Color(1f, 1f, 1f, 0.7f), lockSpr)
            .GetComponent<Image>().preserveAspect = true;

        var eyeGo = MakeEmpty(go.transform, "Eye"); SetRT(eyeGo, new Vector2(W / 2f - 34f, 0f), new Vector2(40f, 40f));
        var eyeImg = eyeGo.AddComponent<Image>(); eyeImg.sprite = eyeSpr; eyeImg.color = new Color(1f, 1f, 1f, 0.55f); eyeImg.preserveAspect = true;
        eyeToggle = eyeGo.AddComponent<Button>(); eyeToggle.targetGraphic = eyeImg;

        var area = MakeEmpty(go.transform, "Text Area");
        var art = (RectTransform)area.transform;
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
        art.offsetMin = new Vector2(58f, 6f); art.offsetMax = new Vector2(-58f, -6f);
        area.AddComponent<RectMask2D>();

        var ph  = MakeStretchTMP(area.transform, "Placeholder", placeholder, new Color(1f, 1f, 1f, 0.3f));
        var txt = MakeStretchTMP(area.transform, "Text", "", Color.white);

        var field = go.AddComponent<TMP_InputField>();
        var fso = new SerializedObject(field);
        fso.FindProperty("m_TextViewport").objectReferenceValue  = art;
        fso.FindProperty("m_TextComponent").objectReferenceValue = txt;
        fso.FindProperty("m_Placeholder").objectReferenceValue   = ph;
        fso.FindProperty("m_TargetGraphic").objectReferenceValue = bg;
        fso.ApplyModifiedProperties();
        field.contentType = TMP_InputField.ContentType.Password;
        field.customCaretColor = true; field.caretColor = Color.white; field.caretWidth = 2;
        return field;
    }

    static void MakeReqItem(Transform parent, string name, Vector2 pos, out Image bg, out TextMeshProUGUI txt)
    {
        bg = MakePanel(parent, name, pos, new Vector2(300f, 36f), new Color(1f, 1f, 1f, 0.10f));
        txt = MakeText(bg.transform, "Text", "○  requisito", new Vector2(6f, 0f), new Vector2(280f, 32f), 18f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
    }

    [MenuItem("ChemiTech/Fix/Settings Change Password")]
    static void FixChangePassword()
    {
        LoadAssets();
        var canvas = GameObject.Find("Canvas");
        var mgr    = Object.FindObjectOfType<SettingsManager>();
        if (canvas == null || mgr == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró 'Canvas' o 'SettingsManager' (¿abriste SettingsScene?).", "OK");
            return;
        }

        var rootT = FindChildRecursive(canvas.transform, "SettingsRoot");
        Transform parent = rootT != null ? rootT : canvas.transform;

        // Regenerar SIEMPRE el contenedor para garantizar el cableado interno
        // (destruye solo la UI de Cambiar Contraseña; todo lo demás queda intacto).
        var existing = FindChildRecursive(parent, "ChangePassword");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        ChangePasswordController ctrl = BuildChangePassword(parent);

        var so = new SerializedObject(mgr);
        so.FindProperty("changePassword").objectReferenceValue = ctrl;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[SettingsBuilder] ✓ Cambiar Contraseña regenerado y cableado.");
        EditorUtility.DisplayDialog("¡Listo!",
            "Modales de Cambiar Contraseña regenerados y cableados.\nGuarda con Ctrl+S.", "OK");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────
    static void LoadAssets()
    {
        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = Spr("Assets/Sprites/Login/rounded-panel.png");
        circle  = Spr("Assets/Sprites/AtomCircle.png");
        person  = Spr("Assets/Sprites/person-icon.png");
        lockSpr = Spr("Assets/Sprites/Login/icon-lock.png");
        eyeSpr  = Spr("Assets/Sprites/Login/icon-eye.png");
    }

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
