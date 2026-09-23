using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Construye MisClasesScene: la lista de clases con sus dos caras (alumno y docente),
/// el modal de crear clase, la pantalla de códigos generados y la confirmación de
/// terminar.
///
/// La escena es NUEVA y se genera entera, así que este builder RECREA el Canvas en
/// cada corrida. Eso lo hace predecible mientras se itera el diseño, pero significa
/// que los ajustes hechos a mano en el Canvas se pierden: cuando la pantalla esté
/// aprobada, congelarla y extenderla con herramientas aditivas, como ZonaJuegoScene.
///
/// Menú: ChemiTech → Build Mis Clases Scene
/// </summary>
public static class MisClasesBuilder
{
    const string ScenePath = "Assets/Scenes/MisClasesScene.unity";

    // Paleta del proyecto (la misma de MisUniversos)
    static readonly Color BG        = Hex("1E2050");
    static readonly Color CARD      = Hex("3A3D70");
    static readonly Color PANEL     = Hex("33356B");
    static readonly Color MODAL_BG  = Hex("242659");
    static readonly Color CYAN      = Hex("19A7CE");
    static readonly Color ACCENT    = Hex("4DD9E8");
    static readonly Color PURPLE    = Hex("5A5FA5");
    static readonly Color GREEN     = Hex("2ECC71");
    static readonly Color RED       = Hex("E74C3C");
    static readonly Color AMBER     = Hex("F5A623");
    static readonly Color DIM       = Hex("A2A2A2");

    static TMP_FontAsset fnt;
    static Sprite        rounded, uiSpr;

    [MenuItem("ChemiTech/Build Mis Clases Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Mis Clases Scene",
                "Crea (o regenera) MisClasesScene.\n\n" +
                "OJO: recrea el Canvas entero. Si ajustaste algo a mano ahí, se pierde.\n\n¿Continuar?",
                "Sí, continuar", "Cancelar"))
            return;

        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        uiSpr   = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var scene = OpenOrCreateScene();

        // Cámara y EventSystem: se reutilizan si ya están.
        EnsureCamera(scene);
        EnsureEventSystem(scene);

