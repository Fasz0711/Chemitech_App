using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Construye ClaseDocenteScene: la misma escena 3D que ve el alumno, más el panel de
/// conducción (barra de acciones, resaltado, roster y control de la sesión).
///
/// La barra de resaltado se llena en runtime con un botón por elemento presente en la
/// escena, así que aquí solo se crea su plantilla.
///
/// Recrea el Canvas en cada corrida. Cuando esté aprobada, congelarla y extenderla con
/// herramientas aditivas.
///
/// Menú: ChemiTech → Build Clase Docente Scene
/// </summary>
public static class ClaseDocenteBuilder
{
    const string ScenePath = "Assets/Scenes/ClaseDocenteScene.unity";

    static readonly Color SKY    = Hex("0A1233");
    static readonly Color PANEL  = Hex("33356B");
    static readonly Color MODAL  = Hex("242659");
    static readonly Color PURPLE = Hex("5A5FA5");
    static readonly Color CYAN   = Hex("19A7CE");
    static readonly Color GREEN  = Hex("2ECC71");
    static readonly Color RED    = Hex("E74C3C");
    static readonly Color AMBER  = Hex("F5A623");
    static readonly Color DIM    = Hex("A2A2A2");
    static readonly Color ACCENT_TEXT = Hex("BFE9F2");

    static TMP_FontAsset fnt;
    static Sprite        rounded;

    [MenuItem("ChemiTech/Build Clase Docente Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Clase Docente Scene",
                "Crea (o regenera) ClaseDocenteScene.\n\n" +
                "OJO: recrea el Canvas entero. Si ajustaste el HUD a mano, se pierde.\n\n¿Continuar?",
                "Sí, continuar", "Cancelar"))
            return;

        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");

        var scene = OpenOrCreateScene();

        // ── Mundo 3D (idéntico a la escena del alumno) ────────────────────────
        var camGo = EnsureRoot(scene, "Main Camera");
        camGo.tag = "MainCamera";
        var cam = Ensure<Camera>(camGo);
        cam.orthographic    = false;
        cam.fieldOfView     = 52f;
        cam.nearClipPlane   = 0.1f;
        cam.farClipPlane    = 250f;
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = SKY;
        Ensure<AudioListener>(camGo);
        var orbit = Ensure<OrbitCameraController>(camGo);
        camGo.transform.SetPositionAndRotation(new Vector3(0f, 9f, -14f), Quaternion.Euler(28f, 0f, 0f));

        var lightGo = EnsureRoot(scene, "Directional Light");
        var light = Ensure<Light>(lightGo);
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = Hex("FFF3E0");
        lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        var sceneRoot = EnsureRoot(scene, "SceneRoot");
        sceneRoot.transform.position = Vector3.zero;

        var atomMat = GetOrCreateMat("Assets/Materials/Atom_Base.mat", Color.white, 0.1f, 0.45f);
        var bondMat = GetOrCreateMat("Assets/Materials/Bond.mat", Hex("C8CCD8"), 0f, 0.35f);

