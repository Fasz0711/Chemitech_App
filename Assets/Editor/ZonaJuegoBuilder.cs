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

        // RotateCatcher.cam (también lo setea el manager en runtime)
        var catcher = FindChild(canvasGo.transform, "RotateCatcher")?.GetComponent<DragRotateCatcher>();
        if (catcher) catcher.cam = orbit;

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
            "Arrastra para rotar · d-pad mueve · flechas zoom · Recentrar resetea.",
            "OK");
    }

    // ── HUD ───────────────────────────────────────────────────────────────────
    static GameObject BuildHud(Transform canvas, OrbitCameraController orbit, TMP_FontAsset fnt,
        Sprite rounded, Sprite circleSpr, Sprite beakerSpr, Sprite uiSpr, Sprite triangle)
    {
        // RotateCatcher (transparente, al fondo, captura arrastres)
        var catcher = UI(canvas, "RotateCatcher", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        Stretch(catcher);
        var catcherImg = catcher.AddComponent<Image>();
        catcherImg.color = new Color(0,0,0,0); catcherImg.raycastTarget = true;
        var drc = catcher.AddComponent<DragRotateCatcher>(); drc.cam = orbit;

        // Contenedor HUD (delante del catcher)
        var hud = UI(canvas, "HUD", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        Stretch(hud);
        var hudT = hud.transform;

        // ── Barra superior ────────────────────────────────────────────────────
        // Pausa (top-left)
        var pause = MakeButton(hudT, "BtnPause", new(0,1), new(0,1), new(0,1), new(24,-22), new(58,58), rounded, Hex("7C4DFF"));
        Label(pause.transform, fnt, "II", 26f, FontStyles.Bold, Color.white);

        // Mover (cyan, junto a pausa)
        var mover = MakeButton(hudT, "BtnMover", new(0,1), new(0,1), new(0,1), new(96,-22), new(214,58), rounded, Hex("19A7CE"));
        var moverCircle = UI(mover.transform, "MoverIndicator", new(0,0.5f), new(0,0.5f), new(0,0.5f), new(20,0), new(34,34));
        var moverImg = moverCircle.AddComponent<Image>(); moverImg.sprite = circleSpr; moverImg.color = Color.white;
        var moverLblGo = UI(mover.transform, "Label", new(0,0), new(1,1), new(0.5f,0.5f), new(24,0), Vector2.zero);
        Stretch(moverLblGo); var moverLbl = moverLblGo.AddComponent<TextMeshProUGUI>();
        moverLbl.text="Mover"; moverLbl.font=fnt; moverLbl.fontSize=28f; moverLbl.fontStyle=FontStyles.Bold;
        moverLbl.color=Color.white; moverLbl.alignment=TextAlignmentOptions.Center; moverLbl.overflowMode=TextOverflowModes.Overflow;

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

        // ── Hint "Arrastra para rotar" (centro-derecha, arriba) ───────────────
        var hint = UI(hudT, "RotateHint", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), new(150,170), new(220,44));
        var hintCircle = UI(hint.transform, "HandIcon", new(0,0.5f), new(0,0.5f), new(0,0.5f), new(2,0), new(40,40));
        var hcImg = hintCircle.AddComponent<Image>(); hcImg.sprite=circleSpr; hcImg.color=Hex("2FD2E0");
        Label(hintCircle.transform, fnt, "✋", 20f, FontStyles.Normal, Color.white);
        var hintPill = UI(hint.transform, "HintPill", new(0,0.5f), new(0,0.5f), new(0,0.5f), new(46,0), new(174,38));
        var hpImg = hintPill.AddComponent<Image>(); hpImg.sprite=rounded; hpImg.type=Image.Type.Sliced; hpImg.color=Hex("19A7CE");
        Label(hintPill.transform, fnt, "Arrastra para rotar", 17f, FontStyles.Bold, Color.white);

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

        // ── Hotbar (6 slots, abajo-centro) ────────────────────────────────────
        const int SLOTS = 6; const float SLOT = 64f, GAP = 8f;
        float innerW = SLOTS * SLOT + (SLOTS - 1) * GAP;
        var bar = UI(hudT, "Hotbar", new(0.5f,0), new(0.5f,0), new(0.5f,0), new(0,40), new(innerW + 28f, SLOT + 24f));
        var barBorder = bar.AddComponent<Image>(); barBorder.sprite=rounded; barBorder.type=Image.Type.Sliced; barBorder.color=Color.white;
        var barInner = UI(bar.transform, "Inner", new(0,0), new(1,1), new(0.5f,0.5f), Vector2.zero, Vector2.zero);
        var barInnerRT = barInner.GetComponent<RectTransform>();
        barInnerRT.anchorMin=Vector2.zero; barInnerRT.anchorMax=Vector2.one;
        barInnerRT.offsetMin=new Vector2(3,3); barInnerRT.offsetMax=new Vector2(-3,-3);
        var barInnerImg = barInner.AddComponent<Image>(); barInnerImg.sprite=rounded; barInnerImg.type=Image.Type.Sliced; barInnerImg.color=Hex("12163A");
        float startX = -innerW/2f + SLOT/2f;
        for (int i = 0; i < SLOTS; i++)
        {
            float x = startX + i * (SLOT + GAP);
            var slot = MakeButton(barInner.transform, $"Slot_{i}", new(0.5f,0.5f), new(0.5f,0.5f), new(0.5f,0.5f), new(x,0), new(SLOT,SLOT), rounded, Hex("20244F"));
        }

        // ── "Presiona para colocar átomo" (abajo-izquierda) ───────────────────
        var hintPlace = UI(hudT, "PlaceHint", new(0,0), new(0,0), new(0,0), new(24,34), new(230,40));
        var hpImg2 = hintPlace.AddComponent<Image>(); hpImg2.sprite=rounded; hpImg2.type=Image.Type.Sliced; hpImg2.color=Hex("14183C");
        Label(hintPlace.transform, fnt, "Presiona para colocar átomo", 15f, FontStyles.Normal, new Color(1,1,1,0.85f));

        // ── Selector de átomos (abajo-derecha) ────────────────────────────────
        var selector = MakeButton(hudT, "BtnSelector", new(1,0), new(1,0), new(1,0), new(-24,32), new(190,76), rounded, Hex("F1C40F"));
        var selIcon = UI(selector.transform, "Icon", new(0,0.5f), new(0,0.5f), new(0,0.5f), new(16,0), new(34,34));
        var selIconImg = selIcon.AddComponent<Image>(); selIconImg.sprite=beakerSpr; selIconImg.preserveAspect=true; selIconImg.color=Hex("23204A");
        var selLblGo = UI(selector.transform, "Label", new(0,0), new(1,1), new(0.5f,0.5f), new(20,0), Vector2.zero);
        Stretch(selLblGo); var selLbl = selLblGo.AddComponent<TextMeshProUGUI>();
        selLbl.text="Selector de\nátomos"; selLbl.font=fnt; selLbl.fontSize=20f; selLbl.fontStyle=FontStyles.Bold;
        selLbl.color=Hex("23204A"); selLbl.alignment=TextAlignmentOptions.Center; selLbl.enableWordWrapping=true;

        return hud;
    }

    // ── Cableado del manager ──────────────────────────────────────────────────
    static void WireManager(ZonaJuegoManager mgr, OrbitCameraController orbit, Transform hud)
    {
        var so = new SerializedObject(mgr);
        SetRef(so, "cam", orbit);
        SetRef(so, "rotateCatcher", FindChildInParents(hud, "RotateCatcher")?.GetComponent<DragRotateCatcher>());
        SetRef(so, "txtUniverse",  FindChild(hud, "UniverseName")?.GetComponent<TextMeshProUGUI>());
        SetRef(so, "txtTimer",     FindChild(hud, "Timer")?.GetComponent<TextMeshProUGUI>());
        SetRef(so, "btnPause",     FindChild(hud, "BtnPause")?.GetComponent<Button>());
        SetRef(so, "btnMover",     FindChild(hud, "BtnMover")?.GetComponent<Button>());
        SetRef(so, "btnRecentrar", FindChild(hud, "BtnRecentrar")?.GetComponent<Button>());
        SetRef(so, "moverIndicator", FindChild(hud, "MoverIndicator")?.GetComponent<Image>());
        SetRef(so, "padUp",    FindChild(hud, "PadUp")?.GetComponent<HoldButton>());
        SetRef(so, "padDown",  FindChild(hud, "PadDown")?.GetComponent<HoldButton>());
        SetRef(so, "padLeft",  FindChild(hud, "PadLeft")?.GetComponent<HoldButton>());
        SetRef(so, "padRight", FindChild(hud, "PadRight")?.GetComponent<HoldButton>());
        SetRef(so, "vertUp",   FindChild(hud, "VertUp")?.GetComponent<HoldButton>());
        SetRef(so, "vertDown", FindChild(hud, "VertDown")?.GetComponent<HoldButton>());
        SetRef(so, "btnSelector", FindChild(hud, "BtnSelector")?.GetComponent<Button>());

        var slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = 6;
        for (int i = 0; i < 6; i++)
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                FindChild(hud, $"Slot_{i}")?.GetComponent<Button>();

        so.ApplyModifiedProperties();
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
