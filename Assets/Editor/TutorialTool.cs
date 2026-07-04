using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Herramienta ADITIVA: agrega el tutorial informativo a ZonaJuegoScene sin tocar
/// el HUD. Crea (find-or-refresh) "TutorialOverlay" como hijo del Canvas (backdrop
/// + modal de bienvenida + modal de paso), le pone un TutorialManager con los pasos,
/// y cablea ZonaJuegoManager.tutorial para el botón "Ver tutorial" de la pausa.
/// Menú: ChemiTech → Add → Tutorial (ZonaJuego)
/// </summary>
public static class TutorialTool
{
    static TMP_FontAsset fnt;
    static Sprite rounded, circle;

    static readonly Color PANEL   = Hex("222A55");
    static readonly Color BORDER  = new Color(0.30f, 0.80f, 0.95f, 0.85f);
    static readonly Color CYAN    = Hex("30D3E6");
    static readonly Color DARKBTN = Hex("3A3B6B");
    static readonly Color HINTBG  = new Color(0.09f, 0.10f, 0.24f, 1f);

    // Contenido de los pasos (informativo). El usuario puede editarlo en el inspector.
    static readonly (string title, string body, string hint)[] STEPS =
    {
        ("Barra de átomos",
         "Abre el \"Selector de átomos\", elige un elemento y quedará listo en la barra inferior.",
         "⚛️  →  👆  →  ¡Aparece en el mundo!"),
        ("Colocar átomos",
         "Toca un slot de la barra: aparece una previsualización en el centro. Muévela con las flechas y pulsa \"Presiona para colocar átomo\".",
         "👆 Slot   →   ⬆⬇⬅➡   →   Colocar"),
        ("Mover y mirar",
         "Arrastra en el mundo para rotar la cámara. El d-pad de la izquierda desplaza la vista y las flechas de la derecha suben o bajan; \"Recentrar\" vuelve a la vista inicial.",
         "🖱️ Arrastrar = rotar    ·    ⬆⬇ = subir/bajar"),
        ("Editar átomos",
         "Toca un átomo ya colocado para seleccionarlo: muévelo con las flechas o elimínalo con el botón de borrar.",
         "👆 Átomo   →   mover  /  🗑️ borrar"),
        ("Crear moléculas",
         "Acerca átomos compatibles: el sistema detecta la estructura y dibuja los enlaces. Verás el aviso \"¡Molécula formada!\".",
         "⚛️ + ⚛️   →   🔗   →   ¡Molécula formada!"),
        ("Detección en línea",
         "La detección usa un servicio de IA. Si se cae, verás un aviso y se reintenta solo; colocar, mover o borrar átomos siempre funciona.",
         "⚠️ Sin conexión   →   se reintenta solo"),
        ("Diario y pausa",
         "Cada molécula nueva se guarda en tu Diario. Usa el botón de pausa (arriba a la izquierda) para guardar, ajustes, tutorial o salir.",
         "⏸️ Pausa   →   Guardar · Ajustes · Salir"),
    };

    [MenuItem("ChemiTech/Add/Tutorial (ZonaJuego)")]
    public static void Add()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var canvas  = FindRootWith<Canvas>(scene, "Canvas");
        var zonaMgr = FindInScene<ZonaJuegoManager>(scene);
        if (canvas == null || zonaMgr == null)
        {
            EditorUtility.DisplayDialog("Tutorial",
                "Abre primero ZonaJuegoScene (no encontré Canvas + ZonaJuegoManager en la escena activa).", "OK");
            return;
        }

        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = Spr("Assets/Sprites/Login/rounded-panel.png");
        circle  = Spr("Assets/Sprites/AtomCircle.png");

        var prev = FindChild(canvas.transform, "TutorialOverlay");
        if (prev) Object.DestroyImmediate(prev);

        // ── Overlay a pantalla completa (por encima del HUD) ────────────────────
        var overlay = MakeEmpty(canvas.transform, "TutorialOverlay");
        Stretch(overlay);
        overlay.transform.SetAsLastSibling();

        var backdrop = MakeEmpty(overlay.transform, "Backdrop");
        Stretch(backdrop);
        var bdImg = backdrop.AddComponent<Image>();
        bdImg.color = new Color(0.03f, 0.05f, 0.12f, 0.72f);
        bdImg.raycastTarget = true;