        // ── Canvas ────────────────────────────────────────────────────────────
        var oldCanvas = FindRoot(scene, "Canvas");
        if (oldCanvas) Object.DestroyImmediate(oldCanvas);

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                                      typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasGo, scene);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        EnsureEventSystem(scene);

        var root = canvasGo.transform;

        var catcher = MakeEmpty(root, "RotateCatcher"); Stretch(catcher);
        var cImg = catcher.AddComponent<Image>(); cImg.color = new Color(0f, 0f, 0f, 0f);
        catcher.AddComponent<DragRotateCatcher>().cam = orbit;

        // ── Cabecera ──────────────────────────────────────────────────────────
        var header = MakeEmpty(root, "Header");
        var hRT = header.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f); hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot = new Vector2(0.5f, 1f);
        hRT.offsetMin = new Vector2(0f, -96f); hRT.offsetMax = Vector2.zero;
        var hImg = header.AddComponent<Image>();
        hImg.color = new Color(PANEL.r, PANEL.g, PANEL.b, 0.92f);
        hImg.raycastTarget = false;

        var btnSalir = MakeButton(header.transform, "BtnSalir", "Salir", PURPLE,
                                  new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(160f, 56f), 23f);

        var className = MakeText(header.transform, "ClassName", "Clase",
                                 new Vector2(0f, 0.5f), new Vector2(340f, 12f), new Vector2(560f, 40f),
                                 27f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        var statusLabel = MakeText(header.transform, "StatusLabel", "Sin iniciar",
                                   new Vector2(0f, 0.5f), new Vector2(340f, -20f), new Vector2(620f, 34f),
                                   19f, DIM, TextAlignmentOptions.Left, FontStyles.Normal);

        var rosterLabel = MakeText(header.transform, "RosterLabel", "0 de 0 conectados",
                                   new Vector2(1f, 0.5f), new Vector2(-480f, 0f), new Vector2(380f, 44f),
                                   22f, CYAN, TextAlignmentOptions.Right, FontStyles.Bold);

        var btnIniciar  = MakeButton(header.transform, "BtnIniciar", "Iniciar clase", GREEN,
                                     new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(250f, 56f), 22f);
        var btnTerminar = MakeButton(header.transform, "BtnTerminar", "Terminar", RED,
                                     new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(250f, 56f), 22f);

        // ── Pista de escena vacía ─────────────────────────────────────────────
        var emptyHint = MakeEmpty(root, "EmptyHint");
        SetRT(emptyHint, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 90f));
        MakeText(emptyHint.transform, "Text", "La pizarra está vacía. Usa los botones de abajo.",
                 new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 80f),
                 26f, DIM, TextAlignmentOptions.Center, FontStyles.Normal);
        emptyHint.SetActive(false);

        // ── Barra de resaltado (se llena en runtime) ──────────────────────────
        var hlBar = MakeEmpty(root, "HighlightBar");
        var hlRT = hlBar.GetComponent<RectTransform>();
        hlRT.anchorMin = new Vector2(0f, 0f); hlRT.anchorMax = new Vector2(1f, 0f);
        hlRT.pivot = new Vector2(0.5f, 0f);
        hlRT.offsetMin = new Vector2(150f, 130f); hlRT.offsetMax = new Vector2(-150f, 200f);
        var hlLayout = hlBar.AddComponent<HorizontalLayoutGroup>();
        hlLayout.spacing = 10f;
        hlLayout.childControlWidth = false; hlLayout.childControlHeight = false;
        hlLayout.childForceExpandWidth = false; hlLayout.childForceExpandHeight = false;
        hlLayout.childAlignment = TextAnchor.MiddleCenter;

        var hlTemplate = MakeButton(hlBar.transform, "HighlightButtonTemplate", "O", AMBER,
                                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 58f), 24f);
        var hlLE = hlTemplate.gameObject.AddComponent<LayoutElement>();
        hlLE.preferredWidth = 96f; hlLE.preferredHeight = 58f;
        hlTemplate.gameObject.SetActive(false);

        var btnQuitar = MakeButton(root, "BtnQuitarResaltado", "Quitar resaltado", PURPLE,
                                   new Vector2(1f, 0f), new Vector2(-170f, 165f), new Vector2(280f, 58f), 21f);

        var btnSumar = MakeButton(root, "BtnModoSumar", "Resaltar: solo uno", AMBER,
                                  new Vector2(0f, 0f), new Vector2(190f, 165f), new Vector2(330f, 58f), 21f);
        var lblSumar = btnSumar.GetComponentInChildren<TextMeshProUGUI>(true);

        // ── Barra de acciones ─────────────────────────────────────────────────
        var bar = MakeEmpty(root, "ActionBar");
        var barRT = bar.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0f, 0f); barRT.anchorMax = new Vector2(1f, 0f);
        barRT.pivot = new Vector2(0.5f, 0f);
        barRT.offsetMin = new Vector2(0f, 0f); barRT.offsetMax = new Vector2(0f, 116f);
        var barImg = bar.AddComponent<Image>();
        barImg.color = new Color(PANEL.r, PANEL.g, PANEL.b, 0.94f);
        barImg.raycastTarget = false;

        var btnAgua    = MakeButton(bar.transform, "BtnAgua", "Agua", CYAN,
                                    new Vector2(0.5f, 0.5f), new Vector2(-740f, 0f), new Vector2(200f, 68f), 24f);
        var btnSal     = MakeButton(bar.transform, "BtnSal", "Sal", CYAN,
                                    new Vector2(0.5f, 0.5f), new Vector2(-530f, 0f), new Vector2(200f, 68f), 24f);
        var btnCO2     = MakeButton(bar.transform, "BtnCO2", "CO2", CYAN,
                                    new Vector2(0.5f, 0.5f), new Vector2(-320f, 0f), new Vector2(200f, 68f), 24f);

        // Cambia si los botones de arriba reemplazan la escena o suman a ella.
        var btnModo    = MakeButton(bar.transform, "BtnModoAgregar", "Modo: reemplazar", AMBER,
                                    new Vector2(0.5f, 0.5f), new Vector2(-60f, 0f), new Vector2(300f, 68f), 22f);
        var lblModo    = btnModo.GetComponentInChildren<TextMeshProUGUI>(true);

        var btnFijar   = MakeButton(bar.transform, "BtnFijarVista", "Fijar vista", PURPLE,
                                    new Vector2(0.5f, 0.5f), new Vector2(260f, 0f), new Vector2(260f, 68f), 24f);
        var lblFijar   = btnFijar.GetComponentInChildren<TextMeshProUGUI>(true);
        var btnLimpiar = MakeButton(bar.transform, "BtnLimpiar", "Limpiar", RED,
                                    new Vector2(0.5f, 0.5f), new Vector2(540f, 0f), new Vector2(200f, 68f), 24f);

        // ── Instrucción en lenguaje natural ───────────────────────────────────
        var promptRow = MakeEmpty(root, "PromptRow");
        var prRT = promptRow.GetComponent<RectTransform>();
        prRT.anchorMin = new Vector2(0f, 0f); prRT.anchorMax = new Vector2(1f, 0f);
        prRT.pivot = new Vector2(0.5f, 0f);
        prRT.offsetMin = new Vector2(150f, 214f); prRT.offsetMax = new Vector2(-150f, 286f);

        var inputPrompt = MakeInput(promptRow.transform, "InputPrompt",
                                    "Escribe una instrucción: \"muestra una molécula de agua\"",
                                    new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                                    stretchRight: 250f);
        var btnEnviar = MakeButton(promptRow.transform, "BtnEnviarPrompt", "Enviar", CYAN,
                                   new Vector2(1f, 0.5f), new Vector2(-115f, 0f), new Vector2(220f, 66f), 24f);

        // Aparece solo si la respuesta tarda; lo instantáneo no debe parpadear.
        var thinking = MakeEmpty(root, "ThinkingIndicator");
        SetRT(thinking, new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(460f, 54f));
        var thImg = thinking.AddComponent<Image>();
        thImg.sprite = rounded; thImg.type = Image.Type.Sliced; thImg.color = AMBER;
        MakeText(thinking.transform, "Text", "Pensando…",
                 new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 48f),
                 22f, Hex("1E2050"), TextAlignmentOptions.Center, FontStyles.Bold);
        thinking.SetActive(false);

        // ── Vista previa: nada llega a los alumnos hasta confirmar ────────────
        var preview = MakeEmpty(root, "PreviewPanel");
        SetRT(preview, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1000f, 420f));
        var pvImg = preview.AddComponent<Image>();
        pvImg.sprite = rounded; pvImg.type = Image.Type.Sliced; pvImg.color = MODAL;

        MakeText(preview.transform, "Title", "Esto es lo que va a pasar",
                 new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(900f, 50f),
                 30f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var previewText = MakeText(preview.transform, "PreviewText", "",
                                   new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(900f, 200f),
                                   24f, ACCENT_TEXT, TextAlignmentOptions.Center, FontStyles.Normal);

        var btnDiscard = MakeButton(preview.transform, "BtnPreviewDiscard", "Descartar", PURPLE,
                                    new Vector2(0.5f, 0f), new Vector2(-190f, 56f), new Vector2(320f, 66f), 25f);
        var btnConfirm = MakeButton(preview.transform, "BtnPreviewConfirm", "Confirmar", GREEN,
                                    new Vector2(0.5f, 0f), new Vector2(190f, 56f), new Vector2(320f, 66f), 25f);
        preview.SetActive(false);

        // ── Modal de terminar ─────────────────────────────────────────────────
        var stopModal = MakeEmpty(root, "StopModal"); Stretch(stopModal);
        var dim = stopModal.AddComponent<Image>(); dim.color = new Color(0f, 0f, 0f, 0.72f);
        var stopPanel = MakeEmpty(stopModal.transform, "Panel");
        SetRT(stopPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 380f));
        var spImg = stopPanel.AddComponent<Image>();
        spImg.sprite = rounded; spImg.type = Image.Type.Sliced; spImg.color = MODAL;

        MakeText(stopPanel.transform, "Title", "¿Terminar la clase?",
                 new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(700f, 52f),
                 34f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        MakeText(stopPanel.transform, "Message",
                 "Esto es definitivo: la clase no se puede volver a abrir.",
                 new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(700f, 120f),
                 23f, DIM, TextAlignmentOptions.Center, FontStyles.Normal);

        var btnStopCancel  = MakeButton(stopPanel.transform, "BtnStopCancel", "Cancelar", PURPLE,
                                        new Vector2(0.5f, 0f), new Vector2(-180f, 58f), new Vector2(300f, 64f), 26f);
        var btnStopConfirm = MakeButton(stopPanel.transform, "BtnStopConfirm", "Sí, terminar", RED,
                                        new Vector2(0.5f, 0f), new Vector2(180f, 58f), new Vector2(300f, 64f), 26f);
        stopModal.SetActive(false);

        // ── Aviso ─────────────────────────────────────────────────────────────
        var notice = MakeEmpty(root, "Notice");
        SetRT(notice, new Vector2(0.5f, 0f), new Vector2(0f, 240f), new Vector2(1000f, 72f));
        var nImg = notice.AddComponent<Image>();
        nImg.sprite = rounded; nImg.type = Image.Type.Sliced; nImg.color = Hex("C0392B");
        var noticeText = MakeText(notice.transform, "NoticeText", "",
                                  new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 64f),
                                  22f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        notice.SetActive(false);

        // ── Manager + cableado ────────────────────────────────────────────────
        var mgrGo = FindRoot(scene, "ClaseDocenteManager");
        if (mgrGo) Object.DestroyImmediate(mgrGo);
        mgrGo = new GameObject("ClaseDocenteManager");
        SceneManager.MoveGameObjectToScene(mgrGo, scene);
        var mgr = mgrGo.AddComponent<ClaseDocenteManager>();

        var so = new SerializedObject(mgr);
        SetRef(so, "cam",                     orbit);
        SetRef(so, "sceneRoot",               sceneRoot.transform);
        SetRef(so, "atomMaterial",            atomMat);
        SetRef(so, "bondMaterial",            bondMat);
        SetRef(so, "className",               className);
        SetRef(so, "rosterLabel",             rosterLabel);
        SetRef(so, "statusLabel",             statusLabel);
        SetRef(so, "emptyHint",               emptyHint);
        SetRef(so, "btnSalir",                btnSalir);
        SetRef(so, "btnAgua",                 btnAgua);
        SetRef(so, "btnSal",                  btnSal);
        SetRef(so, "btnCO2",                  btnCO2);
        SetRef(so, "btnLimpiar",              btnLimpiar);
        SetRef(so, "btnFijarVista",           btnFijar);
        SetRef(so, "lblFijarVista",           lblFijar);
        SetRef(so, "btnModoAgregar",          btnModo);
        SetRef(so, "lblModoAgregar",          lblModo);
        SetRef(so, "highlightBar",            hlBar.GetComponent<RectTransform>());
        SetRef(so, "highlightButtonTemplate", hlTemplate.gameObject);
        SetRef(so, "btnQuitarResaltado",      btnQuitar);
        SetRef(so, "btnModoSumar",            btnSumar);
        SetRef(so, "lblModoSumar",            lblSumar);
        SetRef(so, "thinkingIndicator",       thinking);
        SetRef(so, "btnIniciar",              btnIniciar);
        SetRef(so, "btnTerminar",             btnTerminar);
        SetRef(so, "stopModal",               stopModal);
        SetRef(so, "btnStopConfirm",          btnStopConfirm);
        SetRef(so, "btnStopCancel",           btnStopCancel);
        SetRef(so, "inputPrompt",             inputPrompt);
        SetRef(so, "btnEnviarPrompt",         btnEnviar);
        SetRef(so, "previewPanel",            preview);
        SetRef(so, "previewText",             previewText);
        SetRef(so, "btnPreviewConfirm",       btnConfirm);
        SetRef(so, "btnPreviewDiscard",       btnDiscard);
        SetRef(so, "noticeRoot",              notice);
        SetRef(so, "noticeText",              noticeText);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[ClaseDocenteBuilder] ✓ ClaseDocenteScene construida.");
        EditorUtility.DisplayDialog("¡Listo!",
            "ClaseDocenteScene construida.\n\n" +
            "Recuerda: File → Build Settings → Add Open Scenes.", "OK");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static Button MakeButton(Transform parent, string name, string label, Color color,
                             Vector2 anchor, Vector2 pos, Vector2 size, float fontSize)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, anchor, pos, size);
        var img = go.AddComponent<Image>();
        img.sprite = rounded; img.type = Image.Type.Sliced; img.color = color;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;

        var lbl = MakeEmpty(go.transform, "Label"); Stretch(lbl);
        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.font = fnt; tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return btn;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text,
                                    Vector2 anchor, Vector2 pos, Vector2 size,
                                    float fontSize, Color color,
                                    TextAlignmentOptions align, FontStyles style)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, anchor, pos, size);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = fnt; tmp.fontSize = fontSize;
        tmp.fontStyle = style; tmp.color = color; tmp.alignment = align;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>Campo de texto. 'stretchRight' deja ese hueco a la derecha para el botón
    /// de enviar, en vez de fijar un ancho que se rompería al cambiar la resolución.</summary>
    static TMP_InputField MakeInput(Transform parent, string name, string placeholder,
                                    Vector2 anchor, Vector2 pos, Vector2 size,
                                    float stretchRight = 0f)
    {
        var go = MakeEmpty(parent, name);
        if (stretchRight > 0f)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(-stretchRight, 0f);
        }
        else SetRT(go, anchor, pos, size);

        var img = go.AddComponent<Image>();
        img.sprite = rounded; img.type = Image.Type.Sliced; img.color = Hex("141738");

        var input = go.AddComponent<TMP_InputField>();

        var area = MakeEmpty(go.transform, "Text Area");
        var aRT = area.GetComponent<RectTransform>();
        aRT.anchorMin = Vector2.zero; aRT.anchorMax = Vector2.one;
        aRT.offsetMin = new Vector2(20f, 6f); aRT.offsetMax = new Vector2(-20f, -6f);
        area.AddComponent<RectMask2D>();

        var phGo = MakeEmpty(area.transform, "Placeholder"); Stretch(phGo);
        var ph = phGo.AddComponent<TextMeshProUGUI>();
        ph.text = placeholder; ph.font = fnt; ph.fontSize = 22f;
        ph.color = new Color(1f, 1f, 1f, 0.35f);
        ph.alignment = TextAlignmentOptions.Left; ph.raycastTarget = false;

        var txtGo = MakeEmpty(area.transform, "Text"); Stretch(txtGo);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text = ""; txt.font = fnt; txt.fontSize = 22f;
        txt.color = Color.white; txt.alignment = TextAlignmentOptions.Left;
        txt.raycastTarget = false;

        input.textViewport  = aRT;
        input.textComponent = txt;
        input.placeholder   = ph;
        input.targetGraphic = img;
        input.lineType      = TMP_InputField.LineType.SingleLine;
        return input;
    }

    static Material GetOrCreateMat(string path, Color color, float metallic, float smoothness)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(shader);
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Glossiness", smoothness);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Scene OpenOrCreateScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.path == ScenePath) return active;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return active;

        if (System.IO.File.Exists(ScenePath))
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        return scene;
    }

    static void EnsureEventSystem(Scene scene)
    {
        if (FindRoot(scene, "EventSystem")) return;
        var go = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    static GameObject EnsureRoot(Scene scene, string name)
    {
        var go = FindRoot(scene, name);
        if (go) return go;
        go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c ? c : go.AddComponent<T>();
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static GameObject MakeEmpty(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void SetRT(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[ClaseDocenteBuilder] No existe la propiedad '{prop}'.");
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
