using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builder ADITIVO de MoleculeDetailScene ("Detalle de molécula").
/// Regenera solo el subárbol "DetailRoot" del Canvas; los objetos 3D
/// (MoleculeCamera / Directional Light / StageRoot) se crean idempotentes.
/// Panel izquierdo: datos de la molécula. Panel derecho: visor 3D (RenderTexture).
/// Menú: ChemiTech → Build Molecule Detail Scene / Rebuild → Molecule Detail Scene.
/// </summary>
public static class MoleculeDetailBuilder
{
    const string ScenePath = "Assets/Scenes/MoleculeDetailScene.unity";
    const float  RW = 1600f, RH = 900f;

    static TMP_FontAsset fnt;
    static Sprite   rounded, circle, person, bgSpr;
    static Material atomMat, bondMat;

    static readonly Color CYAN   = Hex("2FD2E0");
    static readonly Color PURPLE = Hex("8B5CF6");
    static readonly Color GRAY   = new Color(1f, 1f, 1f, 0.72f);

    [MenuItem("ChemiTech/Build Molecule Detail Scene")]
    public static void Build() => Build(false);

    [MenuItem("ChemiTech/Rebuild/Molecule Detail Scene (desde cero)")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Regenerar Detalle desde cero",
            "Esto DESTRUYE la UI del Detalle actual y la regenera por código.\n¿Continuar?",
            "Sí, regenerar", "Cancelar")) return;
        Build(true);
    }

    static void Build(bool force)
    {
        if (!force && !EditorUtility.DisplayDialog("Construir Molecule Detail Scene (aditivo)",
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
            if (existing != null && existing.transform.Find("DetailRoot") != null)
            {
                EditorUtility.DisplayDialog("Detalle ya existe",
                    "La UI del Detalle ya está en la escena. No se reconstruyó para no perder cambios manuales.\n\n" +
                    "Para regenerar: ChemiTech → Rebuild → Molecule Detail Scene (desde cero).", "OK");
                return;
            }
        }

        // ── Assets ────────────────────────────────────────────────────────────
        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = Spr("Assets/Sprites/Login/rounded-panel.png");
        circle  = Spr("Assets/Sprites/AtomCircle.png");
        person  = Spr("Assets/Sprites/person-icon.png");
        bgSpr   = Spr("Assets/Sprites/MainMenuBG.png");
        atomMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Atom_Base.mat");
        bondMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Bond.mat");

        // ── Escena 3D (visor): cámara dedicada + luz + stage ───────────────────
        var camGo = EnsureRoot(scene, "MoleculeCamera");
        camGo.tag = "MainCamera";
        var cam = Ensure<Camera>(camGo);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("0E1130");
        cam.orthographic = false; cam.fieldOfView = 40f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 100f;
        camGo.transform.position = new Vector3(0f, 0f, -8f);
        camGo.transform.rotation = Quaternion.identity;
        Ensure<AudioListener>(camGo);

        var lightGo = EnsureRoot(scene, "Directional Light");
        var light = Ensure<Light>(lightGo);
        light.type = LightType.Directional; light.intensity = 1.15f; light.color = Hex("FFF6E6");
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var stageGo = EnsureRoot(scene, "StageRoot");
        stageGo.transform.position = Vector3.zero;

        // ── EventSystem / Canvas (UI overlay) ──────────────────────────────────
        var esGo = EnsureRoot(scene, "EventSystem");
        Ensure<EventSystem>(esGo); Ensure<StandaloneInputModule>(esGo);

        var canvasGo = EnsureRoot(scene, "Canvas");
        var cv = Ensure<Canvas>(canvasGo); cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var csc = Ensure<CanvasScaler>(canvasGo);
        csc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        csc.referenceResolution = new Vector2(RW, RH); csc.matchWidthOrHeight = 0.5f;
        Ensure<GraphicRaycaster>(canvasGo);

        var oldRoot = canvasGo.transform.Find("DetailRoot");
        if (oldRoot != null) Object.DestroyImmediate(oldRoot.gameObject);
        var root = MakeFill(canvasGo.transform, "DetailRoot");

        var bg = MakeImg(root.transform, "Background", new Vector2(RW, RH), Vector2.zero, Color.white, bgSpr);
        Stretch(bg);

        // ── Header ──────────────────────────────────────────────────────────────
        var header = MakePanel(root.transform, "Header", new Vector2(0f, 376f), new Vector2(1480f, 100f), Hex("2E3270"));
        AddBorder(header, PURPLE, 0.5f);

        var backGo = MakePanel(header.transform, "BtnBack", new Vector2(-672f, 0f), new Vector2(76f, 76f), Hex("171A3E"));
        var btnBack = backGo.gameObject.AddComponent<Button>(); btnBack.targetGraphic = backGo;
        MakeText(backGo.transform, "Arrow", "<", Vector2.zero, new Vector2(76f, 76f), 48f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        MakeImg(header.transform, "AvatarRing", new Vector2(86f, 86f), new Vector2(-566f, 0f), CYAN, circle);
        var avatarGo = MakeImg(header.transform, "AvatarIcon", new Vector2(78f, 78f), new Vector2(-566f, 0f), Color.white, circle);
        MakeImg(avatarGo.transform, "Icon", new Vector2(44f, 44f), Vector2.zero, Color.white, person)
            .GetComponent<Image>().preserveAspect = true;
        HeaderAvatarTool.Setup(avatarGo);

        MakeText(header.transform, "Title", "Detalle de molécula", new Vector2(-120f, 0f), new Vector2(760f, 60f), 44f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        // ── Panel izquierdo (datos) ────────────────────────────────────────────
        var info = MakePanel(root.transform, "InfoPanel", new Vector2(-380f, -66f), new Vector2(700f, 660f), Hex("242659"));
        AddBorder(info, CYAN, 0.22f);

        MakeText(info.transform, "LblFormula", "FÓRMULA QUÍMICA", new Vector2(0f, 282f), new Vector2(600f, 26f), 18f, CYAN, TextAlignmentOptions.Left, FontStyles.Bold);
        var formulaTmp = MakeText(info.transform, "Formula", "", new Vector2(0f, 244f), new Vector2(600f, 56f), 42f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        MakeText(info.transform, "LblNombre", "NOMBRE", new Vector2(0f, 198f), new Vector2(600f, 24f), 18f, CYAN, TextAlignmentOptions.Left, FontStyles.Bold);
        var nameTmp = MakeText(info.transform, "Nombre", "", new Vector2(0f, 168f), new Vector2(600f, 36f), 28f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        MakeText(info.transform, "LblDesc", "DESCRIPCIÓN", new Vector2(0f, 128f), new Vector2(600f, 24f), 18f, CYAN, TextAlignmentOptions.Left, FontStyles.Bold);
        var descTmp = MakeText(info.transform, "Descripcion", "", new Vector2(0f, 90f), new Vector2(600f, 56f), 21f, Color.white, TextAlignmentOptions.Left, FontStyles.Normal, true);

        MakeText(info.transform, "LblComp", "COMPOSICIÓN", new Vector2(0f, 40f), new Vector2(600f, 24f), 18f, CYAN, TextAlignmentOptions.Left, FontStyles.Bold);
        var compRow = MakeEmpty(info.transform, "CompositionRow"); SetRT(compRow, new Vector2(0f, -4f), new Vector2(600f, 48f));
        var compRowRt = compRow.GetComponent<RectTransform>();

        var dateTmp = MakeTile(info.transform, "TileDate",   "DESCUBIERTA", new Vector2(-165f, -96f),  new Vector2(320f, 96f));
        var catTmp  = MakeTile(info.transform, "TileCat",    "CATEGORÍA",   new Vector2( 165f, -96f),  new Vector2(320f, 96f));
        var solTmp  = MakeTile(info.transform, "TileSol",    "Solubilidad", new Vector2(-165f, -208f), new Vector2(320f, 96f));
        var massTmp = MakeTile(info.transform, "TileMass",   "Masa Molar",  new Vector2( 165f, -208f), new Vector2(320f, 96f));

        // ── Panel derecho (visor 3D) ───────────────────────────────────────────
        var viewerPanel = MakePanel(root.transform, "ViewerPanel", new Vector2(380f, -66f), new Vector2(700f, 660f), Hex("141633"));
        AddBorder(viewerPanel, PURPLE, 0.35f);

        var rawGo = MakeEmpty(viewerPanel.transform, "Viewer"); SetRT(rawGo, new Vector2(0f, 26f), new Vector2(560f, 560f));
        var raw = rawGo.AddComponent<RawImage>(); raw.color = Color.white;
        var viewer = rawGo.AddComponent<MoleculeViewer3D>();

        MakeText(viewerPanel.transform, "Hint", "Toca y arrastra para rotar", new Vector2(0f, -296f), new Vector2(600f, 34f), 20f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Center, FontStyles.Normal);

        // ── Wiring: MoleculeViewer3D ───────────────────────────────────────────
        var vso = new SerializedObject(viewer);
        vso.FindProperty("cam").objectReferenceValue          = cam;
        vso.FindProperty("stageRoot").objectReferenceValue    = stageGo.transform;
        vso.FindProperty("atomMaterial").objectReferenceValue = atomMat;
        vso.FindProperty("bondMaterial").objectReferenceValue = bondMat;
        vso.FindProperty("labelFont").objectReferenceValue    = fnt;
        vso.FindProperty("target").objectReferenceValue       = raw;
        vso.ApplyModifiedProperties();

        // ── Wiring: MoleculeDetailManager ──────────────────────────────────────
        var mgrGo = EnsureRoot(scene, "MoleculeDetailManager");
        var mgr = Ensure<MoleculeDetailManager>(mgrGo);
        var so = new SerializedObject(mgr);
        so.FindProperty("btnBack").objectReferenceValue          = btnBack;
        so.FindProperty("formulaLabel").objectReferenceValue     = formulaTmp;
        so.FindProperty("nameLabel").objectReferenceValue        = nameTmp;
        so.FindProperty("descriptionLabel").objectReferenceValue = descTmp;
        so.FindProperty("compositionRow").objectReferenceValue   = compRowRt;
        so.FindProperty("dateValue").objectReferenceValue        = dateTmp;
        so.FindProperty("categoryValue").objectReferenceValue    = catTmp;
        so.FindProperty("solubilityValue").objectReferenceValue  = solTmp;
        so.FindProperty("molarMassValue").objectReferenceValue   = massTmp;
        so.FindProperty("atomCircle").objectReferenceValue       = circle;
        so.FindProperty("chipFont").objectReferenceValue         = fnt;
        so.FindProperty("viewer").objectReferenceValue           = viewer;
        so.ApplyModifiedProperties();

        // ── Guardar + Build Settings ──────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[MoleculeDetailBuilder] ✓ MoleculeDetailScene generada.");
        EditorUtility.DisplayDialog("¡Listo!",
            "MoleculeDetailScene generada y agregada a Build Settings.\n" +
            "Si hay error de Input System: ChemiTech → Fix → Input System.", "OK");
    }

    // ── Tile (título + valor) ─────────────────────────────────────────────────────
    static TextMeshProUGUI MakeTile(Transform parent, string name, string title, Vector2 pos, Vector2 size)
    {
        var tile = MakePanel(parent, name, pos, size, Hex("1B1E45"));
        AddBorder(tile, CYAN, 0.12f);
        MakeText(tile.transform, "Title", title, new Vector2(0f, size.y * 0.5f - 22f), new Vector2(size.x - 28f, 24f), 16f, CYAN, TextAlignmentOptions.Left, FontStyles.Bold);
        return MakeText(tile.transform, "Value", "", new Vector2(0f, -10f), new Vector2(size.x - 28f, 40f), 26f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────
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
        brt.anchorMin = rt.anchorMin; brt.anchorMax = rt.anchorMax; brt.pivot = rt.pivot;
        brt.anchoredPosition = rt.anchoredPosition;
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
