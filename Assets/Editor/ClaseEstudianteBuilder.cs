using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Construye ClaseEstudianteScene: la escena 3D de la clase, en solo lectura.
///
/// A diferencia de ZonaJuegoScene, aquí NO hay hotbar, selector, colocación ni
/// BondManager. Solo cámara, luz, un contenedor para el contenido y un HUD mínimo.
/// Reutiliza los mismos materiales de átomo y enlace que la zona de juego, para que
/// una molécula se vea igual en los dos sitios.
///
/// Recrea el Canvas y el HUD en cada corrida. Cuando la pantalla esté aprobada,
/// congelarla y extenderla con herramientas aditivas.
///
/// Menú: ChemiTech → Build Clase Estudiante Scene
/// </summary>
public static class ClaseEstudianteBuilder
{
    const string ScenePath = "Assets/Scenes/ClaseEstudianteScene.unity";

    static readonly Color SKY    = Hex("0A1233");
    static readonly Color PANEL  = Hex("33356B");
    static readonly Color PURPLE = Hex("5A5FA5");
    static readonly Color CYAN   = Hex("19A7CE");
    static readonly Color DIM    = Hex("A2A2A2");

    static TMP_FontAsset fnt;
    static Sprite        rounded;

    [MenuItem("ChemiTech/Build Clase Estudiante Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Clase Estudiante Scene",
                "Crea (o regenera) ClaseEstudianteScene.\n\n" +
                "OJO: recrea el Canvas entero. Si ajustaste el HUD a mano, se pierde.\n\n¿Continuar?",
                "Sí, continuar", "Cancelar"))
            return;

        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");

        var scene = OpenOrCreateScene();

        // ── Cámara 3D (los mismos valores que la zona de juego) ───────────────
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

        // ── Luz ───────────────────────────────────────────────────────────────
        var lightGo = EnsureRoot(scene, "Directional Light");
        var light = Ensure<Light>(lightGo);
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = Hex("FFF3E0");
        lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        // ── Contenedor del contenido: lo llena y lo vacía el renderer ─────────
        var sceneRoot = EnsureRoot(scene, "SceneRoot");
        sceneRoot.transform.position = Vector3.zero;

        // ── Materiales compartidos con la zona de juego ───────────────────────
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

        // Capa transparente que captura el arrastre para rotar la cámara. Va PRIMERO
        // para quedar detrás del HUD: los botones consumen sus propios eventos.
        var catcher = MakeEmpty(root, "RotateCatcher"); Stretch(catcher);
        var cImg = catcher.AddComponent<Image>(); cImg.color = new Color(0f, 0f, 0f, 0f);
        var drag = catcher.AddComponent<DragRotateCatcher>();
        drag.cam = orbit;

        // ── HUD ───────────────────────────────────────────────────────────────
        var header = MakeEmpty(root, "Header");
        var hRT = header.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f); hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot = new Vector2(0.5f, 1f);
        hRT.offsetMin = new Vector2(0f, -96f); hRT.offsetMax = Vector2.zero;
        var hImg = header.AddComponent<Image>();
        hImg.color = new Color(PANEL.r, PANEL.g, PANEL.b, 0.92f);
        hImg.raycastTarget = false;

        var className = MakeText(header.transform, "ClassName", "Clase",
                                 new Vector2(0f, 0.5f), new Vector2(330f, 0f), new Vector2(560f, 56f),
                                 30f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        var syncLabel = MakeText(header.transform, "SyncLabel", "Sincronizado",
                                 new Vector2(1f, 0.5f), new Vector2(-330f, 0f), new Vector2(520f, 44f),
                                 21f, DIM, TextAlignmentOptions.Right, FontStyles.Normal);

        var btnSalir = MakeButton(header.transform, "BtnSalir", "Salir", PURPLE,
                                  new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(160f, 56f), 23f);

        // Aviso de vista fijada por el docente
        var badge = MakeEmpty(root, "FollowingBadge");
        SetRT(badge, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(620f, 56f));
        var bImg = badge.AddComponent<Image>();
        bImg.sprite = rounded; bImg.type = Image.Type.Sliced; bImg.color = CYAN;
        MakeText(badge.transform, "Text", "Siguiendo la vista del docente",
                 new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 50f),
                 22f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        badge.SetActive(false);

        var btnVolver = MakeButton(root, "BtnVolverVista", "Volver a la vista del docente", PURPLE,
                                   new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(560f, 62f), 24f);

        // Pista de escena vacía: el docente todavía no puso nada
        var emptyHint = MakeEmpty(root, "EmptyHint");
        SetRT(emptyHint, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 90f));
        MakeText(emptyHint.transform, "Text", "Tu docente aún no ha puesto nada en la pizarra.",
                 new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 80f),
                 26f, DIM, TextAlignmentOptions.Center, FontStyles.Normal);
        emptyHint.SetActive(false);

        // ── Manager + cableado ────────────────────────────────────────────────
        var mgrGo = FindRoot(scene, "ClaseEstudianteManager");
        if (mgrGo) Object.DestroyImmediate(mgrGo);
        mgrGo = new GameObject("ClaseEstudianteManager");
        SceneManager.MoveGameObjectToScene(mgrGo, scene);
        var mgr = mgrGo.AddComponent<ClaseEstudianteManager>();

        var so = new SerializedObject(mgr);
        SetRef(so, "cam",            orbit);
        SetRef(so, "sceneRoot",      sceneRoot.transform);
        SetRef(so, "atomMaterial",   atomMat);
        SetRef(so, "bondMaterial",   bondMat);
        SetRef(so, "className",      className);
        SetRef(so, "syncLabel",      syncLabel);
        SetRef(so, "followingBadge", badge);
        SetRef(so, "btnVolverVista", btnVolver);
        SetRef(so, "btnSalir",       btnSalir);
        SetRef(so, "emptyHint",      emptyHint);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[ClaseEstudianteBuilder] ✓ ClaseEstudianteScene construida.");
        EditorUtility.DisplayDialog("¡Listo!",
            "ClaseEstudianteScene construida.\n\n" +
            "Recuerda: File → Build Settings → Add Open Scenes.\n" +
            "Sin eso, la sala de espera no puede saltar aquí.", "OK");
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
        else Debug.LogWarning($"[ClaseEstudianteBuilder] No existe la propiedad '{prop}'.");
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
