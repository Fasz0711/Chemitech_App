using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builder ADITIVO de ZonaJuegoScene (esqueleto): escena 3D (cámara orbital URP,
/// luz, plataforma de referencia) + HUD completo cableado a stubs/cámara.
/// Si la escena ya existe, abre y solo agrega lo que falta (find-or-create).
/// </summary>
public static class ZonaJuegoBuilder
{
    const string ScenePath = "Assets/Scenes/ZonaJuegoScene.unity";
    const float  RW = 1600f, RH = 900f;

    [MenuItem("ChemiTech/Build Zona Juego Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Zona Juego Scene (aditivo)",
            "Crea/abre ZonaJuegoScene y agrega cámara 3D + HUD.\nNo borra lo que ya tengas.\n\n¿Continuar?",
            "Sí, continuar", "Cancelar"))
            return;

        // ── Abrir o crear la escena ───────────────────────────────────────────
        Scene scene;
        if (System.IO.File.Exists(ScenePath))
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

        // ── Assets ────────────────────────────────────────────────────────────
        var fnt       = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        var rounded   = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        var circleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
        var beakerSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/icon-beaker.png");
        var uiSpr     = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var triangle  = GetTriangleSprite();

        // ── 1) Cámara 3D ──────────────────────────────────────────────────────
        var camGo = EnsureRoot(scene, "Main Camera");
        camGo.tag = "MainCamera";
        var cam = Ensure<Camera>(camGo);
        cam.orthographic    = false;
        cam.fieldOfView     = 52f;
        cam.nearClipPlane   = 0.1f;
        cam.farClipPlane    = 250f;
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("0A1233");
        Ensure<AudioListener>(camGo);
        var orbit = Ensure<OrbitCameraController>(camGo);
        camGo.transform.SetPositionAndRotation(new Vector3(0f, 9f, -14f), Quaternion.Euler(28f, 0f, 0f));

        // ── 2) Luz direccional ────────────────────────────────────────────────
        var lightGo = EnsureRoot(scene, "Directional Light");
        var light = Ensure<Light>(lightGo);
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = Hex("FFF3E0");
        lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        // ── 3) Plataforma de referencia (para ver la rotación 3D) ─────────────
        var area = EnsureRoot(scene, "PlayArea");
        if (area.transform.childCount == 0)
        {
            var platMat = GetOrCreateMat("Assets/Materials/Zone_Platform.mat", Hex("1A2150"), 0.1f, 0.55f);
            var postMat = GetOrCreateMat("Assets/Materials/Zone_Post.mat",     Hex("2FD2E0"), 0.2f, 0.6f, Hex("103A40"));

            var plat = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plat.name = "Platform";
            plat.transform.SetParent(area.transform, false);
            plat.transform.localScale = new Vector3(2.6f, 1f, 2.6f); // ~26x26
            plat.GetComponent<Renderer>().sharedMaterial = platMat;

            Vector2[] corners = { new(-10,-10), new(10,-10), new(-10,10), new(10,10) };
            for (int i = 0; i < corners.Length; i++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = $"Post_{i}";
                post.transform.SetParent(area.transform, false);
                post.transform.localScale    = new Vector3(0.5f, 2.2f, 0.5f);
                post.transform.localPosition = new Vector3(corners[i].x, 1.1f, corners[i].y);
                post.GetComponent<Renderer>().sharedMaterial = postMat;
            }
        }

        // ── 4) EventSystem ────────────────────────────────────────────────────
        var esGo = EnsureRoot(scene, "EventSystem");
        Ensure<EventSystem>(esGo);
        Ensure<StandaloneInputModule>(esGo);

        // ── 5) Canvas (overlay) ───────────────────────────────────────────────
        var canvasGo = EnsureRoot(scene, "Canvas");
        var canvas = Ensure<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = Ensure<CanvasScaler>(canvasGo);
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RW, RH);
        scaler.matchWidthOrHeight  = 0.5f;
        Ensure<GraphicRaycaster>(canvasGo);

        // ── 6) HUD (UI generada: se regenera en cada build para aplicar fixes) ─
        var oldHud = FindChild(canvasGo.transform, "HUD");
        if (oldHud) Object.DestroyImmediate(oldHud);
        var oldCatcher = FindChild(canvasGo.transform, "RotateCatcher");
        if (oldCatcher) Object.DestroyImmediate(oldCatcher);
        var hud = BuildHud(canvasGo.transform, orbit, fnt, rounded, circleSpr, beakerSpr, uiSpr, triangle);

        // ── 7) Manager + cableado ─────────────────────────────────────────────
        var mgrGo = FindChild(canvasGo.transform, "ZonaJuegoManager") ?? NewChild(canvasGo.transform, "ZonaJuegoManager");
        var mgr = Ensure<ZonaJuegoManager>(mgrGo);
        WireManager(mgr, orbit, hud.transform);

        // ── 8) Colocación de átomos en 3D ─────────────────────────────────────
        var atomMat = GetOrCreateMat("Assets/Materials/Atom_Base.mat", Color.white, 0.1f, 0.45f);
        var placeGo = FindChild(canvasGo.transform, "AtomPlacement") ?? NewChild(canvasGo.transform, "AtomPlacement");
        var place = Ensure<AtomPlacementController>(placeGo);
        var pso = new SerializedObject(place);
        SetRef(pso, "cam",              cam);
        SetRef(pso, "orbit",            orbit);
        SetRef(pso, "atomBaseMaterial", atomMat);
        SetRef(pso, "reticleRoot",      FindChild(hud.transform, "PlacementReticle"));
        SetRef(pso, "reticleDot",       FindChild(hud.transform, "Dot")?.GetComponent<Image>());
        SetRef(pso, "btnPlace",         FindChild(hud.transform, "BtnPlace")?.GetComponent<Button>());
        SetRef(pso, "btnDeleteRoot",    FindChild(hud.transform, "DeleteBar"));
        SetRef(pso, "btnDelete",        FindChild(hud.transform, "DeleteBar")?.GetComponent<Button>());
        SetRef(pso, "labelFont",        fnt);
        pso.ApplyModifiedProperties();

        // selector → placement
        var selCtrl = FindChild(canvasGo.transform, "AtomSelector")?.GetComponent<AtomSelectorController>();
        if (selCtrl) { var sso = new SerializedObject(selCtrl); SetRef(sso, "placement", place); sso.ApplyModifiedProperties(); }

        // manager → placement
        var mso = new SerializedObject(mgr); SetRef(mso, "place", place); mso.ApplyModifiedProperties();

        // ── Guardar ───────────────────────────────────────────────────────────
        if (!System.IO.File.Exists(ScenePath))
            EditorSceneManager.SaveScene(scene, ScenePath);
        else
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        AssetDatabase.Refresh();

        Debug.Log("[ZonaJuegoBuilder] ✓ ZonaJuegoScene lista (cámara 3D + HUD).");
        EditorUtility.DisplayDialog("¡Listo!",
            "ZonaJuegoScene creada/actualizada.\n\n" +
            "File → Build Settings → Add Open Scenes\n" +
            "Arrastrar rota · d-pad/flechas mueven cámara o átomo · tocar coloca/selecciona.",
            "OK");
    }

    // ── HUD ───────────────────────────────────────────────────────────────────
    static GameObject BuildHud(Transform canvas, OrbitCameraController orbit, TMP_FontAsset fnt,
        Sprite rounded, Sprite circleSpr, Sprite beakerSpr, Sprite uiSpr, Sprite triangle)
    {
        // Contenedor HUD (la rotación por arrastre la maneja AtomPlacementController)
        var hud = UI(canvas, "HUD", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        Stretch(hud);
        var hudT = hud.transform;

        // ── Barra superior ────────────────────────────────────────────────────
        // Pausa (top-left)
        var pause = MakeButton(hudT, "BtnPause", new(0,1), new(0,1), new(0,1), new(24,-22), new(58,58), rounded, Hex("7C4DFF"));
        Label(pause.transform, fnt, "II", 26f, FontStyles.Bold, Color.white);

        // Pill Universo + timer (top-center)
        var pill = UI(hudT, "UniversoPill", new(0.5f,1), new(0.5f,1), new(0.5f,1), new(0,-22), new(390,56));
        var pillBorder = pill.AddComponent<Image>();
        pillBorder.sprite=rounded; pillBorder.type=Image.Type.Sliced; pillBorder.color=Color.white;
        var pillInner = UI(pill.transform, "Inner", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        var pillInnerRT = pillInner.GetComponent<RectTransform>();
        pillInnerRT.anchorMin=Vector2.zero; pillInnerRT.anchorMax=Vector2.one;
        pillInnerRT.offsetMin=new Vector2(3,3); pillInnerRT.offsetMax=new Vector2(-3,-3);
        var pillImg = pillInner.AddComponent<Image>(); pillImg.sprite=rounded; pillImg.type=Image.Type.Sliced; pillImg.color=Hex("14183C");
        if (beakerSpr != null)
        {
            var ic = UI(pillInner.transform, "Icon", new(0,0.5f), new(0,0.5f), new(0,0.5f), new(20,0), new(28,28));
            var icImg = ic.AddComponent<Image>(); icImg.sprite=beakerSpr; icImg.preserveAspect=true; icImg.color=Color.white;
        }
        var uniName = UI(pillInner.transform, "UniverseName", new(0,0), new(1,1), new(0.5f,0.5f), new(8,0), Vector2.zero);
        var uniNameRT = uniName.GetComponent<RectTransform>();
        uniNameRT.anchorMin=new Vector2(0,0); uniNameRT.anchorMax=new Vector2(0.62f,1);
        uniNameRT.offsetMin=new Vector2(54,0); uniNameRT.offsetMax=new Vector2(0,0);
        var uniNameTmp = uniName.AddComponent<TextMeshProUGUI>();
        uniNameTmp.text="Universo"; uniNameTmp.font=fnt; uniNameTmp.fontSize=24f; uniNameTmp.fontStyle=FontStyles.Bold;
        uniNameTmp.color=Color.white; uniNameTmp.alignment=TextAlignmentOptions.Left|TextAlignmentOptions.Midline;
        uniNameTmp.enableWordWrapping=false; uniNameTmp.overflowMode=TextOverflowModes.Ellipsis;
        var timer = UI(pillInner.transform, "Timer", new(0.62f,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        var timerRT = timer.GetComponent<RectTransform>();
        timerRT.anchorMin=new Vector2(0.62f,0); timerRT.anchorMax=new Vector2(1,1);
        timerRT.offsetMin=new Vector2(0,0); timerRT.offsetMax=new Vector2(-16,0);
        var timerTmp = timer.AddComponent<TextMeshProUGUI>();
        timerTmp.text="0:00:00"; timerTmp.font=fnt; timerTmp.fontSize=24f; timerTmp.fontStyle=FontStyles.Bold;
        timerTmp.color=Hex("3FE0D0"); timerTmp.alignment=TextAlignmentOptions.Left|TextAlignmentOptions.Midline;
        timerTmp.enableWordWrapping=false; timerTmp.overflowMode=TextOverflowModes.Overflow;

        // Recentrar (top-right)
        var recenter = MakeButton(hudT, "BtnRecentrar", new(1,1), new(1,1), new(1,1), new(-24,-22), new(186,56), rounded, Hex("B9A7F0"));
        Label(recenter.transform, fnt, "⟳  Recentrar", 24f, FontStyles.Bold, Hex("23204A"));

        // ── D-pad (izquierda-medio): pan sobre el plano ───────────────────────
        var pad = UI(hudT, "DPad", new(0,0.5f), new(0,0.5f), new(0,0.5f), new(110,-30), new(170,170));
        MakeHoldArrow(pad.transform, "PadUp",      0f, new(0,52),  rounded, triangle);
        MakeHoldArrow(pad.transform, "PadDown",  180f, new(0,-52), rounded, triangle);
        MakeHoldArrow(pad.transform, "PadLeft",   90f, new(-52,0), rounded, triangle);
        MakeHoldArrow(pad.transform, "PadRight", 270f, new(52,0),  rounded, triangle);

        // ── Flechas verticales (derecha-medio): subir/bajar la cámara ─────────
        var zoom = UI(hudT, "VertPad", new(1,0.5f), new(1,0.5f), new(1,0.5f), new(-70,-20), new(70,150));
        MakeHoldArrow(zoom.transform, "VertUp",     0f, new(0,40),  rounded, triangle);
        MakeHoldArrow(zoom.transform, "VertDown", 180f, new(0,-40), rounded, triangle);

        // ── Hotbar (6 slots ricos: icono + símbolo + badge) ───────────────────
        const int SLOTS = 6; const float SLOT = 64f, GAP = 8f;
        float innerW = SLOTS * SLOT + (SLOTS - 1) * GAP;
        var bar = UI(hudT, "Hotbar", new(0.5f,0), new(0.5f,0), new(0.5f,0), new(0,40), new(innerW + 28f, SLOT + 24f));
        var barBorder = bar.AddComponent<Image>(); barBorder.sprite=rounded; barBorder.type=Image.Type.Sliced; barBorder.color=Color.white;
        var barInner = UI(bar.transform, "Inner", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        var barInnerRT = barInner.GetComponent<RectTransform>();
        barInnerRT.anchorMin=Vector2.zero; barInnerRT.anchorMax=Vector2.one;
        barInnerRT.offsetMin=new Vector2(3,3); barInnerRT.offsetMax=new Vector2(-3,-3);
        var barInnerImg = barInner.AddComponent<Image>(); barInnerImg.sprite=rounded; barInnerImg.type=Image.Type.Sliced; barInnerImg.color=Hex("12163A");

        var slotButtons = new Button[SLOTS];
        var slotIcons   = new Image[SLOTS];
        var slotSymbols = new TextMeshProUGUI[SLOTS];
        var slotBadges  = new GameObject[SLOTS]; // sin números (quedan en null)
        float startX = -innerW/2f + SLOT/2f;
        for (int i = 0; i < SLOTS; i++)
        {
            float x = startX + i * (SLOT + GAP);
            var slot = MakeButton(barInner.transform, $"Slot_{i}", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), new(x,0), new(SLOT,SLOT), rounded, Hex("20244F"));
            slotButtons[i] = slot.GetComponent<Button>();

            var icon = UI(slot.transform, "Icon", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), Vector2.zero, new(46,46));
            var iconImg = icon.AddComponent<Image>(); iconImg.sprite=circleSpr; iconImg.color=Color.white; iconImg.raycastTarget=false;
            icon.SetActive(false); slotIcons[i] = iconImg;

            var symGo = UI(slot.transform, "Symbol", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), Vector2.zero, new(SLOT,SLOT));
            var symTmp = symGo.AddComponent<TextMeshProUGUI>();
            symTmp.text=""; symTmp.font=fnt; symTmp.fontSize=24f; symTmp.fontStyle=FontStyles.Bold; symTmp.color=Color.white;
            symTmp.alignment=TextAlignmentOptions.Center; symTmp.raycastTarget=false; symTmp.overflowMode=TextOverflowModes.Overflow;
            symGo.SetActive(false); slotSymbols[i] = symTmp;
        }

        // ── Botón "Presiona para colocar átomo" (abajo-izquierda) ─────────────
        var btnPlace = MakeButton(hudT, "BtnPlace", new(0,0), new(0,0), new(0,0), new(24,34), new(260,54), rounded, Hex("19A7CE"));
        Label(btnPlace.transform, fnt, "Presiona para colocar átomo", 16f, FontStyles.Bold, Color.white);

        // ── Selector de átomos (abajo-derecha) ────────────────────────────────
        var selector = MakeButton(hudT, "BtnSelector", new(1,0), new(1,0), new(1,0), new(-24,32), new(190,76), rounded, Hex("F1C40F"));
        var selIcon = UI(selector.transform, "Icon", new(0,0.5f), new(0,0.5f), new(0,0.5f), new(16,0), new(34,34));
        var selIconImg = selIcon.AddComponent<Image>(); selIconImg.sprite=beakerSpr; selIconImg.preserveAspect=true; selIconImg.color=Hex("23204A");
        var selLblGo = UI(selector.transform, "Label", new(0,0), new(1,1), new(0.5f,0.5f), new(20,0), Vector2.zero);
        Stretch(selLblGo); var selLbl = selLblGo.AddComponent<TextMeshProUGUI>();
        selLbl.text="Selector de\nátomos"; selLbl.font=fnt; selLbl.fontSize=20f; selLbl.fontStyle=FontStyles.Bold;
        selLbl.color=Hex("23204A"); selLbl.alignment=TextAlignmentOptions.Center; selLbl.enableWordWrapping=true;

        // ── Botón Eliminar (aparece solo al seleccionar un átomo) ─────────────
        var deleteBar = MakeButton(hudT, "DeleteBar", new(0.5f,0), new(0.5f,0), new(0.5f,0), new(0,150), new(220,56), rounded, Hex("E0484B"));
        Label(deleteBar.transform, fnt, "Eliminar átomo", 22f, FontStyles.Bold, Color.white);
        deleteBar.SetActive(false);

        // ── Retícula de colocación (cursor central, oculto hasta armar) ───────
        var reticle = UI(hudT, "PlacementReticle", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), Vector2.zero, new(44,44));
        var halo = UI(reticle.transform, "Ring", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), Vector2.zero, new(44,44));
        var haloImg = halo.AddComponent<Image>(); haloImg.sprite=circleSpr; haloImg.color=new Color(1,1,1,0.30f); haloImg.raycastTarget=false;
        var dot = UI(reticle.transform, "Dot", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), Vector2.zero, new(18,18));
        var dotImg = dot.AddComponent<Image>(); dotImg.sprite=circleSpr; dotImg.color=Color.white; dotImg.raycastTarget=false;
        reticle.SetActive(false);

        // ── Modal selector de átomos ──────────────────────────────────────────
        var refs = BuildAtomSelectorModal(hudT, fnt, rounded, circleSpr);

        // El hotbar debe renderizar SOBRE el dim del modal (queda interactivo)
        bar.transform.SetAsLastSibling();

        // ── Controller del selector + hotbar ──────────────────────────────────
        var ctrlGo = NewChild(hudT, "AtomSelector");
        var ctrl = ctrlGo.AddComponent<AtomSelectorController>();
        var cso = new SerializedObject(ctrl);
        SetRef(cso, "modalRoot",    refs.modalRoot);
        SetRef(cso, "btnOpen",      selector.GetComponent<Button>());
        SetRef(cso, "btnClose",     refs.btnClose);
        SetRef(cso, "searchInput",  refs.search);
        WireArr(cso, "filterButtons", refs.filterBtns);
        WireArr(cso, "filterBgs",     refs.filterBgs);
        WireArr(cso, "filterLabels",  refs.filterLabels);
        SetRef(cso, "gridContent",  refs.gridContent);
        SetRef(cso, "atomTemplate", refs.atomTemplate);
        SetRef(cso, "infoPopup",    refs.infoPopup);
        SetRef(cso, "infoText",     refs.infoText);
        WireArr(cso, "slotButtons", slotButtons);
        WireArr(cso, "slotIcons",   slotIcons);
        WireArr(cso, "slotSymbols", slotSymbols);
        WireArr(cso, "slotBadges",  slotBadges);
        SetRef(cso, "atomCircle",   circleSpr);
        cso.ApplyModifiedProperties();

        return hud;
    }

    // ── Modal del selector de átomos ──────────────────────────────────────────
    class SelRefs
    {
        public GameObject        modalRoot;
        public Button            btnClose;
        public TMP_InputField    search;
        public Button[]          filterBtns;
        public Image[]           filterBgs;
        public TextMeshProUGUI[] filterLabels;
        public RectTransform     gridContent;
        public GameObject        atomTemplate;
        public RectTransform     infoPopup;
        public TextMeshProUGUI   infoText;
    }

    static SelRefs BuildAtomSelectorModal(Transform parent, TMP_FontAsset fnt, Sprite rounded, Sprite circleSpr)
    {
        var r = new SelRefs();

        var modal = UI(parent, "AtomSelectorModal", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        Stretch(modal);
        r.modalRoot = modal;

        var dim = UI(modal.transform, "Dim", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        Stretch(dim);
        var dimImg = dim.AddComponent<Image>(); dimImg.color = new Color(0,0,0,0.62f); dimImg.raycastTarget = true;

        const float PW = 1000f, PH = 440f;
        var panel = UI(modal.transform, "Panel", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), new(0,46), new(PW,PH));
        var panelBorder = panel.AddComponent<Image>(); panelBorder.sprite=rounded; panelBorder.type=Image.Type.Sliced; panelBorder.color=Color.white;
        var inner = UI(panel.transform, "Inner", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        var innerRT = inner.GetComponent<RectTransform>();
        innerRT.anchorMin=Vector2.zero; innerRT.anchorMax=Vector2.one; innerRT.offsetMin=new Vector2(4,4); innerRT.offsetMax=new Vector2(-4,-4);
        var innerImg = inner.AddComponent<Image>(); innerImg.sprite=rounded; innerImg.type=Image.Type.Sliced; innerImg.color=Hex("1B1F46");

        // X cerrar (esquina superior derecha, sobresaliendo)
        var closeGo = UI(panel.transform, "BtnClose", new(1,1), new(1,1), new(0.5f,0.5f), Vector2.zero, new(54,54));
        var closeImg = closeGo.AddComponent<Image>(); closeImg.sprite=circleSpr; closeImg.color=Hex("E0484B");
        r.btnClose = closeGo.AddComponent<Button>(); r.btnClose.targetGraphic = closeImg;
        Label(closeGo.transform, fnt, "X", 26f, FontStyles.Bold, Color.white);

        // Buscador
        var searchGo = UI(inner.transform, "Search", new(0,1), new(0,1), new(0,1), new(24,-20), new(400,52));
        r.search = MakeSearchInput(searchGo, fnt, rounded, "Buscar átomo o número atómico...");

        // Filtros (5)
        string[] tabs = { "Todos", "Metales", "Gases", "No metales", "Metaloides" };
        float[]  tw   = { 76f, 96f, 76f, 104f, 104f };
        r.filterBtns = new Button[5]; r.filterBgs = new Image[5]; r.filterLabels = new TextMeshProUGUI[5];
        float tx = 24f + 400f + 16f;
        for (int i = 0; i < 5; i++)
        {
            var tab = UI(inner.transform, $"Filter_{i}", new(0,1), new(0,1), new(0,1), new(tx + tw[i]/2f, -46f), new(tw[i], 44f));
            var tabImg = tab.AddComponent<Image>(); tabImg.sprite=rounded; tabImg.type=Image.Type.Sliced;
            tabImg.color = (i==0) ? new Color(0.10f,0.65f,0.81f,1f) : new Color(1,1,1,0.06f);
            r.filterBtns[i] = tab.AddComponent<Button>(); r.filterBtns[i].targetGraphic = tabImg;
            r.filterBgs[i] = tabImg;
            var tlGo = UI(tab.transform, "Label", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero); Stretch(tlGo);
            var tl = tlGo.AddComponent<TextMeshProUGUI>();
            tl.text=tabs[i]; tl.font=fnt; tl.fontSize=17f; tl.fontStyle=FontStyles.Bold;
            tl.color = (i==0) ? Color.white : new Color(1,1,1,0.7f);
            tl.alignment=TextAlignmentOptions.Center; tl.raycastTarget=false; tl.overflowMode=TextOverflowModes.Overflow;
            r.filterLabels[i] = tl;
            tx += tw[i] + 8f;
        }

        // Scroll + grid
        var scrollGo = UI(inner.transform, "Scroll", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        var scrollRT = scrollGo.GetComponent<RectTransform>();
        scrollRT.anchorMin=Vector2.zero; scrollRT.anchorMax=Vector2.one; scrollRT.offsetMin=new Vector2(18,18); scrollRT.offsetMax=new Vector2(-18,-86);
        var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=28f;

        var vp = UI(scrollGo.transform, "Viewport", new(0,0), new(1,1), new(0f,1f), Vector2.zero, Vector2.zero);
        var vpRT = vp.GetComponent<RectTransform>(); vpRT.anchorMin=Vector2.zero; vpRT.anchorMax=Vector2.one; vpRT.offsetMin=Vector2.zero; vpRT.offsetMax=new Vector2(-14,0);
        var vpImg = vp.AddComponent<Image>(); vpImg.color=new Color(1,1,1,0.015f); vp.AddComponent<RectMask2D>();

        var content = UI(vp.transform, "Content", new(0,1), new(1,1), new(0.5f,1f), Vector2.zero, Vector2.zero);
        var contentRT = content.GetComponent<RectTransform>(); contentRT.anchorMin=new Vector2(0,1); contentRT.anchorMax=new Vector2(1,1); contentRT.pivot=new Vector2(0.5f,1f); contentRT.anchoredPosition=Vector2.zero;
        var glg = content.AddComponent<GridLayoutGroup>();
        glg.cellSize=new Vector2(96,96); glg.spacing=new Vector2(14,14); glg.padding=new RectOffset(6,6,6,6);
        glg.constraint=GridLayoutGroup.Constraint.FixedColumnCount; glg.constraintCount=8; glg.childAlignment=TextAnchor.UpperCenter;
        var fit = content.AddComponent<ContentSizeFitter>(); fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=vpRT; scroll.content=contentRT;
        r.gridContent = contentRT;

        r.atomTemplate = BuildAtomCell(content.transform, fnt, rounded, circleSpr);
        r.atomTemplate.SetActive(false);

        // Popup de info
        var popup = UI(modal.transform, "InfoPopup", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), Vector2.zero, new(250,52));
        var popBorder = popup.AddComponent<Image>(); popBorder.sprite=rounded; popBorder.type=Image.Type.Sliced; popBorder.color=Color.white;
        var popInner = UI(popup.transform, "Inner", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        var popInnerRT = popInner.GetComponent<RectTransform>(); popInnerRT.anchorMin=Vector2.zero; popInnerRT.anchorMax=Vector2.one; popInnerRT.offsetMin=new Vector2(3,3); popInnerRT.offsetMax=new Vector2(-3,-3);
        var popInnerImg = popInner.AddComponent<Image>(); popInnerImg.sprite=rounded; popInnerImg.type=Image.Type.Sliced; popInnerImg.color=Hex("0F1336");
        var popTxtGo = UI(popInner.transform, "Text", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero); Stretch(popTxtGo);
        r.infoText = popTxtGo.AddComponent<TextMeshProUGUI>();
        r.infoText.text="—"; r.infoText.font=fnt; r.infoText.fontSize=20f; r.infoText.fontStyle=FontStyles.Bold; r.infoText.color=Color.white;
        r.infoText.alignment=TextAlignmentOptions.Center; r.infoText.raycastTarget=false; r.infoText.overflowMode=TextOverflowModes.Overflow;
        r.infoPopup = popup.GetComponent<RectTransform>();
        popup.SetActive(false);

        modal.SetActive(false);
        return r;
    }

    static GameObject BuildAtomCell(Transform parent, TMP_FontAsset fnt, Sprite rounded, Sprite circleSpr)
    {
        var cell = UI(parent, "AtomTemplate", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), Vector2.zero, new(96,96));
        var cellImg = cell.AddComponent<Image>(); cellImg.color=new Color(0,0,0,0); cellImg.raycastTarget=true;
        var btn = cell.AddComponent<Button>(); btn.targetGraphic = cellImg;

        // Ring (selección) detrás de todo
        var ring = UI(cell.transform, "Ring", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        var ringRT = ring.GetComponent<RectTransform>(); ringRT.anchorMin=Vector2.zero; ringRT.anchorMax=Vector2.one; ringRT.offsetMin=new Vector2(-3,-3); ringRT.offsetMax=new Vector2(3,3);
        var ringImg = ring.AddComponent<Image>(); ringImg.sprite=rounded; ringImg.type=Image.Type.Sliced; ringImg.color=Hex("3FE0FF"); ringImg.raycastTarget=false;
        ring.SetActive(false);

        // Bg oscuro
        var bg = UI(cell.transform, "Bg", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero); Stretch(bg);
        var bgImg = bg.AddComponent<Image>(); bgImg.sprite=rounded; bgImg.type=Image.Type.Sliced; bgImg.color=Hex("20244F"); bgImg.raycastTarget=false;

        // Círculo de átomo
        var circ = UI(cell.transform, "Circle", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), Vector2.zero, new(70,70));
        var circImg = circ.AddComponent<Image>(); circImg.sprite=circleSpr; circImg.color=Color.white; circImg.preserveAspect=true; circImg.raycastTarget=false;

        // Símbolo
        var sym = UI(cell.transform, "Symbol", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), Vector2.zero, new(70,70));
        var symTmp = sym.AddComponent<TextMeshProUGUI>();
        symTmp.text="H"; symTmp.font=fnt; symTmp.fontSize=27f; symTmp.fontStyle=FontStyles.Bold; symTmp.color=Color.white;
        symTmp.alignment=TextAlignmentOptions.Center; symTmp.raycastTarget=false; symTmp.overflowMode=TextOverflowModes.Overflow;

        return cell;
    }

    static TMP_InputField MakeSearchInput(GameObject go, TMP_FontAsset fnt, Sprite rounded, string placeholder)
    {
        var bg = go.AddComponent<Image>(); bg.sprite=rounded; bg.type=Image.Type.Sliced; bg.color=Hex("0F1336");

        var area = new GameObject("Text Area", typeof(RectTransform));
        area.transform.SetParent(go.transform, false);
        var areaRT = area.GetComponent<RectTransform>();
        areaRT.anchorMin=Vector2.zero; areaRT.anchorMax=Vector2.one; areaRT.offsetMin=new Vector2(18,6); areaRT.offsetMax=new Vector2(-16,-6);
        area.AddComponent<RectMask2D>();

        var ph = new GameObject("Placeholder", typeof(RectTransform));
        ph.transform.SetParent(area.transform, false);
        var phRT = ph.GetComponent<RectTransform>(); phRT.anchorMin=Vector2.zero; phRT.anchorMax=Vector2.one; phRT.offsetMin=phRT.offsetMax=Vector2.zero;
        var phTmp = ph.AddComponent<TextMeshProUGUI>();
        phTmp.text=placeholder; phTmp.font=fnt; phTmp.fontSize=20f; phTmp.color=new Color(1,1,1,0.35f);
        phTmp.alignment=TextAlignmentOptions.Left|TextAlignmentOptions.Midline; phTmp.enableWordWrapping=false;

        var txt = new GameObject("Text", typeof(RectTransform));
        txt.transform.SetParent(area.transform, false);
        var txtRT = txt.GetComponent<RectTransform>(); txtRT.anchorMin=Vector2.zero; txtRT.anchorMax=Vector2.one; txtRT.offsetMin=txtRT.offsetMax=Vector2.zero;
        var txtTmp = txt.AddComponent<TextMeshProUGUI>();
        txtTmp.text=""; txtTmp.font=fnt; txtTmp.fontSize=20f; txtTmp.color=Color.white;
        txtTmp.alignment=TextAlignmentOptions.Left|TextAlignmentOptions.Midline; txtTmp.enableWordWrapping=false;

        var field = go.AddComponent<TMP_InputField>();
        var so = new SerializedObject(field);
        so.FindProperty("m_TextViewport").objectReferenceValue  = areaRT;
        so.FindProperty("m_TextComponent").objectReferenceValue = txtTmp;
        so.FindProperty("m_Placeholder").objectReferenceValue   = phTmp;
        so.FindProperty("m_TargetGraphic").objectReferenceValue = bg;
        so.FindProperty("m_ContentType").enumValueIndex         = (int)TMP_InputField.ContentType.Standard;
        so.FindProperty("m_LineType").enumValueIndex            = 0;
        so.ApplyModifiedProperties();
        field.interactable=true; field.customCaretColor=true; field.caretColor=Color.white; field.caretWidth=2; field.caretBlinkRate=0.85f;
        return field;
    }

    // ── Cableado del manager ──────────────────────────────────────────────────
    static void WireManager(ZonaJuegoManager mgr, OrbitCameraController orbit, Transform hud)
    {
        var so = new SerializedObject(mgr);
        SetRef(so, "cam", orbit);
        SetRef(so, "txtUniverse",  FindChild(hud, "UniverseName")?.GetComponent<TextMeshProUGUI>());
        SetRef(so, "txtTimer",     FindChild(hud, "Timer")?.GetComponent<TextMeshProUGUI>());
        SetRef(so, "btnPause",     FindChild(hud, "BtnPause")?.GetComponent<Button>());
        SetRef(so, "btnRecentrar", FindChild(hud, "BtnRecentrar")?.GetComponent<Button>());
        SetRef(so, "padUp",    FindChild(hud, "PadUp")?.GetComponent<HoldButton>());
        SetRef(so, "padDown",  FindChild(hud, "PadDown")?.GetComponent<HoldButton>());
        SetRef(so, "padLeft",  FindChild(hud, "PadLeft")?.GetComponent<HoldButton>());
        SetRef(so, "padRight", FindChild(hud, "PadRight")?.GetComponent<HoldButton>());
        SetRef(so, "vertUp",   FindChild(hud, "VertUp")?.GetComponent<HoldButton>());
        SetRef(so, "vertDown", FindChild(hud, "VertDown")?.GetComponent<HoldButton>());

        so.ApplyModifiedProperties();
    }

    static void WireArr<T>(SerializedObject so, string prop, T[] arr) where T : Object
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[ZonaJuegoBuilder] propiedad '{prop}' no existe."); return; }
        p.arraySize = arr.Length;
        for (int i = 0; i < arr.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
    }

    // ── Helpers de construcción ───────────────────────────────────────────────
    static GameObject MakeButton(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 piv,
        Vector2 pos, Vector2 size, Sprite rounded, Color color)
    {
        var go = UI(parent, name, aMin, aMax, piv, pos, size);
        var img = go.AddComponent<Image>();
        img.sprite = rounded; img.type = Image.Type.Sliced; img.color = color;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        return go;
    }

    static HoldButton MakeHoldArrow(Transform parent, string name, float rotZ, Vector2 pos, Sprite rounded, Sprite triangle)
    {
        var go = UI(parent, name, new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), pos, new(48,48));
        var img = go.AddComponent<Image>();
        img.sprite = rounded; img.type = Image.Type.Sliced; img.color = Hex("2A2D5A");
        var arrow = UI(go.transform, "Arrow", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), Vector2.zero, new(24,24));
        arrow.transform.localEulerAngles = new Vector3(0f, 0f, rotZ);
        var aImg = arrow.AddComponent<Image>();
        aImg.sprite = triangle; aImg.color = Color.white; aImg.preserveAspect = true; aImg.raycastTarget = false;
        return go.AddComponent<HoldButton>();
    }

    // Triángulo blanco apuntando hacia arriba (se rota por Image para cada dirección)
    static Sprite GetTriangleSprite()
    {
        const string path = "Assets/Sprites/ui_triangle.png";
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px  = new Color[s * s];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;
        float cx = (s - 1) / 2f, bottom = 0.16f * s, top = 0.86f * s;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                if (y < bottom || y > top) continue;
                float ty = (y - bottom) / (top - bottom);      // 0 base → 1 ápice
                float halfW = (1f - ty) * 0.36f * s;
                if (Mathf.Abs(x - cx) <= halfW) px[y * s + x] = Color.white;
            }
        tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.spritePixelsPerUnit = 100; ti.filterMode = FilterMode.Bilinear; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void Label(Transform parent, TMP_FontAsset fnt, string text, float size, FontStyles style, Color color)
    {
        var go = NewChild(parent, "Label"); Stretch(go);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = fnt; tmp.fontSize = size; tmp.fontStyle = style; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center; tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow; tmp.raycastTarget = false;
    }

    // ── Materiales URP ────────────────────────────────────────────────────────
    static Material GetOrCreateMat(string path, Color baseCol, float metallic, float smooth, Color emission = default)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", baseCol);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        if (emission != default)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", emission);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // ── Helpers de escena/jerarquía ───────────────────────────────────────────
    static GameObject EnsureRoot(Scene scene, string name)
    {
        foreach (var r in scene.GetRootGameObjects())
            if (r.name == name) return r;
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static GameObject FindChild(Transform parent, string name)
    {
        foreach (Transform c in parent)
        {
            if (c.name == name) return c.gameObject;
            var f = FindChild(c, name);
            if (f != null) return f;
        }
        return null;
    }

    // Busca empezando desde el padre del nodo dado (para RotateCatcher, hermano de HUD)
    static GameObject FindChildInParents(Transform node, string name)
    {
        var root = node.parent != null ? node.parent : node;
        return FindChild(root, name);
    }

    static GameObject NewChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static GameObject UI(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 piv, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = piv;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[ZonaJuegoBuilder] propiedad '{prop}' no existe."); return; }
        p.objectReferenceValue = value;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
