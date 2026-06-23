using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builder ADITIVO de PerfilScene ("Mi Perfil").
/// - Abre/crea la escena (no usa NewScene sobre una existente).
/// - Cámara, EventSystem y Canvas vía find-or-create (Ensure).
/// - Regenera SOLO el subárbol de UI "PerfilRoot" (la pantalla es 100% por código,
///   no hay nada colocado a mano que preservar). No toca otras escenas.
/// Menú: ChemiTech → Build Perfil Scene
/// </summary>
public static class PerfilBuilder
{
    const string ScenePath = "Assets/Scenes/PerfilScene.unity";
    const float  RW = 1600f, RH = 900f;

    // Assets compartidos (se cargan en Build)
    static TMP_FontAsset fnt;
    static Sprite rounded, circle, ring, beaker, person, email, lockSpr, check;
    static Sprite[] innerIcons;

    // Paleta
    static readonly Color CYAN   = Hex("2FD2E0");
    static readonly Color YELLOW = Hex("FFC83D");
    static readonly Color PINK   = Hex("FF5CA8");
    static readonly Color PURPLE = Hex("8B5CF6");

    [MenuItem("ChemiTech/Build Perfil Scene")]
    public static void Build() => Build(false);

    // Regenera la UI desde código AUNQUE ya exista (descarta cambios manuales).
    [MenuItem("ChemiTech/Rebuild/Perfil Scene (desde cero)")]
    public static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("Regenerar Perfil desde cero",
            "Esto DESTRUYE la UI de Perfil actual y la vuelve a generar por código.\n" +
            "Perderás cualquier cambio manual que hayas hecho dentro de la pantalla.\n\n¿Continuar?",
            "Sí, regenerar", "Cancelar"))
            return;
        Build(true);
    }

    // Corrige el resaltado de selección de avatares en la ESCENA ACTUAL:
    // mueve el indicador a DETRÁS del avatar (halo de fondo) sin tocar nada más.
    [MenuItem("ChemiTech/Fix/Perfil Avatar Selection")]
    public static void FixAvatarSelection()
    {
        var mgr = Object.FindObjectOfType<PerfilManager>();
        if (mgr == null)
        {
            EditorUtility.DisplayDialog("Error",
                "No se encontró PerfilManager en la escena activa.\nAbre PerfilScene primero.", "OK");
            return;
        }

        circle = Spr("Assets/Sprites/AtomCircle.png");

        var so = new SerializedObject(mgr);
        var optProp  = so.FindProperty("avatarOptions");
        var ringProp = so.FindProperty("avatarRings");
        if (optProp == null || optProp.arraySize == 0)
        {
            EditorUtility.DisplayDialog("Error", "PerfilManager no tiene 'avatarOptions' cableados.", "OK");
            return;
        }
        if (ringProp.arraySize < optProp.arraySize) ringProp.arraySize = optProp.arraySize;

        int count = 0;
        for (int i = 0; i < optProp.arraySize; i++)
        {
            var btn = optProp.GetArrayElementAtIndex(i).objectReferenceValue as Button;
            if (btn == null) continue;
            var optGo  = btn.gameObject;
            var rt     = (RectTransform)optGo.transform;
            var parent = optGo.transform.parent;

            // 1) Quitar el indicador viejo que iba ENCIMA
            var oldSel = optGo.transform.Find("Sel");
            if (oldSel != null) Object.DestroyImmediate(oldSel.gameObject);

            // 2) Crear (o reutilizar) el halo DETRÁS, alineado al avatar
            string haloName = optGo.name + "_Sel";
            var existing = parent.Find(haloName);
            GameObject halo = existing ? existing.gameObject
                                       : MakeImg(parent, haloName, Vector2.zero, Vector2.zero, CYAN, circle);

            var hrt = (RectTransform)halo.transform;
            hrt.anchorMin        = rt.anchorMin;
            hrt.anchorMax        = rt.anchorMax;
            hrt.pivot            = rt.pivot;
            hrt.anchoredPosition = rt.anchoredPosition;
            hrt.sizeDelta        = rt.sizeDelta + new Vector2(22f, 22f);

            halo.transform.SetSiblingIndex(optGo.transform.GetSiblingIndex()); // detrás del avatar
            halo.SetActive(false);

            // 3) Repointar avatarRings[i] al nuevo halo
            ringProp.GetArrayElementAtIndex(i).objectReferenceValue = halo;
            count++;
        }

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[PerfilBuilder] ✓ Selección de avatar corregida en {count} opciones.");
        EditorUtility.DisplayDialog("¡Listo!",
            $"Resaltado corregido en {count} avatares: ahora va DETRÁS (fondo) y no cubre la imagen.\n" +
            "Guarda con Ctrl+S.", "OK");
    }

    static void Build(bool force)
    {
        if (!force && !EditorUtility.DisplayDialog("Construir Perfil Scene (aditivo)",
            "Crea PerfilScene si no existe. Si ya existe, NO se reconstruye\n" +
            "para no perder tus cambios manuales.\n\n¿Continuar?",
            "Sí, continuar", "Cancelar"))
            return;

        // ── Abrir o crear la escena ───────────────────────────────────────────
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
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // ── Guard: si la UI ya existe y no se fuerza, preservarla ──────────────
        if (!force)
        {
            var existingCanvas = FindRoot(scene, "Canvas");
            if (existingCanvas != null && existingCanvas.transform.Find("PerfilRoot") != null)
            {
                EditorUtility.DisplayDialog("Perfil ya existe",
                    "La UI de Perfil ya está en la escena. No se reconstruyó para\n" +
                    "no perder tus cambios manuales.\n\n" +
                    "Para regenerarla desde código usa:\n" +
                    "ChemiTech → Rebuild → Perfil Scene (desde cero).",
                    "OK");
                return;
            }
        }

        // ── Assets ────────────────────────────────────────────────────────────
        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = Spr("Assets/Sprites/Login/rounded-panel.png");
        circle  = Spr("Assets/Sprites/AtomCircle.png");
        ring    = Spr("Assets/Sprites/orbit-ring.png");
        beaker  = Spr("Assets/Sprites/icon-beaker.png");
        person  = Spr("Assets/Sprites/person-icon.png");
        email   = Spr("Assets/Sprites/Login/icon-email.png");
        lockSpr = Spr("Assets/Sprites/Login/icon-lock.png");
        check   = Spr("Assets/Sprites/check-green.png");
        innerIcons = new[]
        {
            Spr("Assets/Sprites/Icons/icon_galaxy.png"),
            Spr("Assets/Sprites/Icons/icon_star.png"),
            Spr("Assets/Sprites/Icons/icon_comet.png"),
            Spr("Assets/Sprites/Icons/icon_planet.png"),
            Spr("Assets/Sprites/Icons/icon_satellite.png"),
            Spr("Assets/Sprites/Icons/icon_telescope.png"),
            Spr("Assets/Sprites/Icons/icon_globe.png"),
            Spr("Assets/Sprites/Icons/icon_burst.png"),
        };

        // ── Cámara / EventSystem / Canvas (find-or-create) ────────────────────
        var camGo = EnsureRoot(scene, "Main Camera");
        camGo.tag = "MainCamera";
        var cam = Ensure<Camera>(camGo);
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("0A1233");
        cam.orthographic    = true;
        cam.depth           = -1;
        Ensure<AudioListener>(camGo);

        var esGo = EnsureRoot(scene, "EventSystem");
        Ensure<EventSystem>(esGo);
        Ensure<StandaloneInputModule>(esGo);

        var canvasGo = EnsureRoot(scene, "Canvas");
        var cv = Ensure<Canvas>(canvasGo);
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var csc = Ensure<CanvasScaler>(canvasGo);
        csc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        csc.referenceResolution = new Vector2(RW, RH);
        csc.matchWidthOrHeight  = 0.5f;
        Ensure<GraphicRaycaster>(canvasGo);

        // ── Regenerar el subárbol de UI ───────────────────────────────────────
        var old = canvasGo.transform.Find("PerfilRoot");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = MakeFill(canvasGo.transform, "PerfilRoot");

        // Fondo
        var bg = MakeImg(root.transform, "Background", new Vector2(RW, RH), Vector2.zero,
            Color.white, Spr("Assets/Sprites/MainMenuBG.png"));
        Stretch(bg);

        // ── Componer ──────────────────────────────────────────────────────────
        Image headerAvatarIcon;
        BuildHeader(root.transform, out var btnBack, out headerAvatarIcon);

        var panel = MakePanel(root.transform, "MainPanel", new Vector2(0f, -60f),
            new Vector2(1480f, 720f), Hex("242659"));
        AddBorder(panel, CYAN, 0.25f);

        Image profileAvatarIcon;
        var profileView = BuildProfileView(panel.transform,
            out var btnCambiar, out var btnCerrar, out profileAvatarIcon);

        Image previewIcon;
        var avatarView = BuildAvatarView(panel.transform,
            out var btnCancelar, out var btnGuardar,
            out var options, out var rings, out previewIcon);
        avatarView.SetActive(false);

        // ── Manager ─────────────────────────────────────────────────────────────
        var mgrGo = EnsureRoot(scene, "PerfilManager");
        var mgr   = Ensure<PerfilManager>(mgrGo);
        var so = new SerializedObject(mgr);
        so.FindProperty("btnBack").objectReferenceValue          = btnBack;
        so.FindProperty("btnCerrarSesion").objectReferenceValue  = btnCerrar;
        so.FindProperty("profileView").objectReferenceValue      = profileView;
        so.FindProperty("avatarView").objectReferenceValue       = avatarView;
        so.FindProperty("btnCambiarAvatar").objectReferenceValue = btnCambiar;
        so.FindProperty("btnCancelar").objectReferenceValue      = btnCancelar;
        so.FindProperty("btnGuardar").objectReferenceValue       = btnGuardar;
        so.FindProperty("previewIcon").objectReferenceValue      = previewIcon;
        so.FindProperty("headerAvatarIcon").objectReferenceValue = headerAvatarIcon;
        so.FindProperty("profileAvatarIcon").objectReferenceValue= profileAvatarIcon;

        var optProp = so.FindProperty("avatarOptions");
        optProp.arraySize = options.Length;
        for (int i = 0; i < options.Length; i++)
            optProp.GetArrayElementAtIndex(i).objectReferenceValue = options[i];

        var ringProp = so.FindProperty("avatarRings");
        ringProp.arraySize = rings.Length;
        for (int i = 0; i < rings.Length; i++)
            ringProp.GetArrayElementAtIndex(i).objectReferenceValue = rings[i];

        so.ApplyModifiedProperties();

        // ── Guardar + Build Settings ──────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[PerfilBuilder] ✓ PerfilScene generada.");
        EditorUtility.DisplayDialog("¡Listo!",
            "PerfilScene generada y agregada a Build Settings.\n" +
            "Si hay error de Input System: ChemiTech → Fix → Input System.",
            "OK");
    }

    // ── Header ─────────────────────────────────────────────────────────────────
    static GameObject BuildHeader(Transform parent, out Button btnBack, out Image avatarIcon)
    {
        var header = MakePanel(parent, "Header", new Vector2(0f, 376f),
            new Vector2(1480f, 100f), Hex("2E3270"));
        AddBorder(header, PURPLE, 0.5f);

        // Botón atrás
        var backGo = MakePanel(header.transform, "BtnBack", new Vector2(-672f, 0f),
            new Vector2(76f, 76f), Hex("171A3E"));
        btnBack = backGo.gameObject.AddComponent<Button>();
        btnBack.targetGraphic = backGo;
        MakeText(backGo.transform, "Arrow", "<", Vector2.zero, new Vector2(76f, 76f),
            48f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // Mini avatar
        var ringGo = MakeImg(header.transform, "AvatarRing", new Vector2(78f, 78f),
            new Vector2(-566f, 0f), CYAN, circle);
        var av = MakeImg(ringGo.transform, "AvatarCircle", new Vector2(66f, 66f), Vector2.zero, Color.white, circle);
        avatarIcon = MakeImg(av.transform, "Inner", new Vector2(40f, 40f), Vector2.zero, PURPLE, person)
            .GetComponent<Image>();
        avatarIcon.preserveAspect = true;

        // Título
        MakeText(header.transform, "Title", "Mi Perfil", new Vector2(-150f, 0f),
            new Vector2(640f, 60f), 44f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        return header.gameObject;
    }

    // ── Vista de perfil ──────────────────────────────────────────────────────
    static GameObject BuildProfileView(Transform panel, out Button btnCambiar,
        out Button btnCerrar, out Image avatarIcon)
    {
        var view = MakeFill(panel, "ProfileView");

        // Tarjeta de avatar (izquierda)
        var card = MakePanel(view.transform, "AvatarCard", new Vector2(-500f, 0f),
            new Vector2(420f, 656f), Hex("31346B"));
        AddBorder(card, PURPLE, 0.4f);

        var circ = MakeImg(card.transform, "AvatarCircle", new Vector2(250f, 250f),
            new Vector2(0f, 150f), Color.white, circle);
        avatarIcon = MakeImg(circ.transform, "Inner", new Vector2(150f, 150f), Vector2.zero, PURPLE, person)
            .GetComponent<Image>();
        avatarIcon.preserveAspect = true;

        MakeText(card.transform, "Username", "Jugador12345", new Vector2(0f, -35f),
            new Vector2(380f, 50f), 34f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        btnCambiar = MakeButton(card.transform, "BtnCambiarAvatar", "Cambiar Avatar",
            new Vector2(0f, -250f), new Vector2(340f, 84f), CYAN, Hex("0A2F44"));

        // Columna derecha con scroll
        var content = MakeScroll(view.transform, "RightScroll", new Vector2(240f, 0f),
            new Vector2(980f, 640f), 730f);

        // Grid 2x2 de stats
        var grid = MakeEmpty(content.transform, "StatsGrid");
        SetTop(grid, 0f, new Vector2(960f, 360f));
        MakeStatCard(grid.transform, new Vector2(-240f,  90f), "27",            "Moléculas Descubiertas", CYAN,   beaker);
        MakeStatCard(grid.transform, new Vector2( 240f,  90f), "3d 5h 34m 4s",  "Tiempo Jugado",          YELLOW, innerIcons[2]);
        MakeStatCard(grid.transform, new Vector2(-240f, -90f), "13/07/2026",    "Usuario desde",          YELLOW, person);
        MakeStatCard(grid.transform, new Vector2( 240f, -90f), "3",             "Universos Creados",      PINK,   innerIcons[0]);

        // Correo
        var mail = MakeEmpty(content.transform, "EmailSection");
        SetTop(mail, -410f, new Vector2(960f, 160f));
        MakeText(mail.transform, "Label", "Correo electrónico", new Vector2(-360f, 58f),
            new Vector2(420f, 34f), 24f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Left, FontStyles.Normal);
        var field = MakePanel(mail.transform, "Field", new Vector2(0f, -22f), new Vector2(960f, 96f), Hex("1A1D44"));
        MakeImg(field.transform, "Icon", new Vector2(34f, 34f), new Vector2(-430f, 0f), new Color(1f, 1f, 1f, 0.8f), email)
            .GetComponent<Image>().preserveAspect = true;
        MakeText(field.transform, "Text", "alex@chemitech.com", new Vector2(40f, 0f),
            new Vector2(820f, 50f), 28f, Color.white, TextAlignmentOptions.Left, FontStyles.Normal);

        // Cerrar Sesión
        var logoutGo = MakeButton(content.transform, "BtnCerrarSesion", "Cerrar Sesión",
            new Vector2(0f, 0f), new Vector2(940f, 100f), CYAN, Hex("0A2F44"));
        SetTop(logoutGo.gameObject, -610f, new Vector2(940f, 100f));
        btnCerrar = logoutGo;

        return view;
    }

    // ── Vista de selección de avatar ─────────────────────────────────────────
    static GameObject BuildAvatarView(Transform panel, out Button btnCancelar, out Button btnGuardar,
        out Button[] options, out GameObject[] rings, out Image previewIcon)
    {
        var view = MakeFill(panel, "AvatarView");

        // Tarjeta de vista previa (izquierda)
        var card = MakePanel(view.transform, "PreviewCard", new Vector2(-500f, 0f),
            new Vector2(420f, 656f), Hex("31346B"));
        AddBorder(card, PURPLE, 0.4f);
        MakeText(card.transform, "PreviewLabel", "VISTA PREVIA", new Vector2(0f, 250f),
            new Vector2(360f, 30f), 22f, CYAN, TextAlignmentOptions.Center, FontStyles.Bold);
        var circ = MakeImg(card.transform, "AvatarCircle", new Vector2(250f, 250f),
            new Vector2(0f, 70f), Color.white, circle);
        previewIcon = MakeImg(circ.transform, "Inner", new Vector2(150f, 150f), Vector2.zero, PURPLE, person)
            .GetComponent<Image>();
        previewIcon.preserveAspect = true;
        MakeText(card.transform, "Username", "Jugador12345", new Vector2(0f, -120f),
            new Vector2(380f, 50f), 32f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // Encabezado de la cuadrícula
        MakeText(view.transform, "ChooseLabel", "Elige tu avatar", new Vector2(240f, 230f),
            new Vector2(880f, 50f), 34f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        // Cuadrícula 6x2
        var palette = new[]
        {
            Hex("4A90E2"), Hex("E86AA6"), Hex("3FB979"), PURPLE, Hex("F2922B"), Hex("E8731E"),
            CYAN, Hex("8A93A8"), Hex("F0A03C"), Hex("D95C9A"),
        };
        const int COLS = 6;
        const float CELL = 110f, GAP = 28f;
        float startX = 240f - ((COLS - 1) * (CELL + GAP)) * 0.5f;
        float[] rowY = { 70f, -68f };

        options = new Button[palette.Length];
        rings   = new GameObject[palette.Length];

        for (int i = 0; i < palette.Length; i++)
        {
            int row = i / COLS, col = i % COLS;
            var pos = new Vector2(startX + col * (CELL + GAP), rowY[row]);
            options[i] = MakeAvatarOption(view.transform, $"Avatar{i}", pos, palette[i],
                innerIcons[i % innerIcons.Length], out rings[i]);
        }

        // Bloqueados (Lvl 10 / Lvl 15): fila 2, columnas 5 y 6
        MakeLockedOption(view.transform, "AvatarLock1", new Vector2(startX + 4 * (CELL + GAP), rowY[1]), "Lvl 10");
        MakeLockedOption(view.transform, "AvatarLock2", new Vector2(startX + 5 * (CELL + GAP), rowY[1]), "Lvl 15");

        // Botones inferiores
        btnCancelar = MakeButton(view.transform, "BtnCancelar", "Cancelar",
            new Vector2(300f, -250f), new Vector2(220f, 84f), Hex("3A3B6B"), Color.white);
        btnGuardar  = MakeButton(view.transform, "BtnGuardar", "Guardar",
            new Vector2(560f, -250f), new Vector2(260f, 84f), CYAN, Hex("0A2F44"));
        MakeImg(btnGuardar.transform, "Check", new Vector2(34f, 34f), new Vector2(-78f, 0f), Color.white, check)
            .GetComponent<Image>().preserveAspect = true;

        return view;
    }

    // ── Sub-builders ─────────────────────────────────────────────────────────
    static GameObject MakeStatCard(Transform parent, Vector2 pos, string value, string label,
        Color accent, Sprite icon)
    {
        var card = MakePanel(parent, "Stat_" + label, pos, new Vector2(460f, 160f), Hex("20234E"));
        AddBorder(card, accent, 0.55f);

        if (icon != null)
            MakeImg(card.transform, "Icon", new Vector2(40f, 40f), new Vector2(0f, 48f), accent, icon)
                .GetComponent<Image>().preserveAspect = true;

        MakeText(card.transform, "Value", value, new Vector2(0f, 2f),
            new Vector2(440f, 50f), 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        MakeText(card.transform, "Label", label, new Vector2(0f, -48f),
            new Vector2(440f, 34f), 22f, accent, TextAlignmentOptions.Center, FontStyles.Normal);
        return card.gameObject;
    }

    static Button MakeAvatarOption(Transform parent, string name, Vector2 pos, Color color,
        Sprite inner, out GameObject sel)
    {
        // Halo de selección DETRÁS del avatar (resaltado de fondo), oculto por defecto.
        // Se crea ANTES que el avatar para quedar en un índice menor → se dibuja detrás.
        sel = MakeImg(parent, name + "_Sel", new Vector2(132f, 132f), pos, CYAN, circle);
        sel.SetActive(false);

        // Avatar (por encima del halo)
        var go = MakeImg(parent, name, new Vector2(110f, 110f), pos, color, circle);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();

        var ic = MakeImg(go.transform, "Inner", new Vector2(60f, 60f), Vector2.zero, Color.white, inner);
        ic.GetComponent<Image>().preserveAspect = true;

        return btn;
    }

    static void MakeLockedOption(Transform parent, string name, Vector2 pos, string lvl)
    {
        var go = MakeImg(parent, name, new Vector2(110f, 110f), pos, Hex("262A55"), circle);
        MakeImg(go.transform, "Lock", new Vector2(44f, 44f), new Vector2(0f, 10f), new Color(1f, 1f, 1f, 0.75f), lockSpr)
            .GetComponent<Image>().preserveAspect = true;
        MakeText(go.transform, "Lvl", lvl, new Vector2(0f, -30f),
            new Vector2(100f, 24f), 18f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center, FontStyles.Bold);
    }

    // ── ScrollRect ─────────────────────────────────────────────────────────────
    static Transform MakeScroll(Transform parent, string name, Vector2 pos, Vector2 size, float contentHeight)
    {
        var scrollGo = MakeEmpty(parent, name);
        SetRT(scrollGo, pos, size);
        var sr = scrollGo.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical   = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;

        var viewport = MakeEmpty(scrollGo.transform, "Viewport");
        Stretch(viewport);
        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.001f); // casi invisible, requerido por la máscara
        viewport.AddComponent<RectMask2D>();

        var content = MakeEmpty(viewport.transform, "Content");
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 1f);
        crt.anchorMax = new Vector2(0.5f, 1f);
        crt.pivot     = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(size.x - 20f, contentHeight);

        sr.viewport = viewport.GetComponent<RectTransform>();
        sr.content  = crt;

        return content.transform;
    }

    // ── Primitivas / helpers ───────────────────────────────────────────────────
    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static GameObject EnsureRoot(Scene scene, string name)
    {
        var found = FindRoot(scene, name);
        if (found != null) return found;
        var n = new GameObject(name);
        SceneManager.MoveGameObjectToScene(n, scene);
        return n;
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
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
        var go = MakeEmpty(parent, name);
        Stretch(go);
        return go;
    }

    static Image MakePanel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, size);
        var img = go.AddComponent<Image>();
        img.sprite = rounded;
        img.type   = Image.Type.Sliced;
        img.color  = color;
        return img;
    }

    // Marco: un panel un poco más grande detrás del panel original (mismo padre).
    static void AddBorder(Image panel, Color color, float alpha)
    {
        var parent = panel.transform.parent;
        var rt = panel.rectTransform;
        var b = MakeImg(parent, panel.name + "_Border",
            rt.sizeDelta + new Vector2(8f, 8f), rt.anchoredPosition,
            new Color(color.r, color.g, color.b, alpha), rounded);
        b.GetComponent<Image>().type = Image.Type.Sliced;
        b.transform.SetSiblingIndex(panel.transform.GetSiblingIndex()); // justo detrás
    }

    static GameObject MakeImg(Transform parent, string name, Vector2 size, Vector2 pos, Color color, Sprite spr)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, size);
        var img = go.AddComponent<Image>();
        img.color = color;
        if (spr != null) img.sprite = spr;
        return go;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size,
        float fontSize, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, size);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.font = fnt;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
        Color bgColor, Color textColor)
    {
        var img = MakePanel(parent, name, pos, size, bgColor);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
        btn.colors = cb;
        MakeText(img.transform, "Label", label, Vector2.zero, size, 30f, textColor,
            TextAlignmentOptions.Center, FontStyles.Bold);
        return btn;
    }

    static void SetRT(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // Posiciona un hijo del Content (pivot top-center): y se mide hacia abajo desde el tope.
    static void SetTop(GameObject go, float topY, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(0f, topY);
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Sprite Spr(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }

    static void AddSceneToBuildSettings(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in list) if (s.path == path) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
