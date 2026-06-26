using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builder ADITIVO de EditarUniversoScene ("Editar Universo").
/// Regenera solo el subárbol "EditarRoot" (no toca otras escenas/objetos).
/// Menú: ChemiTech → Build Editar Universo Scene
///        ChemiTech → Rebuild → Editar Universo Scene (desde cero)
/// </summary>
public static class EditarUniversoBuilder
{
    const string ScenePath = "Assets/Scenes/EditarUniversoScene.unity";
    const float  RW = 1600f, RH = 900f;
    const int    ICON_COUNT = 8, COLOR_COUNT = 6;

    static TMP_FontAsset fnt;
    static Sprite rounded, circle, ring, uiSpr;
    static Sprite[] universeIcons; // orden UniverseTheme.IconNames
    static Sprite[] avatarIcons;   // orden AvatarCatalog.IconNames

    static readonly Color CYAN = Hex("2FD2E0");
    static readonly Color RED  = Hex("E74C3C");
    static readonly string[] COLOR_HEX = { "4DD9E8", "E575B5", "9B59B6", "F1C40F", "2ECC71", "E74C3C" };

    [MenuItem("ChemiTech/Build Editar Universo Scene")]
    public static void Build() => Build(false);

    [MenuItem("ChemiTech/Rebuild/Editar Universo Scene (desde cero)")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Regenerar Editar Universo desde cero",
            "Esto DESTRUYE la UI de Editar Universo actual y la regenera por código.\n¿Continuar?",
            "Sí, regenerar", "Cancelar")) return;
        Build(true);
    }

    static void Build(bool force)
    {
        if (!force && !EditorUtility.DisplayDialog("Construir Editar Universo Scene (aditivo)",
            "Crea la escena si no existe. Si ya existe, NO se reconstruye\npara no perder cambios manuales.\n\n¿Continuar?",
            "Sí, continuar", "Cancelar")) return;

        // ── Escena ─────────────────────────────────────────────────────────────
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
            if (existing != null && existing.transform.Find("EditarRoot") != null)
            {
                EditorUtility.DisplayDialog("Editar Universo ya existe",
                    "La UI ya está en la escena. No se reconstruyó para no perder cambios manuales.\n\n" +
                    "Para regenerar: ChemiTech → Rebuild → Editar Universo Scene (desde cero).", "OK");
                return;
            }
        }

        // ── Assets ────────────────────────────────────────────────────────────
        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = Spr("Assets/Sprites/Login/rounded-panel.png");
        circle  = Spr("Assets/Sprites/AtomCircle.png");
        ring    = Spr("Assets/Sprites/orbit-ring.png");
        uiSpr   = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        universeIcons = LoadIcons(UniverseTheme.IconNames);
        avatarIcons   = LoadIcons(AvatarCatalog.IconNames);

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

        var old = canvasGo.transform.Find("EditarRoot");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = MakeFill(canvasGo.transform, "EditarRoot");

        var bg = MakeImg(root.transform, "Background", new Vector2(RW, RH), Vector2.zero, Color.white,
            Spr("Assets/Sprites/MainMenuBG.png"));
        Stretch(bg);

        // ── Header ──────────────────────────────────────────────────────────────
        var header = MakePanel(root.transform, "Header", new Vector2(0f, 376f), new Vector2(1480f, 100f), Hex("2E3270"));
        AddBorder(header, Hex("8B5CF6"), 0.5f);
        var backGo = MakePanel(header.transform, "BtnBack", new Vector2(-672f, 0f), new Vector2(76f, 76f), Hex("171A3E"));
        var btnAtras = backGo.gameObject.AddComponent<Button>(); btnAtras.targetGraphic = backGo;
        MakeText(backGo.transform, "Arrow", "<", Vector2.zero, new Vector2(76f, 76f), 48f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        BuildHeaderAvatar(header.transform, new Vector2(-566f, 0f));
        MakeText(header.transform, "Title", "Editar Universo", new Vector2(-120f, 0f), new Vector2(760f, 60f), 44f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        // ── Panel principal ───────────────────────────────────────────────────
        var panel = MakePanel(root.transform, "MainPanel", new Vector2(0f, -60f), new Vector2(1480f, 720f), Hex("242659"));
        AddBorder(panel, CYAN, 0.22f);

        // ── Preview (izquierda) ───────────────────────────────────────────────
        var prevCircle = BuildPreview(panel.transform, out var prevIcon, out var prevName);

        // ── Scroll (derecha) ──────────────────────────────────────────────────
        var content = MakeScroll(panel.transform, "RightScroll", new Vector2(240f, 0f), new Vector2(980f, 640f), 760f);

        float top = 0f;
        const float CW = 940f;

        // Nombre
        PlaceText(content, "NombreLabel", "Nombre del universo", ref top, 34f, 24f, new Color(1f,1f,1f,0.85f), TextAlignmentOptions.Left); top -= 6f;
        var nameField = BuildNameField(content, top, CW, out var counter); top -= 88f + 8f;
        var validation = PlaceText(content, "Validation",
            "El nombre no puede estar vacío ni usar caracteres como < > : \" | ?",
            ref top, 30f, 21f, Hex("F5B342"), TextAlignmentOptions.Left); top -= 14f;

        // Ícono
        PlaceText(content, "IconLabel", "Ícono", ref top, 32f, 24f, Color.white, TextAlignmentOptions.Left); top -= 4f;
        var iconBtns = new Button[ICON_COUNT];
        var iconBorders = new Image[ICON_COUNT];
        BuildIconRow(content, top, CW, iconBtns, iconBorders); top -= 70f + 18f;

        // Color
        PlaceText(content, "ColorLabel", "Color del tema", ref top, 32f, 24f, Color.white, TextAlignmentOptions.Left); top -= 4f;
        var colorBtns = new Button[COLOR_COUNT];
        var colorChecks = new TextMeshProUGUI[COLOR_COUNT];
        BuildColorRow(content, top, CW, colorBtns, colorChecks); top -= 64f + 22f;

        // Eliminar (sección)
        var btnEliminar = BuildDeleteSection(content, top, CW); top -= 120f + 24f;

        // Cancelar / Guardar Cambios
        var btnCancelar = MakeButton(content, "BtnCancelar", "Cancelar", new Vector2(-250f, 0f), new Vector2(280f, 88f), Hex("3A3B6B"), Color.white);
        SetTop(btnCancelar.gameObject, top, new Vector2(280f, 88f));
        var rtC = (RectTransform)btnCancelar.transform; rtC.anchoredPosition = new Vector2(-250f, top);
        var btnGuardar = MakeButton(content, "BtnGuardar", "Guardar Cambios", new Vector2(170f, 0f), new Vector2(420f, 88f), CYAN, Hex("0A2F44"));
        SetTop(btnGuardar.gameObject, top, new Vector2(420f, 88f));
        var rtG = (RectTransform)btnGuardar.transform; rtG.anchoredPosition = new Vector2(170f, top);

        // ── Modal eliminar (overlay, inactivo) ────────────────────────────────
        BuildDeleteModal(root.transform, out var deleteModal, out var btnModalCancelar, out var btnModalConfirmar);
        deleteModal.SetActive(false);

        // ── Manager ─────────────────────────────────────────────────────────────
        var mgrGo = EnsureRoot(scene, "EditarUniversoManager");
        var mgr = Ensure<EditarUniversoManager>(mgrGo);
        var so = new SerializedObject(mgr);
        so.FindProperty("inputName").objectReferenceValue        = nameField;
        so.FindProperty("counterLabel").objectReferenceValue     = counter;
        so.FindProperty("validationLabel").objectReferenceValue  = validation;
        so.FindProperty("previewCircle").objectReferenceValue    = prevCircle;
        so.FindProperty("previewIcon").objectReferenceValue      = prevIcon;
        so.FindProperty("previewNameLabel").objectReferenceValue = prevName;
        so.FindProperty("btnAtras").objectReferenceValue         = btnAtras;
        so.FindProperty("btnCancelar").objectReferenceValue      = btnCancelar;
        so.FindProperty("btnGuardar").objectReferenceValue       = btnGuardar;
        so.FindProperty("btnEliminar").objectReferenceValue      = btnEliminar;
        so.FindProperty("deleteModal").objectReferenceValue      = deleteModal;
        so.FindProperty("btnModalCancelar").objectReferenceValue = btnModalCancelar;
        so.FindProperty("btnModalConfirmar").objectReferenceValue= btnModalConfirmar;
        WireArray(so, "iconButtons",  iconBtns);
        WireArray(so, "iconBorders",  iconBorders);
        WireArray(so, "colorButtons", colorBtns);
        WireArray(so, "colorChecks",  colorChecks);
        so.ApplyModifiedProperties();

        // ── Guardar + Build Settings ──────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[EditarUniversoBuilder] ✓ EditarUniversoScene generada.");
        EditorUtility.DisplayDialog("¡Listo!",
            "EditarUniversoScene generada y agregada a Build Settings.\n" +
            "Si hay error de Input System: ChemiTech → Fix → Input System.", "OK");
    }

    // ── Preview ──────────────────────────────────────────────────────────────
    static Image BuildPreview(Transform panel, out Image prevIcon, out TextMeshProUGUI prevName)
    {
        var card = MakePanel(panel, "PreviewCard", new Vector2(-500f, 0f), new Vector2(420f, 656f), Hex("31346B"));
        AddBorder(card, Hex("8B5CF6"), 0.4f);
        MakeText(card.transform, "PreviewLabel", "VISTA PREVIA", new Vector2(0f, 262f), new Vector2(360f, 30f), 22f, CYAN, TextAlignmentOptions.Center, FontStyles.Bold);

        MakeImg(card.transform, "Orbit", new Vector2(330f, 330f), new Vector2(0f, 70f), new Color(1f, 1f, 1f, 0.18f), ring)
            .GetComponent<Image>().preserveAspect = true;

        var circ = MakeImg(card.transform, "PreviewCircle", new Vector2(250f, 250f), new Vector2(0f, 70f), CYAN, circle);
        var prevCircle = circ.GetComponent<Image>();
        prevIcon = MakeImg(circ.transform, "Inner", new Vector2(120f, 120f), Vector2.zero, Color.white, universeIcons[1]).GetComponent<Image>();
        prevIcon.preserveAspect = true;

        prevName = MakeText(card.transform, "PreviewName", "Universo 1", new Vector2(0f, -130f), new Vector2(380f, 50f), 32f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        return prevCircle;
    }

    // ── Campo de nombre (TMP_InputField) ───────────────────────────────────────
    static TMP_InputField BuildNameField(Transform parent, float top, float W, out TextMeshProUGUI counter)
    {
        const float H = 88f;
        var go = MakeEmpty(parent, "NameField");
        SetTop(go, top, new Vector2(W, H));
        var bg = go.AddComponent<Image>(); bg.sprite = uiSpr; bg.type = Image.Type.Sliced; bg.color = Hex("0D1238");

        MakeImg(go.transform, "Icon", new Vector2(34f, 34f), new Vector2(-W / 2f + 40f, 0f), new Color(1f, 1f, 1f, 0.85f), universeIcons[1])
            .GetComponent<Image>().preserveAspect = true;

        counter = MakeText(go.transform, "Counter", "0 / 25", new Vector2(W / 2f - 75f, 0f), new Vector2(120f, 30f), 22f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.Right, FontStyles.Normal);

        var area = MakeEmpty(go.transform, "Text Area");
        var art = (RectTransform)area.transform;
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
        art.offsetMin = new Vector2(74f, 8f); art.offsetMax = new Vector2(-150f, -8f);
        area.AddComponent<RectMask2D>();

        var ph  = MakeStretchTMP(area.transform, "Placeholder", "Nombre del universo", new Color(1f, 1f, 1f, 0.3f));
        var txt = MakeStretchTMP(area.transform, "Text", "", Color.white);

        var field = go.AddComponent<TMP_InputField>();
        var so = new SerializedObject(field);
        so.FindProperty("m_TextViewport").objectReferenceValue  = art;
        so.FindProperty("m_TextComponent").objectReferenceValue = txt;
        so.FindProperty("m_Placeholder").objectReferenceValue   = ph;
        so.FindProperty("m_TargetGraphic").objectReferenceValue = bg;
        so.FindProperty("m_CharacterLimit").intValue            = 25;
        so.FindProperty("m_LineType").enumValueIndex            = 0;
        so.ApplyModifiedProperties();
        field.customCaretColor = true; field.caretColor = Color.white; field.caretWidth = 2;
        return field;
    }

    // ── Fila de íconos ─────────────────────────────────────────────────────────
    static void BuildIconRow(Transform content, float top, float W, Button[] btns, Image[] borders)
    {
        const float CELL = 64f;
        float gap = (W - ICON_COUNT * CELL) / (ICON_COUNT - 1);
        var row = MakeEmpty(content, "IconRow");
        SetTop(row, top, new Vector2(W, 70f));
        float startX = -W / 2f + CELL / 2f;

        for (int i = 0; i < ICON_COUNT; i++)
        {
            var go = MakeImg(row.transform, $"Icon_{i}", new Vector2(CELL, CELL), new Vector2(startX + i * (CELL + gap), 0f), Hex("1E2140"), rounded);
            var img = go.GetComponent<Image>(); img.type = Image.Type.Sliced;
            borders[i] = img;
            btns[i] = go.AddComponent<Button>(); btns[i].targetGraphic = img;
            MakeImg(go.transform, "IconImg", new Vector2(CELL - 16f, CELL - 16f), Vector2.zero, Color.white, universeIcons[i])
                .GetComponent<Image>().preserveAspect = true;
        }
    }

    // ── Fila de colores ────────────────────────────────────────────────────────
    static void BuildColorRow(Transform content, float top, float W, Button[] btns, TextMeshProUGUI[] checks)
    {
        const float CELL = 56f;
        float gap = (W - COLOR_COUNT * CELL) / (COLOR_COUNT - 1) * 0.45f; // un poco más juntos
        float used = COLOR_COUNT * CELL + (COLOR_COUNT - 1) * gap;
        var row = MakeEmpty(content, "ColorRow");
        SetTop(row, top, new Vector2(W, 64f));
        float startX = -used / 2f + CELL / 2f;

        for (int i = 0; i < COLOR_COUNT; i++)
        {
            var go = MakeImg(row.transform, $"Color_{i}", new Vector2(CELL, CELL), new Vector2(startX + i * (CELL + gap), 0f), Hex(COLOR_HEX[i]), circle);
            var img = go.GetComponent<Image>();
            btns[i] = go.AddComponent<Button>(); btns[i].targetGraphic = img;
            checks[i] = MakeText(go.transform, "Check", "✓", Vector2.zero, new Vector2(CELL, CELL), 30f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        }
    }

    // ── Sección Eliminar ───────────────────────────────────────────────────────
    static Button BuildDeleteSection(Transform content, float top, float W)
    {
        var box = MakePanel(content, "DeleteSection", Vector2.zero, new Vector2(W, 120f), new Color(0.20f, 0.10f, 0.14f, 0.55f));
        SetTop(box.gameObject, top, new Vector2(W, 120f));
        AddBorder(box, RED, 0.7f);

        MakeText(box.transform, "Title", "Eliminar este universo", new Vector2(-W / 2f + 250f, 22f), new Vector2(480f, 32f), 24f, RED, TextAlignmentOptions.Left, FontStyles.Bold);
        MakeText(box.transform, "Sub", "Se borrarán todas las moléculas y átomos de este universo.", new Vector2(-W / 2f + 290f, -18f), new Vector2(560f, 30f), 20f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Left, FontStyles.Normal);

        var btn = MakeButton(box.transform, "BtnEliminar", "Eliminar", new Vector2(W / 2f - 150f, 0f), new Vector2(240f, 80f), RED, Color.white);
        return btn;
    }

    // ── Modal eliminar ───────────────────────────────────────────────────────
    static void BuildDeleteModal(Transform root, out GameObject modal, out Button btnCancelar, out Button btnConfirmar)
    {
        modal = MakeFill(root, "DeleteModal");
        var dim = modal.AddComponent<Image>(); dim.color = new Color(0f, 0f, 0f, 0.6f); // bloquea el fondo

        var panel = MakePanel(modal.transform, "Panel", Vector2.zero, new Vector2(660f, 380f), Hex("242659"));
        AddBorder(panel, CYAN, 0.9f);

        MakeText(panel.transform, "Title", "¿Eliminar universo?", new Vector2(0f, 120f), new Vector2(600f, 60f), 42f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        var body = MakeText(panel.transform, "Body",
            "Se borrarán todos los átomos y moléculas de este universo. Tus descubrimientos del diario se conservan.",
            new Vector2(0f, 20f), new Vector2(560f, 120f), 24f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center, FontStyles.Normal);
        body.enableWordWrapping = true;

        btnCancelar  = MakeButton(panel.transform, "BtnModalCancelar", "Cancelar", new Vector2(-150f, -120f), new Vector2(240f, 84f), Hex("3A3B6B"), Color.white);
        btnConfirmar = MakeButton(panel.transform, "BtnModalConfirmar", "Si, eliminar", new Vector2(150f, -120f), new Vector2(260f, 84f), RED, Color.white);
    }

    // ── Header avatar ─────────────────────────────────────────────────────────
    static void BuildHeaderAvatar(Transform header, Vector2 pos)
    {
        var ringGo = MakeImg(header, "AvatarRing", new Vector2(78f, 78f), pos, CYAN, circle);
        var bg = MakeImg(ringGo.transform, "AvatarCircle", new Vector2(66f, 66f), Vector2.zero, Color.white, circle).GetComponent<Image>();
        var inner = MakeImg(bg.transform, "Inner", new Vector2(40f, 40f), Vector2.zero, Color.white, avatarIcons[3]).GetComponent<Image>();
        inner.preserveAspect = true;

        var ha = ringGo.AddComponent<HeaderAvatar>();
        var so = new SerializedObject(ha);
        so.FindProperty("bg").objectReferenceValue           = bg;
        so.FindProperty("inner").objectReferenceValue        = inner;
        so.FindProperty("circleSprite").objectReferenceValue = circle;
        WireArray(so, "icons", avatarIcons);
        so.ApplyModifiedProperties();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────
    static TextMeshProUGUI PlaceText(Transform content, string name, string text, ref float top,
        float h, float size, Color color, TextAlignmentOptions align)
    {
        var tmp = MakeText(content, name, text, Vector2.zero, new Vector2(940f, h), size, color, align, FontStyles.Normal);
        SetTop(tmp.gameObject, top, new Vector2(940f, h));
        top -= h;
        return tmp;
    }

    static Sprite[] LoadIcons(string[] names)
    {
        var arr = new Sprite[names.Length];
        for (int i = 0; i < names.Length; i++)
            arr[i] = Spr($"Assets/Sprites/Icons/{names[i]}.png");
        return arr;
    }

    static void WireArray(SerializedObject so, string prop, Object[] items)
    {
        var p = so.FindProperty(prop);
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
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

    static Transform MakeScroll(Transform parent, string name, Vector2 pos, Vector2 size, float contentHeight)
    {
        var scrollGo = MakeEmpty(parent, name); SetRT(scrollGo, pos, size);
        var sr = scrollGo.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30f;

        var viewport = MakeEmpty(scrollGo.transform, "Viewport"); Stretch(viewport);
        var vpImg = viewport.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.001f);
        viewport.AddComponent<RectMask2D>();

        var contentGo = MakeEmpty(viewport.transform, "Content");
        var crt = (RectTransform)contentGo.transform;
        crt.anchorMin = new Vector2(0.5f, 1f); crt.anchorMax = new Vector2(0.5f, 1f); crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(size.x - 20f, contentHeight);

        sr.viewport = (RectTransform)viewport.transform; sr.content = crt;
        return contentGo.transform;
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
        float fontSize, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var go = MakeEmpty(parent, name); SetRT(go, pos, size);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = fnt; tmp.fontSize = fontSize; tmp.color = color;
        tmp.alignment = align; tmp.fontStyle = style;
        tmp.enableWordWrapping = false; tmp.overflowMode = TextOverflowModes.Overflow;
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

    static void SetTop(GameObject go, float topY, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size; rt.anchoredPosition = new Vector2(0f, topY);
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