        // ── Modal de bienvenida ─────────────────────────────────────────────────
        var welcome = ModalPanel(overlay.transform, "WelcomeModal", new Vector2(560f, 400f));
        MakeText(welcome.transform, "Title", "¡Bienvenido al universo!", new Vector2(0f, 128f),
                 new Vector2(500f, 56f), 38f, Color.white, TextAlignmentOptions.Center, bold: true);
        MakeText(welcome.transform, "Body",
                 "¿Quieres ver un tutorial rápido para aprender lo básico? Solo toma unos 60 segundos ⏱️",
                 new Vector2(0f, 54f), new Vector2(480f, 90f), 23f, new Color(1f, 1f, 1f, 0.85f),
                 TextAlignmentOptions.Center);

        Chip(welcome.transform, "Controles",      -164f, -24f, 130f);
        Chip(welcome.transform, "Inventario",       -20f, -24f, 130f);
        Chip(welcome.transform, "Colocar átomos",  144f, -24f, 170f);
        Chip(welcome.transform, "Crear moléculas",  -62f, -60f, 180f);
        Chip(welcome.transform, "Diario",            97f, -60f, 110f);

        var btnOmitir  = MakeButton(welcome.transform, "BtnOmitir", "Omitir Tutorial",
                                    new Vector2(-125f, -138f), new Vector2(230f, 60f), DARKBTN, out _, 26f);
        var btnIniciar = MakeButton(welcome.transform, "BtnIniciar", "Iniciar Tutorial",
                                    new Vector2(125f, -138f), new Vector2(230f, 60f), CYAN, out _, 26f);

        // ── Modal de paso ───────────────────────────────────────────────────────
        var step = ModalPanel(overlay.transform, "StepModal", new Vector2(560f, 470f));
        var stepTitle = MakeText(step.transform, "Title", "1 - Barra de átomos", new Vector2(0f, 150f),
                                 new Vector2(480f, 50f), 30f, Color.white, TextAlignmentOptions.Center, bold: true);
        var stepBody  = MakeText(step.transform, "Body", "…", new Vector2(0f, 66f),
                                 new Vector2(470f, 120f), 23f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Center);

        var hintBox = MakeEmpty(step.transform, "HintBox");
        SetRT(hintBox, new Vector2(0f, -24f), new Vector2(474f, 66f));
        var hintImg = hintBox.AddComponent<Image>();
        hintImg.sprite = rounded; hintImg.type = Image.Type.Sliced; hintImg.color = HINTBG;
        var stepHint = MakeText(hintBox.transform, "Hint", "…", Vector2.zero, new Vector2(454f, 60f),
                                20f, new Color(1f, 1f, 1f, 0.92f), TextAlignmentOptions.Center);

        var btnAtras = MakeButton(step.transform, "BtnAtras", "Atrás",
                                  new Vector2(-120f, -120f), new Vector2(210f, 60f), DARKBTN, out _, 26f);
        var btnSig   = MakeButton(step.transform, "BtnSiguiente", "Siguiente",
                                  new Vector2(120f, -120f), new Vector2(210f, 60f), CYAN, out var sigLabel, 26f);

        var dots = MakeEmpty(step.transform, "Dots");
        SetRT(dots, new Vector2(0f, -186f), new Vector2(420f, 20f));

        // Botón de cerrar (X) en la esquina superior derecha
        var closeGo = MakeEmpty(step.transform, "BtnClose");
        SetRT(closeGo, new Vector2(278f, 233f), new Vector2(46f, 46f));
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.sprite = circle; closeImg.type = Image.Type.Simple; closeImg.color = Hex("E74C3C");
        closeImg.preserveAspect = true;
        var btnClose = closeGo.AddComponent<Button>();
        btnClose.targetGraphic = closeImg;
        MakeLabelCentered(closeGo.transform, "✕", 24f);

        // ── TutorialManager ─────────────────────────────────────────────────────
        var mgr = overlay.AddComponent<TutorialManager>();
        var so  = new SerializedObject(mgr);
        SetRef(so, "backdrop", backdrop);
        SetRef(so, "welcomeModal", welcome);
        SetRef(so, "btnOmitir", btnOmitir);
        SetRef(so, "btnIniciar", btnIniciar);
        SetRef(so, "stepModal", step);
        SetRef(so, "stepTitle", stepTitle);
        SetRef(so, "stepBody", stepBody);
        SetRef(so, "hintBox", hintBox);
        SetRef(so, "stepHint", stepHint);
        SetRef(so, "btnAtras", btnAtras);
        SetRef(so, "btnSiguiente", btnSig);
        SetRef(so, "btnSiguienteLabel", sigLabel);
        SetRef(so, "btnClose", btnClose);
        SetRef(so, "dotsContainer", dots.GetComponent<RectTransform>());
        so.FindProperty("dotSprite").objectReferenceValue = rounded;