        // Canvas: se recrea entero (ver nota de la clase).
        var oldCanvas = FindRoot(scene, "Canvas");
        if (oldCanvas) Object.DestroyImmediate(oldCanvas);

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                                      typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasGo, scene);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        var root = canvasGo.transform;

        // Fondo
        var bg = MakeEmpty(root, "Background"); Stretch(bg);
        var bgImg = bg.AddComponent<Image>(); bgImg.color = BG; bgImg.raycastTarget = false;

        // ── Estructura ────────────────────────────────────────────────────────
        var btnAtras = BuildHeader(root);
        BuildTeacherBar(root, out var teacherBar, out var btnCrear);
        var loading = BuildLoading(root);
        BuildEmptyState(root, out var emptyState, out var emptyLabel);
        BuildList(root, out var listState, out var listContent, out var cardTemplate);
        BuildCreateModal(root, out var createModal, out var inName, out var inSection,
                         out var inCount, out var btnCreateOk, out var btnCreateCancel, out var createError);
        BuildCodesModal(root, out var codesModal, out var codesTitle, out var codesContent,
                        out var codeRow, out var btnCodesClose);
        BuildStopModal(root, out var stopModal, out var stopMsg, out var btnStopOk, out var btnStopCancel);
        BuildNotice(root, out var noticeRoot, out var noticeText);

        // ── Manager + cableado ────────────────────────────────────────────────
        var mgrGo = new GameObject("MisClasesManager", typeof(RectTransform));
        mgrGo.transform.SetParent(root, false);
        var mgr = mgrGo.AddComponent<MisClasesManager>();

        var so = new SerializedObject(mgr);
        SetRef(so, "btnAtras",         btnAtras);
        SetRef(so, "loadingGroup",     loading);
        SetRef(so, "listGroup",        listState);
        SetRef(so, "emptyStateGroup",  emptyState);
        SetRef(so, "emptyLabel",       emptyLabel);
        SetRef(so, "listContent",      listContent);
        SetRef(so, "cardTemplate",     cardTemplate);
        SetRef(so, "teacherBar",       teacherBar);
        SetRef(so, "btnCrearClase",    btnCrear);
        SetRef(so, "createModal",      createModal);
        SetRef(so, "inputName",        inName);
        SetRef(so, "inputSection",     inSection);
        SetRef(so, "inputCount",       inCount);
        SetRef(so, "btnCreateConfirm", btnCreateOk);
        SetRef(so, "btnCreateCancel",  btnCreateCancel);
        SetRef(so, "createError",      createError);
        SetRef(so, "codesModal",       codesModal);
        SetRef(so, "codesTitle",       codesTitle);
        SetRef(so, "codesContent",     codesContent);
        SetRef(so, "codeRowTemplate",  codeRow);
        SetRef(so, "btnCodesClose",    btnCodesClose);
        SetRef(so, "stopModal",        stopModal);
        SetRef(so, "stopMessage",      stopMsg);
        SetRef(so, "btnStopConfirm",   btnStopOk);
        SetRef(so, "btnStopCancel",    btnStopCancel);
        SetRef(so, "noticeRoot",       noticeRoot);
        SetRef(so, "noticeText",       noticeText);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[MisClasesBuilder] ✓ MisClasesScene construida.");
        EditorUtility.DisplayDialog("¡Listo!",
            "MisClasesScene construida y guardada.\n\n" +
            "Recuerda: File → Build Settings → Add Open Scenes.", "OK");
    }

    // ── Bloques ───────────────────────────────────────────────────────────────

    static Button BuildHeader(Transform root)
    {
        var header = MakeEmpty(root, "Header");
        var rt = header.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -110f); rt.offsetMax = new Vector2(0f, 0f);
        var img = header.AddComponent<Image>(); img.color = PANEL; img.raycastTarget = false;

        var btn = MakeButton(header.transform, "BtnAtras", "Atrás", PURPLE,
                             new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(150f, 56f), 24f);

        MakeText(header.transform, "Title", "Mis clases",
                 new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 60f),
                 38f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        return btn;
    }

    static void BuildTeacherBar(Transform root, out GameObject bar, out Button btnCrear)
    {
        bar = MakeEmpty(root, "TeacherBar");
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(22f, -196f); rt.offsetMax = new Vector2(-22f, -122f);

        btnCrear = MakeButton(bar.transform, "BtnCrearClase", "+ Crear clase", CYAN,
                              new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 0f), 28f, stretch: true);
        bar.SetActive(false);
    }

    static GameObject BuildLoading(Transform root)
    {
        var go = MakeEmpty(root, "LoadingGroup"); Stretch(go);
        MakeText(go.transform, "Label", "Cargando tus clases…",
                 new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 60f),
                 28f, DIM, TextAlignmentOptions.Center, FontStyles.Normal);
        return go;
    }

    static void BuildEmptyState(Transform root, out GameObject empty, out TextMeshProUGUI label)
    {
        empty = MakeEmpty(root, "EmptyState"); Stretch(empty);
        label = MakeText(empty.transform, "EmptyLabel", "No tienes clases asignadas.",
                         new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 200f),
                         30f, DIM, TextAlignmentOptions.Center, FontStyles.Normal);
        empty.SetActive(false);
    }

    static void BuildList(Transform root, out GameObject listState,
                          out RectTransform content, out GameObject cardTemplate)
    {
        listState = MakeEmpty(root, "ListState");
        var rt = listState.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(22f, 22f); rt.offsetMax = new Vector2(-22f, -206f);

        var scrollGo = MakeEmpty(listState.transform, "ScrollView"); Stretch(scrollGo);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 26f;

        var vpGo = MakeEmpty(scrollGo.transform, "Viewport");
        var vpRT = vpGo.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = new Vector2(-16f, 0f);
        vpRT.pivot = new Vector2(0f, 1f);
        var vpImg = vpGo.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.02f);
        vpGo.AddComponent<RectMask2D>();

        var contentGo = MakeEmpty(vpGo.transform, "Content");
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f); content.anchoredPosition = Vector2.zero;
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f; vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlWidth = true;  vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vpRT; scroll.content = content;

        cardTemplate = BuildCardTemplate(contentGo.transform);
        cardTemplate.SetActive(false);

        listState.SetActive(false);
    }

    static GameObject BuildCardTemplate(Transform parent)
    {
        var card = MakeEmpty(parent, "CardTemplate");
        var le = card.AddComponent<LayoutElement>(); le.preferredHeight = 168f;
        var img = card.AddComponent<Image>();
        img.sprite = rounded; img.type = Image.Type.Sliced; img.color = CARD;

        MakeText(card.transform, "Name", "Nombre de la clase",
                 new Vector2(0f, 1f), new Vector2(26f, -30f), new Vector2(620f, 44f),
                 30f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

        MakeText(card.transform, "Subtitle", "3A · docente",
                 new Vector2(0f, 1f), new Vector2(26f, -72f), new Vector2(620f, 34f),
                 22f, DIM, TextAlignmentOptions.Left, FontStyles.Normal);

        MakeText(card.transform, "Code", "Tu código: 3A_07",
                 new Vector2(0f, 1f), new Vector2(26f, -110f), new Vector2(620f, 34f),
                 22f, ACCENT, TextAlignmentOptions.Left, FontStyles.Bold);

        // Badge de estado (arriba a la derecha)
        var badge = MakeEmpty(card.transform, "StatusBadge");
        SetRT(badge, new Vector2(1f, 1f), new Vector2(-150f, -32f), new Vector2(190f, 46f));
        var bImg = badge.AddComponent<Image>();
        bImg.sprite = rounded; bImg.type = Image.Type.Sliced; bImg.color = PANEL;
        MakeText(badge.transform, "Status", "Sin iniciar",
                 new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(186f, 42f),
                 20f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // Acciones (abajo a la derecha). El manager muestra las que tocan según rol.
        MakeButton(card.transform, "BtnEnter", "Entrar", CYAN,
                   new Vector2(1f, 0f), new Vector2(-120f, 40f), new Vector2(200f, 56f), 24f);
        MakeButton(card.transform, "BtnStart", "Iniciar", GREEN,
                   new Vector2(1f, 0f), new Vector2(-340f, 40f), new Vector2(200f, 56f), 24f);
        MakeButton(card.transform, "BtnStop", "Terminar", RED,
                   new Vector2(1f, 0f), new Vector2(-120f, 40f), new Vector2(200f, 56f), 24f);

        return card;
    }

    static void BuildCreateModal(Transform root, out GameObject modal,
                                 out TMP_InputField inName, out TMP_InputField inSection,
                                 out TMP_InputField inCount, out Button btnOk, out Button btnCancel,
                                 out TextMeshProUGUI error)
    {
        modal = MakeModalRoot(root, "CreateModal", new Vector2(820f, 620f), out var panel);

        MakeText(panel.transform, "Title", "Nueva clase",
                 new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(700f, 50f),
                 34f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        MakeText(panel.transform, "LabelName", "Nombre de la clase",
                 new Vector2(0f, 1f), new Vector2(60f, -110f), new Vector2(400f, 34f),
                 22f, DIM, TextAlignmentOptions.Left, FontStyles.Normal);
        inName = MakeInput(panel.transform, "InputName", "Química 3A",
                           new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(700f, 62f));

        MakeText(panel.transform, "LabelSection", "Sección (2 a 10 letras o números)",
                 new Vector2(0f, 1f), new Vector2(60f, -212f), new Vector2(560f, 34f),
                 22f, DIM, TextAlignmentOptions.Left, FontStyles.Normal);
        inSection = MakeInput(panel.transform, "InputSection", "3A",
                              new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(700f, 62f));

        MakeText(panel.transform, "LabelCount", "Cantidad de alumnos (1 a 50)",
                 new Vector2(0f, 1f), new Vector2(60f, -314f), new Vector2(560f, 34f),
                 22f, DIM, TextAlignmentOptions.Left, FontStyles.Normal);
        inCount = MakeInput(panel.transform, "InputCount", "25",
                            new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(700f, 62f));
        inCount.contentType = TMP_InputField.ContentType.IntegerNumber;

        error = MakeText(panel.transform, "CreateError", "",
                         new Vector2(0.5f, 1f), new Vector2(0f, -424f), new Vector2(700f, 64f),
                         21f, RED, TextAlignmentOptions.Center, FontStyles.Normal);

        btnCancel = MakeButton(panel.transform, "BtnCreateCancel", "Cancelar", PURPLE,
                               new Vector2(0.5f, 0f), new Vector2(-180f, 58f), new Vector2(300f, 64f), 26f);
        btnOk     = MakeButton(panel.transform, "BtnCreateConfirm", "Crear clase", CYAN,
                               new Vector2(0.5f, 0f), new Vector2(180f, 58f), new Vector2(300f, 64f), 26f);

        modal.SetActive(false);
    }

    static void BuildCodesModal(Transform root, out GameObject modal, out TextMeshProUGUI title,
                                out RectTransform content, out GameObject rowTemplate, out Button btnClose)
    {
        modal = MakeModalRoot(root, "CodesModal", new Vector2(900f, 820f), out var panel);

        title = MakeText(panel.transform, "CodesTitle", "Códigos de la clase",
                         new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(800f, 50f),
                         32f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // El aviso no es decorativo: si el docente cierra sin copiar, hay que reponer
        // las contraseñas alumno por alumno.
        var warn = MakeEmpty(panel.transform, "Warning");
        SetRT(warn, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(800f, 70f));
        var wImg = warn.AddComponent<Image>();
        wImg.sprite = rounded; wImg.type = Image.Type.Sliced; wImg.color = AMBER;
        MakeText(warn.transform, "Text", "Cópialas ahora: estas contraseñas no se pueden volver a ver.",
                 new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 64f),
                 21f, Hex("1E2050"), TextAlignmentOptions.Center, FontStyles.Bold);

        var scrollGo = MakeEmpty(panel.transform, "ScrollView");
        var sRT = scrollGo.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0f); sRT.anchorMax = new Vector2(1f, 1f);
        sRT.offsetMin = new Vector2(40f, 120f); sRT.offsetMax = new Vector2(-40f, -158f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var vpGo = MakeEmpty(scrollGo.transform, "Viewport"); Stretch(vpGo);
        vpGo.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
        var vpImg = vpGo.AddComponent<Image>(); vpImg.color = new Color(0f, 0f, 0f, 0.18f);
        vpGo.AddComponent<RectMask2D>();

        var contentGo = MakeEmpty(vpGo.transform, "Content");
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f); content.anchoredPosition = Vector2.zero;
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f; vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = vpGo.GetComponent<RectTransform>(); scroll.content = content;

        // Fila plantilla: código a la izquierda, contraseña a la derecha.
        rowTemplate = MakeEmpty(contentGo.transform, "CodeRowTemplate");
        var rle = rowTemplate.AddComponent<LayoutElement>(); rle.preferredHeight = 54f;
        var rImg = rowTemplate.AddComponent<Image>();
        rImg.sprite = rounded; rImg.type = Image.Type.Sliced; rImg.color = PANEL;
        MakeText(rowTemplate.transform, "Code", "3A_01",
                 new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(260f, 44f),
                 24f, ACCENT, TextAlignmentOptions.Left, FontStyles.Bold);
        MakeText(rowTemplate.transform, "Password", "Luna-4827",
                 new Vector2(1f, 0.5f), new Vector2(-220f, 0f), new Vector2(400f, 44f),
                 24f, Color.white, TextAlignmentOptions.Right, FontStyles.Normal);
        rowTemplate.SetActive(false);

        btnClose = MakeButton(panel.transform, "BtnCodesClose", "Ya las copié", CYAN,
                              new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(380f, 64f), 26f);

        modal.SetActive(false);
    }

    static void BuildStopModal(Transform root, out GameObject modal, out TextMeshProUGUI msg,
                               out Button btnOk, out Button btnCancel)
    {
        modal = MakeModalRoot(root, "StopModal", new Vector2(820f, 420f), out var panel);

        MakeText(panel.transform, "Title", "¿Terminar la clase?",
                 new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(700f, 52f),
                 34f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        msg = MakeText(panel.transform, "StopMessage", "Esto es definitivo.",
                       new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(700f, 160f),
                       23f, DIM, TextAlignmentOptions.Center, FontStyles.Normal);

        btnCancel = MakeButton(panel.transform, "BtnStopCancel", "Cancelar", PURPLE,
                               new Vector2(0.5f, 0f), new Vector2(-180f, 58f), new Vector2(300f, 64f), 26f);
        btnOk     = MakeButton(panel.transform, "BtnStopConfirm", "Sí, terminar", RED,
                               new Vector2(0.5f, 0f), new Vector2(180f, 58f), new Vector2(300f, 64f), 26f);

        modal.SetActive(false);
    }

    static void BuildNotice(Transform root, out GameObject noticeRoot, out TextMeshProUGUI text)
    {
        noticeRoot = MakeEmpty(root, "Notice");
        SetRT(noticeRoot, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1000f, 76f));
        var img = noticeRoot.AddComponent<Image>();
        img.sprite = rounded; img.type = Image.Type.Sliced; img.color = Hex("C0392B");
        text = MakeText(noticeRoot.transform, "NoticeText", "",
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 68f),
                        23f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        noticeRoot.SetActive(false);
    }

    // ── Helpers de UI ─────────────────────────────────────────────────────────

    static GameObject MakeModalRoot(Transform root, string name, Vector2 panelSize, out GameObject panel)
    {
        var modal = MakeEmpty(root, name); Stretch(modal);
        var dim = modal.AddComponent<Image>(); dim.color = new Color(0f, 0f, 0f, 0.72f);

        panel = MakeEmpty(modal.transform, "Panel");
        SetRT(panel, new Vector2(0.5f, 0.5f), Vector2.zero, panelSize);
        var pImg = panel.AddComponent<Image>();
        pImg.sprite = rounded; pImg.type = Image.Type.Sliced; pImg.color = MODAL_BG;
        return modal;
    }

    static Button MakeButton(Transform parent, string name, string label, Color color,
                             Vector2 anchor, Vector2 pos, Vector2 size, float fontSize,
                             bool stretch = false)
    {
        var go = MakeEmpty(parent, name);
        if (stretch)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        else SetRT(go, anchor, pos, size);

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

    static TMP_InputField MakeInput(Transform parent, string name, string placeholder,
                                    Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, anchor, pos, size);
        var img = go.AddComponent<Image>();
        img.sprite = uiSpr; img.type = Image.Type.Sliced; img.color = Hex("141738");

        var input = go.AddComponent<TMP_InputField>();

        var area = MakeEmpty(go.transform, "Text Area");
        var aRT = area.GetComponent<RectTransform>();
        aRT.anchorMin = Vector2.zero; aRT.anchorMax = Vector2.one;
        aRT.offsetMin = new Vector2(18f, 6f); aRT.offsetMax = new Vector2(-18f, -6f);
        area.AddComponent<RectMask2D>();

        var phGo = MakeEmpty(area.transform, "Placeholder"); Stretch(phGo);
        var ph = phGo.AddComponent<TextMeshProUGUI>();
        ph.text = placeholder; ph.font = fnt; ph.fontSize = 24f;
        ph.color = new Color(1f, 1f, 1f, 0.35f);
        ph.alignment = TextAlignmentOptions.Left; ph.raycastTarget = false;

        var txtGo = MakeEmpty(area.transform, "Text"); Stretch(txtGo);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text = ""; txt.font = fnt; txt.fontSize = 24f;
        txt.color = Color.white; txt.alignment = TextAlignmentOptions.Left;
        txt.raycastTarget = false;

        input.textViewport  = aRT;
        input.textComponent = txt;
        input.placeholder   = ph;
        input.targetGraphic = img;
        input.lineType      = TMP_InputField.LineType.SingleLine;
        return input;
    }

    // ── Utilidades ────────────────────────────────────────────────────────────

    static Scene OpenOrCreateScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.path == ScenePath) return active;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return active;

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
        else Debug.LogWarning($"[MisClasesBuilder] No existe la propiedad '{prop}' en el manager.");
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
