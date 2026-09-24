using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Construye ClaseEsperaScene: la sala de espera del alumno.
///
/// Deliberadamente sobria: un título, un mensaje y el estado de la conexión. Nada de
/// la lección puede asomarse aquí (ver ClaseEsperaManager).
///
/// Recrea el Canvas en cada corrida, igual que MisClasesBuilder: mientras se itera el
/// diseño es lo predecible. Cuando la pantalla esté aprobada, congelarla y extenderla
/// con herramientas aditivas.
///
/// Menú: ChemiTech → Build Clase Espera Scene
/// </summary>
public static class ClaseEsperaBuilder
{
    const string ScenePath = "Assets/Scenes/ClaseEsperaScene.unity";

    static readonly Color BG     = Hex("1E2050");
    static readonly Color PANEL  = Hex("33356B");
    static readonly Color PURPLE = Hex("5A5FA5");
    static readonly Color ACCENT = Hex("4DD9E8");
    static readonly Color DIM    = Hex("A2A2A2");

    static TMP_FontAsset fnt;
    static Sprite        rounded;

    [MenuItem("ChemiTech/Build Clase Espera Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Clase Espera Scene",
                "Crea (o regenera) ClaseEsperaScene.\n\n" +
                "OJO: recrea el Canvas entero. Si ajustaste algo a mano ahí, se pierde.\n\n¿Continuar?",
                "Sí, continuar", "Cancelar"))
            return;

        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");

        var scene = OpenOrCreateScene();
        EnsureCamera(scene);
        EnsureEventSystem(scene);

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

        var root = canvasGo.transform;

        var bg = MakeEmpty(root, "Background"); Stretch(bg);
        var bgImg = bg.AddComponent<Image>(); bgImg.color = BG; bgImg.raycastTarget = false;

        // Cabecera con el nombre de la clase
        var header = MakeEmpty(root, "Header");
        var hRT = header.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f); hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot = new Vector2(0.5f, 1f);
        hRT.offsetMin = new Vector2(0f, -110f); hRT.offsetMax = Vector2.zero;
        var hImg = header.AddComponent<Image>(); hImg.color = PANEL; hImg.raycastTarget = false;

        var className = MakeText(header.transform, "ClassName", "Clase",
                                 new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 60f),
                                 34f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // Panel central: el mensaje de espera
        var panel = MakeEmpty(root, "Panel");
        SetRT(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(980f, 340f));
        var pImg = panel.AddComponent<Image>();
        pImg.sprite = rounded; pImg.type = Image.Type.Sliced; pImg.color = PANEL;

        MakeText(panel.transform, "Hint", "Sala de espera",
                 new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(800f, 48f),
                 24f, ACCENT, TextAlignmentOptions.Center, FontStyles.Bold);

        var message = MakeText(panel.transform, "Message",
                               "Espera a que tu docente inicie la clase.",
                               new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(860f, 160f),
                               32f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);

        // Estado de la conexión: el manager lo pinta verde o ámbar
        var connection = MakeText(root, "ConnectionLabel", "Conectado",
                                  new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(900f, 44f),
                                  22f, DIM, TextAlignmentOptions.Center, FontStyles.Normal);

        var btnSalir = MakeButton(root, "BtnSalir", "Salir de la clase", PURPLE,
                                  new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(420f, 64f), 26f);

        // Manager + cableado
        var mgrGo = new GameObject("ClaseEsperaManager", typeof(RectTransform));
        mgrGo.transform.SetParent(root, false);
        var mgr = mgrGo.AddComponent<ClaseEsperaManager>();

        var so = new SerializedObject(mgr);
        SetRef(so, "className",       className);
        SetRef(so, "message",         message);
        SetRef(so, "connectionLabel", connection);
        SetRef(so, "btnSalir",        btnSalir);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[ClaseEsperaBuilder] ✓ ClaseEsperaScene construida.");
        EditorUtility.DisplayDialog("¡Listo!",
            "ClaseEsperaScene construida y guardada.\n\n" +
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

    static void EnsureCamera(Scene scene)
    {
        if (FindRoot(scene, "Main Camera")) return;
        var go = new GameObject("Main Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.tag = "MainCamera";
        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BG;
        cam.orthographic = true;
    }

    static void EnsureEventSystem(Scene scene)
    {
        if (FindRoot(scene, "EventSystem")) return;
        var go = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
        SceneManager.MoveGameObjectToScene(go, scene);
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
        else Debug.LogWarning($"[ClaseEsperaBuilder] No existe la propiedad '{prop}' en el manager.");
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