        var stepsProp = so.FindProperty("steps");
        stepsProp.arraySize = STEPS.Length;
        for (int i = 0; i < STEPS.Length; i++)
        {
            var el = stepsProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("title").stringValue = STEPS[i].title;
            el.FindPropertyRelative("body").stringValue  = STEPS[i].body;
            el.FindPropertyRelative("hint").stringValue  = STEPS[i].hint;
        }
        so.ApplyModifiedProperties();

        // ── Cablear ZonaJuegoManager.tutorial (para "Ver tutorial" de la pausa) ──
        var zso = new SerializedObject(zonaMgr);
        SetRef(zso, "tutorial", mgr);
        zso.ApplyModifiedProperties();

        // Estado inicial oculto (el manager lo controla en runtime).
        welcome.SetActive(false);
        step.SetActive(false);
        backdrop.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[TutorialTool] ✓ Tutorial agregado y cableado.");
        EditorUtility.DisplayDialog("¡Listo!",
            $"Tutorial agregado al Canvas ({STEPS.Length} pasos) y cableado a ZonaJuegoManager.\n\n" +
            "• Logeado: se reproduce solo la 1ª vez por cuenta.\n" +
            "• Invitado: se reproduce cada vez que entra.\n" +
            "• Reejecutar desde el menú de pausa → \"Ver tutorial\".\n" +
            "• Para reprobar el auto-arranque de una cuenta: Edit → Clear All PlayerPrefs.", "OK");
    }

    // ── Modal con borde + panel; devuelve el root (los hijos se añaden encima) ─
    static GameObject ModalPanel(Transform parent, string name, Vector2 size)
    {
        var root = MakeEmpty(parent, name);
        SetRT(root, Vector2.zero, size);

        var border = MakeEmpty(root.transform, "Border");
        SetRT(border, Vector2.zero, size + new Vector2(12f, 12f));
        var bimg = border.AddComponent<Image>();
        bimg.sprite = rounded; bimg.type = Image.Type.Sliced; bimg.color = BORDER;
        bimg.raycastTarget = false;

        var panel = MakeEmpty(root.transform, "Panel");
        SetRT(panel, Vector2.zero, size);
        var pimg = panel.AddComponent<Image>();
        pimg.sprite = rounded; pimg.type = Image.Type.Sliced; pimg.color = PANEL;

        return root;
    }

    static void Chip(Transform parent, string text, float centerX, float y, float width)
    {
        var go = MakeEmpty(parent, "Chip");
        SetRT(go, new Vector2(centerX, y), new Vector2(width, 32f));
        var img = go.AddComponent<Image>();
        img.sprite = rounded; img.type = Image.Type.Sliced; img.color = new Color(1f, 1f, 1f, 0.08f);
        img.raycastTarget = false;
        var t = MakeLabelCentered(go.transform, text, 17f);
        t.color = new Color(1f, 1f, 1f, 0.8f);
        t.fontStyle = FontStyles.Normal;
    }

    // ── Helpers de escena ─────────────────────────────────────────────────────
    static T FindRootWith<T>(Scene scene, string preferName) where T : Component
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == preferName && go.GetComponent<T>() != null) return go.GetComponent<T>();
        foreach (var go in scene.GetRootGameObjects())
        {
            var c = go.GetComponent<T>(); if (c) return c;
        }
        return null;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            var c = go.GetComponentInChildren<T>(true); if (c) return c;
        }
        return null;
    }

    static GameObject FindChild(Transform parent, string name)
    {
        foreach (Transform t in parent) if (t.name == name) return t.gameObject;
        return null;
    }

    static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
    }

    // ── Helpers de UI ─────────────────────────────────────────────────────────
    static GameObject MakeEmpty(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
        Color bg, out TextMeshProUGUI labelTmp, float labelSize)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, size);
        var img = go.AddComponent<Image>();
        img.sprite = rounded; img.type = Image.Type.Sliced; img.color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        labelTmp = MakeLabelCentered(go.transform, label, labelSize);
        return btn;
    }

    static TextMeshProUGUI MakeLabelCentered(Transform parent, string text, float size)
    {
        var go = MakeEmpty(parent, "Label");
        Stretch(go);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.font = fnt; t.fontSize = size; t.color = Color.white;
        t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size,
        float fontSize, Color color, TextAlignmentOptions align, bool bold = false)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, size);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = fnt; tmp.fontSize = fontSize; tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f); rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetRT(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static Sprite Spr(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
}
