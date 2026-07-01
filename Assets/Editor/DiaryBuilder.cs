using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builder ADITIVO de DiaryScene ("Diario de Moléculas").
/// Regenera solo el subárbol "DiaryRoot". Header con avatar (HeaderAvatar),
/// badge alternable (Modo invitado / X Moléculas), vista bloqueada (invitado)
/// y vista vacía (con sesión).
/// Menú: ChemiTech → Build Diary Scene / Rebuild → Diary Scene (desde cero)
/// </summary>
public static class DiaryBuilder
{
    const string ScenePath = "Assets/Scenes/DiaryScene.unity";
    const float  RW = 1600f, RH = 900f;

    static TMP_FontAsset fnt;
    static Sprite rounded, circle, lockSpr, bookSpr, person;

    static readonly Color CYAN   = Hex("2FD2E0");
    static readonly Color PURPLE = Hex("8B5CF6");
    static readonly Color GRAY   = new Color(1f, 1f, 1f, 0.72f);

    [MenuItem("ChemiTech/Build Diary Scene")]
    public static void Build() => Build(false);

    [MenuItem("ChemiTech/Rebuild/Diary Scene (desde cero)")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Regenerar Diary desde cero",
            "Esto DESTRUYE la UI del Diario actual y la regenera por código.\n¿Continuar?",
            "Sí, regenerar", "Cancelar")) return;
        Build(true);
    }

    static void Build(bool force)
    {
        if (!force && !EditorUtility.DisplayDialog("Construir Diary Scene (aditivo)",
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
            if (existing != null && existing.transform.Find("DiaryRoot") != null)
            {
                EditorUtility.DisplayDialog("Diary ya existe",
                    "La UI del Diario ya está en la escena. No se reconstruyó para no perder cambios manuales.\n\n" +
                    "Para regenerar: ChemiTech → Rebuild → Diary Scene (desde cero).", "OK");
                return;
            }
        }

        // ── Assets ────────────────────────────────────────────────────────────
        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = Spr("Assets/Sprites/Login/rounded-panel.png");
        circle  = Spr("Assets/Sprites/AtomCircle.png");
        lockSpr = Spr("Assets/Sprites/Login/icon-lock.png");
        bookSpr = Spr("Assets/Sprites/diary-icon.png");
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

        var old = canvasGo.transform.Find("DiaryRoot");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = MakeFill(canvasGo.transform, "DiaryRoot");

        var bg = MakeImg(root.transform, "Background", new Vector2(RW, RH), Vector2.zero, Color.white,
            Spr("Assets/Sprites/MainMenuBG.png"));
        Stretch(bg);

        // ── Header ──────────────────────────────────────────────────────────────
        var header = MakePanel(root.transform, "Header", new Vector2(0f, 376f), new Vector2(1480f, 100f), Hex("2E3270"));
        AddBorder(header, PURPLE, 0.5f);

        var backGo = MakePanel(header.transform, "BtnBack", new Vector2(-672f, 0f), new Vector2(76f, 76f), Hex("171A3E"));
        var btnBack = backGo.gameObject.AddComponent<Button>(); btnBack.targetGraphic = backGo;
        MakeText(backGo.transform, "Arrow", "<", Vector2.zero, new Vector2(76f, 76f), 48f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // Avatar (ring cian + AvatarIcon) → HeaderAvatar vía la herramienta reutilizable
        MakeImg(header.transform, "AvatarRing", new Vector2(86f, 86f), new Vector2(-566f, 0f), CYAN, circle);
        var avatarGo = MakeImg(header.transform, "AvatarIcon", new Vector2(78f, 78f), new Vector2(-566f, 0f), Color.white, circle);
        MakeImg(avatarGo.transform, "Icon", new Vector2(44f, 44f), Vector2.zero, Color.white, person)
            .GetComponent<Image>().preserveAspect = true;
        HeaderAvatarTool.Setup(avatarGo);

        MakeText(header.transform, "Title", "Diario de Moléculas", new Vector2(-120f, 0f), new Vector2(760f, 60f), 44f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        // Badges (top-right, alternables)
        var guestBadge = MakePanel(header.transform, "GuestBadge", new Vector2(595f, 0f), new Vector2(240f, 54f), new Color(0.18f, 0.20f, 0.42f, 0.9f));
        AddBorder(guestBadge, new Color(1f, 1f, 1f, 0.25f), 1f);
        MakeImg(guestBadge.transform, "Icon", new Vector2(28f, 28f), new Vector2(-86f, 0f), GRAY, person).GetComponent<Image>().preserveAspect = true;
        MakeText(guestBadge.transform, "Label", "Modo invitado", new Vector2(18f, 0f), new Vector2(180f, 36f), 20f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var molBadge = MakePanel(header.transform, "MolBadge", new Vector2(545f, 0f), new Vector2(340f, 54f), Hex("F1C40F"));
        var molLabel = MakeText(molBadge.transform, "Label", "0 Moléculas Descubiertas", Vector2.zero, new Vector2(320f, 36f), 20f, Hex("2A2208"), TextAlignmentOptions.Center, FontStyles.Bold);

        // ── Panel principal ───────────────────────────────────────────────────
        var panel = MakePanel(root.transform, "MainPanel", new Vector2(0f, -60f), new Vector2(1480f, 720f), Hex("242659"));
        AddBorder(panel, CYAN, 0.22f);

        // ── Vista invitado (bloqueado) ────────────────────────────────────────
        var guestView = MakeFill(panel.transform, "GuestView");
        var lockBox = MakePanel(guestView.transform, "LockBox", new Vector2(0f, 175f), new Vector2(118f, 118f), PURPLE);
        MakeImg(lockBox.transform, "Lock", new Vector2(64f, 64f), Vector2.zero, Color.white, lockSpr).GetComponent<Image>().preserveAspect = true;
        MakeText(guestView.transform, "Title", "El diario es solo para exploradores con cuenta",
            new Vector2(0f, 55f), new Vector2(1040f, 60f), 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold, true);
        MakeText(guestView.transform, "Subtitle", "Crea una cuenta gratis para guardar tus moléculas descubiertas y verlas desde cualquier dispositivo.",
            new Vector2(0f, -18f), new Vector2(820f, 80f), 24f, GRAY, TextAlignmentOptions.Center, FontStyles.Normal, true);
        var btnLogin    = MakeButton(guestView.transform, "BtnIniciarSesion", "Iniciar sesión", new Vector2(-165f, -135f), new Vector2(300f, 92f), Hex("C9CDF2"), Hex("2A2D5A"));
        var btnRegister = MakeButton(guestView.transform, "BtnCrearCuenta",   "Crear cuenta",   new Vector2( 165f, -135f), new Vector2(300f, 92f), CYAN, Hex("0A2F44"));
        MakeText(guestView.transform, "FootNote", "También puedes seguir jugando sin guardar tu progreso",
            new Vector2(0f, -305f), new Vector2(900f, 36f), 18f, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Center, FontStyles.Normal);

        // ── Vista con sesión (vacío) ──────────────────────────────────────────
        var loggedView = MakeFill(panel.transform, "LoggedInView");
        MakeImg(loggedView.transform, "Book", new Vector2(130f, 130f), new Vector2(0f, 120f), Color.white, bookSpr).GetComponent<Image>().preserveAspect = true;
        MakeText(loggedView.transform, "Title", "¡Tu diario está vacío!",
            new Vector2(0f, 0f), new Vector2(900f, 60f), 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        MakeText(loggedView.transform, "Subtitle", "Combina átomos en tu universo para descubrir tus primeras moléculas",
            new Vector2(0f, -68f), new Vector2(780f, 70f), 24f, GRAY, TextAlignmentOptions.Center, FontStyles.Normal, true);
        var btnJugar = MakeButton(loggedView.transform, "BtnEmpezarJugar", "Empezar a Jugar", new Vector2(0f, -175f), new Vector2(400f, 92f), CYAN, Hex("0A2F44"));
        loggedView.SetActive(false);
        molBadge.gameObject.SetActive(false);

        // ── Manager ─────────────────────────────────────────────────────────────
        var mgrGo = EnsureRoot(scene, "DiaryManager");
        var mgr = Ensure<DiaryManager>(mgrGo);
        var so = new SerializedObject(mgr);
        so.FindProperty("btnBack").objectReferenceValue          = btnBack;
        so.FindProperty("guestBadge").objectReferenceValue       = guestBadge.gameObject;
        so.FindProperty("molCountBadge").objectReferenceValue    = molBadge.gameObject;
        so.FindProperty("molCountLabel").objectReferenceValue    = molLabel;
        so.FindProperty("guestView").objectReferenceValue        = guestView;
        so.FindProperty("loggedInView").objectReferenceValue     = loggedView;
        so.FindProperty("btnIniciarSesion").objectReferenceValue = btnLogin;
        so.FindProperty("btnCrearCuenta").objectReferenceValue   = btnRegister;
        so.FindProperty("btnEmpezarJugar").objectReferenceValue  = btnJugar;
        so.ApplyModifiedProperties();

        // ── Guardar + Build Settings ──────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[DiaryBuilder] ✓ DiaryScene generada.");
        EditorUtility.DisplayDialog("¡Listo!",
            "DiaryScene generada y agregada a Build Settings.\n" +
            "Si hay error de Input System: ChemiTech → Fix → Input System.", "OK");
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

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor, Color textColor)
    {
        var img = MakePanel(parent, name, pos, size, bgColor);
        var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        var cb = btn.colors; cb.highlightedColor = new Color(1f, 1f, 1f, 0.92f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f); btn.colors = cb;
        MakeText(img.transform, "Label", label, Vector2.zero, size, 30f, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
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
