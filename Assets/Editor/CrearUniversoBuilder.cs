using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public static class CrearUniversoBuilder
{
    const float RW = 1600f, RH = 900f;
    const float PANEL_W   = 760f;
    const float HEADER_H  = 82f;
    const float CONTENT_H = 470f;
    const float GAP       = 16f;
    const float PREV_X    = -210f;
    const float FORM_X    =  115f;
    const float FORM_W    =  440f;
    const int   ICON_COUNT =   8;
    const int   COL_COUNT  =   6;

    [MenuItem("ChemiTech/Build Crear Universo Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Crear Universo Scene",
            "Esto creará Assets/Scenes/CrearUniversoScene.unity.\n¿Continuar?",
            "Sí, construir", "Cancelar"))
            return;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Assets ───────────────────────────────────────────────────────────
        var fnt        = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        var circleSpr  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
        var bgSpr      = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MainMenuBG.png");
        var roundedSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        var uiSpr      = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var personSpr  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/person-icon.png");

        // Generar los 8 íconos antes de construir la escena
        var iconSprites = GenerateIconSprites();

        // ── Cámara ───────────────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("0A1240");
        cam.orthographic    = true; cam.depth = -1;
        camGo.AddComponent<AudioListener>();

        // ── EventSystem ───────────────────────────────────────────────────────
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();

        // ── Canvas ────────────────────────────────────────────────────────────
        var cGo = new GameObject("Canvas");
        var cv  = cGo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var csc = cGo.AddComponent<CanvasScaler>();
        csc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        csc.referenceResolution = new Vector2(RW, RH);
        csc.matchWidthOrHeight  = 0.5f;
        cGo.AddComponent<GraphicRaycaster>();

        // ── Fondo ─────────────────────────────────────────────────────────────
        var bgGo  = MakeEmpty(cGo.transform, "Background");
        var bgRT  = bgGo.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bgGo.AddComponent<Image>().sprite = bgSpr;

        // ── Layout vertical ───────────────────────────────────────────────────
        float totalH    = HEADER_H + GAP + CONTENT_H;
        float headerCY  =  totalH / 2f - HEADER_H  / 2f;
        float contentCY = -totalH / 2f + CONTENT_H / 2f;

        // ── Header ────────────────────────────────────────────────────────────
        MakeBorderPanel(cGo.transform, "HeaderBorder", "HeaderPanel",
            new Vector2(0f, headerCY), new Vector2(PANEL_W, HEADER_H),
            roundedSpr, Hex("1E2050"), out var headerGo);

        const float BTN_SZ = 52f, H_PAD = 14f;
        float backX = -PANEL_W / 2f + H_PAD + BTN_SZ / 2f;

        var backGo  = MakeEmpty(headerGo.transform, "BtnAtras");
        SetRT(backGo, new Vector2(backX, 0f), new Vector2(BTN_SZ, BTN_SZ));
        var backImg = backGo.AddComponent<Image>();
        backImg.sprite = roundedSpr; backImg.type = Image.Type.Sliced; backImg.color = Hex("2A2D5A");
        var btnAtrasH = backGo.AddComponent<Button>(); btnAtrasH.targetGraphic = backImg;
        var bLbl = MakeEmpty(backGo.transform, "Label"); Stretch(bLbl);
        var bTmp = bLbl.AddComponent<TextMeshProUGUI>();
        bTmp.text = "<"; bTmp.font = fnt; bTmp.fontSize = 28f;
        bTmp.fontStyle = FontStyles.Bold; bTmp.color = Color.white;
        bTmp.alignment = TextAlignmentOptions.Center;

        float avatarX = backX + BTN_SZ / 2f + 8f + BTN_SZ / 2f;
        var avatarGo  = MakeEmpty(headerGo.transform, "AvatarIcon");
        SetRT(avatarGo, new Vector2(avatarX, 0f), new Vector2(BTN_SZ, BTN_SZ));
        var avatarImg = avatarGo.AddComponent<Image>();
        avatarImg.sprite = roundedSpr; avatarImg.type = Image.Type.Sliced; avatarImg.color = Hex("3DBE6C");
        if (personSpr != null)
        {
            var aGo = MakeEmpty(avatarGo.transform, "Icon");
            SetRT(aGo, Vector2.zero, new Vector2(BTN_SZ - 10f, BTN_SZ - 10f));
            var aImg = aGo.AddComponent<Image>();
            aImg.sprite = personSpr; aImg.preserveAspect = true;
        }

        float titleStartX = avatarX + BTN_SZ / 2f + 14f;
        float titleW      = PANEL_W / 2f - titleStartX - H_PAD;
        var hTitleGo  = MakeEmpty(headerGo.transform, "TitleLabel");
        SetRT(hTitleGo, new Vector2(titleStartX + titleW / 2f, 0f), new Vector2(titleW, HEADER_H));
        var hTitleTmp = hTitleGo.AddComponent<TextMeshProUGUI>();
        hTitleTmp.text = "Crear Universo"; hTitleTmp.font = fnt; hTitleTmp.fontSize = 36f;
        hTitleTmp.fontStyle = FontStyles.Bold; hTitleTmp.color = Color.white;
        hTitleTmp.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        hTitleTmp.overflowMode = TextOverflowModes.Overflow;

        // ── Panel principal ───────────────────────────────────────────────────
        MakeBorderPanel(cGo.transform, "PanelBorder", "MainPanel",
            new Vector2(0f, contentCY), new Vector2(PANEL_W, CONTENT_H),
            roundedSpr, Hex("242659"), out var panelGo);

        // ── Sub-panel vista previa ────────────────────────────────────────────
        var prevBgGo  = MakeEmpty(panelGo.transform, "PreviewBg");
        SetRT(prevBgGo, new Vector2(PREV_X, 8f), new Vector2(190f, 286f));
        var prevBgImg = prevBgGo.AddComponent<Image>();
        prevBgImg.sprite = roundedSpr; prevBgImg.type = Image.Type.Sliced; prevBgImg.color = Hex("1A1E45");

        var vpLblGo  = MakeEmpty(panelGo.transform, "VistaPreviaLabel");
        SetRT(vpLblGo, new Vector2(PREV_X, 148f), new Vector2(180f, 22f));
        var vpLblTmp = vpLblGo.AddComponent<TextMeshProUGUI>();
        vpLblTmp.text = "VISTA PREVIA"; vpLblTmp.font = fnt; vpLblTmp.fontSize = 15f;
        vpLblTmp.color = new Color(1f,1f,1f,0.5f); vpLblTmp.fontStyle = FontStyles.Bold;
        vpLblTmp.alignment = TextAlignmentOptions.Center; vpLblTmp.overflowMode = TextOverflowModes.Overflow;

        MakeSparkle(panelGo.transform, "SpkPrev1", new Vector2(PREV_X-70f, 110f), 18f);
        MakeSparkle(panelGo.transform, "SpkPrev2", new Vector2(PREV_X+72f,  98f), 14f);
        MakeSparkle(panelGo.transform, "SpkPrev3", new Vector2(PREV_X-70f, -20f), 13f);

        // Círculo de preview (fondo de color)
        var prevCircGo  = MakeEmpty(panelGo.transform, "PreviewCircle");
        SetRT(prevCircGo, new Vector2(PREV_X, 38f), new Vector2(145f, 145f));
        var prevCircImg = prevCircGo.AddComponent<Image>();
        prevCircImg.sprite = circleSpr; prevCircImg.type = Image.Type.Simple;
        prevCircImg.color  = Hex("4DD9E8");

        // Ícono superpuesto en el preview (refleja el ícono seleccionado)
        var prevIconGo  = MakeEmpty(panelGo.transform, "PreviewIcon");
        SetRT(prevIconGo, new Vector2(PREV_X, 38f), new Vector2(90f, 90f));
        var prevIconImg = prevIconGo.AddComponent<Image>();
        prevIconImg.sprite = iconSprites[1]; // índice 1 = planeta (default)
        prevIconImg.type   = Image.Type.Simple; prevIconImg.preserveAspect = true;

        var prevNameGo  = MakeEmpty(panelGo.transform, "PreviewNameLabel");
        SetRT(prevNameGo, new Vector2(PREV_X, -68f), new Vector2(180f, 30f));
        var prevNameTmp = prevNameGo.AddComponent<TextMeshProUGUI>();
        prevNameTmp.text = "Universo 1"; prevNameTmp.font = fnt; prevNameTmp.fontSize = 22f;
        prevNameTmp.fontStyle = FontStyles.Bold; prevNameTmp.color = Color.white;
        prevNameTmp.alignment = TextAlignmentOptions.Center;
        prevNameTmp.overflowMode = TextOverflowModes.Overflow;

        // ── Label "Nombre del universo" ───────────────────────────────────────
        MakeFieldLabel(panelGo.transform, fnt, "LabelNombre", "Nombre del universo",
            new Vector2(FORM_X, 185f), FORM_W);

        // ── Input + contador ──────────────────────────────────────────────────
        var inputField = MakeNameInput(panelGo.transform, fnt, uiSpr,
            "InputName", "Universo 1", new Vector2(FORM_X, 135f), FORM_W, 58f,
            out TextMeshProUGUI counterTmp);

        // ── Validación ────────────────────────────────────────────────────────
        var validGo  = MakeEmpty(panelGo.transform, "ValidationLabel");
        SetRT(validGo, new Vector2(FORM_X, 90f), new Vector2(FORM_W, 32f));
        var validTmp = validGo.AddComponent<TextMeshProUGUI>();
        validTmp.text = "⚠ El nombre no puede estar vacío ni usar\ncaracteres como <>:\"|?";
        validTmp.font = fnt; validTmp.fontSize = 17f; validTmp.color = Hex("E5B435");
        validTmp.alignment = TextAlignmentOptions.Left; validTmp.enableWordWrapping = true;
        validGo.SetActive(false);

        // ── Label "Ícono" ─────────────────────────────────────────────────────
        MakeFieldLabel(panelGo.transform, fnt, "LabelIcono", "Ícono",
            new Vector2(FORM_X, 47f), FORM_W);

        // ── 8 íconos ─────────────────────────────────────────────────────────
        const float ICON_SZ = 44f, ICON_GAP = 5f;
        float iconTotalW = ICON_COUNT * ICON_SZ + (ICON_COUNT - 1) * ICON_GAP;
        float iconStartX = FORM_X - iconTotalW / 2f + ICON_SZ / 2f;
        const float ICON_Y = 3f;

        var iconBtns    = new Button[ICON_COUNT];
        var iconBorders = new Image[ICON_COUNT];

        for (int i = 0; i < ICON_COUNT; i++)
        {
            float ix = iconStartX + i * (ICON_SZ + ICON_GAP);
            var bdrGo  = MakeEmpty(panelGo.transform, $"Icon_{i}");
            SetRT(bdrGo, new Vector2(ix, ICON_Y), new Vector2(ICON_SZ, ICON_SZ));
            var bdrImg = bdrGo.AddComponent<Image>();
            bdrImg.sprite = roundedSpr; bdrImg.type = Image.Type.Sliced;
            bdrImg.color  = (i == 1)
                ? new Color(0.30f, 0.85f, 0.91f, 1f)
                : new Color(0.12f, 0.14f, 0.28f, 0.85f);
            iconBorders[i] = bdrImg;
            iconBtns[i]    = bdrGo.AddComponent<Button>();
            iconBtns[i].targetGraphic = bdrImg;

            // Sprite del ícono
            var iGo  = MakeEmpty(bdrGo.transform, "IconImg");
            SetRT(iGo, Vector2.zero, new Vector2(ICON_SZ - 6f, ICON_SZ - 6f));
            var iImg = iGo.AddComponent<Image>();
            iImg.sprite = iconSprites[i]; iImg.type = Image.Type.Simple; iImg.preserveAspect = true;
        }

        // ── Label "Color del tema" ────────────────────────────────────────────
        MakeFieldLabel(panelGo.transform, fnt, "LabelColor", "Color del tema",
            new Vector2(FORM_X, -40f), FORM_W);

        // ── 6 círculos de color ───────────────────────────────────────────────
        const float COL_SZ = 46f, COL_GAP = 10f;
        float colTotalW = COL_COUNT * COL_SZ + (COL_COUNT - 1) * COL_GAP;
        float colStartX = FORM_X - colTotalW / 2f + COL_SZ / 2f;
        const float COL_Y = -83f;

        string[] colHex = { "4DD9E8","E575B5","9B59B6","F1C40F","2ECC71","E74C3C" };
        var colorBtns   = new Button[COL_COUNT];
        var colorChecks = new TextMeshProUGUI[COL_COUNT];

        for (int i = 0; i < COL_COUNT; i++)
        {
            float cx = colStartX + i * (COL_SZ + COL_GAP);
            var circGo  = MakeEmpty(panelGo.transform, $"Color_{i}");
            SetRT(circGo, new Vector2(cx, COL_Y), new Vector2(COL_SZ, COL_SZ));
            var circImg = circGo.AddComponent<Image>();
            circImg.sprite = circleSpr; circImg.type = Image.Type.Simple; circImg.color = Hex(colHex[i]);
            colorBtns[i] = circGo.AddComponent<Button>(); colorBtns[i].targetGraphic = circImg;

            var chkGo  = MakeEmpty(circGo.transform, "Check"); Stretch(chkGo);
            var chkTmp = chkGo.AddComponent<TextMeshProUGUI>();
            chkTmp.text = "✓"; chkTmp.font = fnt; chkTmp.fontSize = 24f;
            chkTmp.fontStyle = FontStyles.Bold; chkTmp.color = Color.white;
            chkTmp.alignment = TextAlignmentOptions.Center;
            chkTmp.overflowMode = TextOverflowModes.Overflow;
            colorChecks[i] = chkTmp;
            chkGo.SetActive(i == 0);
        }

        // ── Botones ───────────────────────────────────────────────────────────
        const float BTNC_W=200f, BTNCR_W=260f, BTN_H=58f, BTN_GAP=20f, BTN_Y=-180f;
        float pairW = BTNC_W + BTN_GAP + BTNCR_W;

        var cancelGo  = MakeEmpty(panelGo.transform, "BtnCancelar");
        SetRT(cancelGo, new Vector2(-pairW/2f+BTNC_W/2f, BTN_Y), new Vector2(BTNC_W, BTN_H));
        var cancelImg = cancelGo.AddComponent<Image>();
        cancelImg.sprite = roundedSpr; cancelImg.type = Image.Type.Sliced; cancelImg.color = Hex("3A3B6B");
        var btnCancelar = cancelGo.AddComponent<Button>(); btnCancelar.targetGraphic = cancelImg;
        MakeLabel(cancelGo.transform, fnt, "Cancelar", 32f, FontStyles.Bold, Color.white);

        var crearGo  = MakeEmpty(panelGo.transform, "BtnCrear");
        SetRT(crearGo, new Vector2(pairW/2f-BTNCR_W/2f, BTN_Y), new Vector2(BTNCR_W, BTN_H));
        var crearImg = crearGo.AddComponent<Image>();
        crearImg.sprite = roundedSpr; crearImg.type = Image.Type.Sliced; crearImg.color = Hex("00BCD4");
        var btnCrear = crearGo.AddComponent<Button>(); btnCrear.targetGraphic = crearImg;
        MakeLabel(crearGo.transform, fnt, "Crear Universo", 32f, FontStyles.Bold, Color.white);

        // ── Manager ───────────────────────────────────────────────────────────
        var mgrGo = MakeEmpty(cGo.transform, "CrearUniversoManager");
        var mgr   = mgrGo.AddComponent<CrearUniversoManager>();
        var so    = new SerializedObject(mgr);

        so.FindProperty("inputName").objectReferenceValue       = inputField;
        so.FindProperty("counterLabel").objectReferenceValue    = counterTmp;
        so.FindProperty("validationLabel").objectReferenceValue = validTmp;
        so.FindProperty("previewCircle").objectReferenceValue   = prevCircImg;
        so.FindProperty("previewIcon").objectReferenceValue     = prevIconImg;
        so.FindProperty("previewNameLabel").objectReferenceValue = prevNameTmp;
        so.FindProperty("btnCancelar").objectReferenceValue     = btnCancelar;
        so.FindProperty("btnCrear").objectReferenceValue        = btnCrear;

        WireArray(so, "iconButtons",  iconBtns,    ICON_COUNT);
        WireArray(so, "iconBorders",  iconBorders, ICON_COUNT);
        WireArray(so, "iconSprites",  iconSprites, ICON_COUNT);
        WireArray(so, "colorButtons", colorBtns,   COL_COUNT);
        WireArray(so, "colorChecks",  colorChecks, COL_COUNT);

        so.ApplyModifiedProperties();

        // ── Guardar ───────────────────────────────────────────────────────────
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/CrearUniversoScene.unity");
        AssetDatabase.Refresh();

        Debug.Log("[CrearUniversoBuilder] ✓ CrearUniversoScene creada.");
        EditorUtility.DisplayDialog("¡Listo!", "CrearUniversoScene creada.\n\nFile → Build Settings → Add Open Scenes", "OK");
    }

    // ── Generadores de íconos ─────────────────────────────────────────────────

    static Sprite[] GenerateIconSprites()
    {
        System.IO.Directory.CreateDirectory("Assets/Sprites/Icons");
        return new Sprite[]
        {
            GenGalaxyIcon(),
            GenPlanetIcon(),
            GenCometIcon(),
            GenStarIcon(),
            GenBurstIcon(),
            GenSatelliteIcon(),
            GenGlobeIcon(),
            GenTelescopeIcon(),
        };
    }

    // 1 — Galaxia: campo estelar con nébula central
    static Sprite GenGalaxyIcon()
    {
        int s = 64;
        var px = ClearPx(s);
        FillCircle(px, s, 31.5f, 31.5f, 28f, new Color(0.04f, 0.06f, 0.22f, 1f));
        FillCircle(px, s, 31.5f, 31.5f, 10f, new Color(0.50f, 0.40f, 0.90f, 0.6f));
        FillCircle(px, s, 31.5f, 31.5f,  5f, new Color(0.90f, 0.85f, 1.00f, 1f));
        int[] sx={18,42,26,48,12,50,34,14,38};
        int[] sy={46,36,18,22,28,50,54,16,10};
        float[] sb={1f,.8f,1f,.7f,.6f,.9f,.7f,.8f,.9f};
        for(int i=0;i<sx.Length;i++)
        {
            float d=Mathf.Sqrt((sx[i]-31.5f)*(sx[i]-31.5f)+(sy[i]-31.5f)*(sy[i]-31.5f));
            if(d<28f) px[sy[i]*s+sx[i]]=new Color(sb[i],sb[i],sb[i],1f);
        }
        return SaveIcon(px, s, "icon_galaxy");
    }

    // 2 — Planeta tipo Saturno con anillo
    static Sprite GenPlanetIcon()
    {
        int s = 64;
        var px = ClearPx(s);
        float cx=31.5f, cy=31.5f, pr=16f;
        float rA=26f, rB=7f, rT=3.5f;
        Color pDark  = new Color(0.10f,0.38f,0.75f,1f);
        Color pLight = new Color(0.38f,0.72f,0.98f,1f);
        Color ring   = new Color(0.98f,0.80f,0.25f,1f);
        // Anillo detrás del planeta
        DrawEllipseRing(px, s, cx, cy, rA, rB, rT, ring);
        // Planeta (encima del anillo)
        for(int y=0;y<s;y++) for(int x=0;x<s;x++){
            float dx=x-cx, dy=y-cy, d=Mathf.Sqrt(dx*dx+dy*dy);
            if(d<=pr){ float lum=Mathf.Clamp01(0.45f-dy/pr*0.35f); px[y*s+x]=Color.Lerp(pDark,pLight,lum); }
        }
        // Anillo delante del planeta (mitad superior)
        for(int y=0;y<s;y++) for(int x=0;x<s;x++){
            float dx=x-cx, dy=y-cy;
            if(dy>=0) continue;
            float outerV=dx*dx/(rA*rA)+dy*dy/(rB*rB);
            float iA=rA-rT, iB=rB-rT;
            float innerV=dx*dx/(iA*iA)+dy*dy/(iB*iB);
            if(outerV<=1f && innerV>=1f) px[y*s+x]=ring;
        }
        return SaveIcon(px, s, "icon_planet");
    }

    // 3 — Cometa: círculo naranja con cola
    static Sprite GenCometIcon()
    {
        int s = 64;
        var px = ClearPx(s);
        float hx=44f, hy=18f;
        // Cola (degradado hacia abajo-izquierda)
        for(int y=0;y<s;y++) for(int x=0;x<s;x++){
            float dx=x-hx, dy=y-hy;
            float proj=(dx*(-1)+dy*(-1))/Mathf.Sqrt(2f); // proyección en dir (-1,-1)/√2 → cola
            float perp=(dx*(-1)+dy*1)/Mathf.Sqrt(2f);
            if(proj>0 && proj<30 && Mathf.Abs(perp)<4.5f*(1f-proj/30f)){
                float a=(1f-proj/30f);
                px[y*s+x]=new Color(1f,0.55f,0.12f,a*a);
            }
        }
        FillCircle(px, s, hx, hy, 10f, new Color(1f,0.65f,0.20f,1f));
        FillCircle(px, s, hx, hy,  5f, new Color(1f,0.92f,0.70f,1f));
        return SaveIcon(px, s, "icon_comet");
    }

    // 4 — Estrella de 5 puntas amarilla
    static Sprite GenStarIcon()
    {
        int s = 64;
        var px = ClearPx(s);
        FillStar(px, s, 31.5f, 31.5f, 29f, 11f, 5, -90f, new Color(1f,0.84f,0.08f,1f));
        FillCircle(px, s, 31.5f, 31.5f, 7f, new Color(1f,1f,0.72f,1f));
        return SaveIcon(px, s, "icon_star");
    }

    // 5 — Destello de 4+4 puntas dorado
    static Sprite GenBurstIcon()
    {
        int s = 64;
        var px = ClearPx(s);
        FillStar(px, s, 31.5f, 31.5f, 30f, 5f, 4,  0f, new Color(1f,0.82f,0.18f,1f));
        FillStar(px, s, 31.5f, 31.5f, 18f, 9f, 4, 45f, new Color(1f,0.92f,0.42f,1f));
        FillCircle(px, s, 31.5f, 31.5f, 6f, new Color(1f,1f,0.80f,1f));
        return SaveIcon(px, s, "icon_burst");
    }

    // 6 — Satélite: cuerpo central + paneles solares
    static Sprite GenSatelliteIcon()
    {
        int s = 64;
        var px = ClearPx(s);
        Color body  = new Color(0.28f,0.52f,0.88f,1f);
        Color panel = new Color(0.12f,0.30f,0.70f,1f);
        Color div   = new Color(0.65f,0.75f,0.90f,1f);
        FillRect(px,s,  2,28,20,8,panel); FillRect(px,s, 42,28,20,8,panel);
        FillRect(px,s,  7,28, 2,8,div);   FillRect(px,s, 13,28, 2,8,div);
        FillRect(px,s, 47,28, 2,8,div);   FillRect(px,s, 53,28, 2,8,div);
        FillCircle(px,s,31.5f,31.5f,10f,body);
        FillCircle(px,s,31.5f,17f,  4f, div);
        FillRect(px,s,30,17,3,10,div);
        return SaveIcon(px, s, "icon_satellite");
    }

    // 7 — Globo terrestre: esfera azul con meridianos
    static Sprite GenGlobeIcon()
    {
        int s = 64;
        var px = ClearPx(s);
        float cx=31.5f, cy=31.5f, r=26f;
        Color sea  = new Color(0.13f,0.42f,0.82f,1f);
        Color land = new Color(0.20f,0.65f,0.30f,1f);
        Color line = new Color(0.65f,0.88f,1.00f,0.7f);
        FillCircle(px,s,cx,cy,r,sea);
        FillCircle(px,s,22f,26f,9f,land);
        FillCircle(px,s,40f,38f,6f,land);
        FillCircle(px,s,38f,20f,4f,land);
        DrawEllipseRing(px,s,cx,cy,   26f,4.5f,1f,line);
        DrawEllipseRing(px,s,cx,cy+10f,20f,3.5f,1f,line);
        DrawEllipseRing(px,s,cx,cy-10f,20f,3.5f,1f,line);
        for(int y=0;y<s;y++){ float dy=y-cy; if(Mathf.Abs(dy)<r){ int ix=(int)cx; if(Mathf.Sqrt((ix-cx)*(ix-cx)+dy*dy)<r) px[y*s+ix]=line; } }
        // Recortar al círculo
        for(int y=0;y<s;y++) for(int x=0;x<s;x++){ float d=Mathf.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy)); if(d>r) px[y*s+x]=Color.clear; }
        return SaveIcon(px, s, "icon_globe");
    }

    // 8 — Telescopio: tubo diagonal + trípode
    static Sprite GenTelescopeIcon()
    {
        int s = 64;
        var px = ClearPx(s);
        Color tube  = new Color(0.38f,0.40f,0.52f,1f);
        Color metal = new Color(0.58f,0.60f,0.72f,1f);
        Color lens  = new Color(0.30f,0.72f,1.00f,1f);
        // Tubo diagonal (de arriba-derecha a abajo-izquierda)
        for(int y=0;y<s;y++) for(int x=0;x<s;x++){
            float t=(x+y-58f)/Mathf.Sqrt(2f);
            float n=(x-y)/Mathf.Sqrt(2f);
            if(t>-22f && t<22f && Mathf.Abs(n)<6f)
                px[y*s+x]=(Mathf.Abs(n)<5f ? tube : metal);
        }
        FillCircle(px,s,15f,47f,6f,metal); FillCircle(px,s,15f,47f,3.5f,lens);
        FillCircle(px,s,49f,17f,5f,metal);
        FillRect(px,s,22,44,2,14,tube); FillRect(px,s,34,44,2,14,tube);
        FillRect(px,s,24,56,12,3,tube);
        return SaveIcon(px, s, "icon_telescope");
    }

    // ── Helpers de dibujo ─────────────────────────────────────────────────────

    static Color[] ClearPx(int s) { var p=new Color[s*s]; for(int i=0;i<p.Length;i++) p[i]=Color.clear; return p; }

    static void FillCircle(Color[] px, int s, float cx, float cy, float r, Color col)
    {
        for(int y=0;y<s;y++) for(int x=0;x<s;x++)
            if((x-cx)*(x-cx)+(y-cy)*(y-cy)<=r*r) px[y*s+x]=col;
    }

    static void FillStar(Color[] px, int s, float cx, float cy, float outerR, float innerR, int pts, float rotDeg, Color col)
    {
        float rot=rotDeg*Mathf.Deg2Rad, sec=2f*Mathf.PI/pts;
        for(int y=0;y<s;y++) for(int x=0;x<s;x++){
            float dx=x-cx, dy=y-cy;
            float dist=Mathf.Sqrt(dx*dx+dy*dy);
            if(dist>outerR+1) continue;
            float ang=((Mathf.Atan2(dy,dx)-rot)%sec+sec)%sec;
            float t=Mathf.Abs(ang-sec/2f)/(sec/2f);
            if(dist<=Mathf.Lerp(innerR,outerR,t)) px[y*s+x]=col;
        }
    }

    static void DrawEllipseRing(Color[] px, int s, float cx, float cy, float A, float B, float thick, Color col)
    {
        float iA=Mathf.Max(0.1f,A-thick), iB=Mathf.Max(0.1f,B-thick);
        for(int y=0;y<s;y++) for(int x=0;x<s;x++){
            float dx=x-cx, dy=y-cy;
            if(dx*dx/(A*A)+dy*dy/(B*B)<=1f && dx*dx/(iA*iA)+dy*dy/(iB*iB)>=1f)
                px[y*s+x]=col;
        }
    }

    static void FillRect(Color[] px, int s, int x0, int y0, int w, int h, Color col)
    {
        for(int y=y0;y<y0+h;y++) for(int x=x0;x<x0+w;x++)
            if(x>=0&&x<s&&y>=0&&y<s) px[y*s+x]=col;
    }

    static Sprite SaveIcon(Color[] px, int s, string name)
    {
        string path = $"Assets/Sprites/Icons/{name}.png";
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.spritePixelsPerUnit = 100; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ── Helpers de UI ─────────────────────────────────────────────────────────

    static void MakeBorderPanel(Transform parent, string borderName, string panelName,
        Vector2 pos, Vector2 size, Sprite rounded, Color panelColor,
        out GameObject panelGo)
    {
        var bGo  = MakeEmpty(parent, borderName);
        SetRT(bGo, pos, new Vector2(size.x + 14f, size.y + 14f));
        var bImg = bGo.AddComponent<Image>();
        bImg.sprite = rounded; bImg.type = Image.Type.Sliced; bImg.color = new Color(1f,1f,1f,0.22f);

        panelGo  = MakeEmpty(parent, panelName);
        SetRT(panelGo, pos, size);
        var pImg = panelGo.AddComponent<Image>();
        pImg.sprite = rounded; pImg.type = Image.Type.Sliced; pImg.color = panelColor;
    }

    static void MakeFieldLabel(Transform parent, TMP_FontAsset fnt, string goName, string text, Vector2 pos, float w)
    {
        var go  = MakeEmpty(parent, goName);
        SetRT(go, pos, new Vector2(w, 22f));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = fnt; tmp.fontSize = 19f; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left; tmp.overflowMode = TextOverflowModes.Overflow;
    }

    static TMP_InputField MakeNameInput(Transform parent, TMP_FontAsset fnt, Sprite uiSpr,
        string name, string defaultText, Vector2 pos, float w, float h,
        out TextMeshProUGUI counter)
    {
        var go = MakeEmpty(parent, name);
        SetRT(go, pos, new Vector2(w, h));
        var bg = go.AddComponent<Image>();
        bg.color = Hex("0D1238"); bg.sprite = uiSpr; bg.type = Image.Type.Sliced;

        var area   = new GameObject("Text Area", typeof(RectTransform));
        area.transform.SetParent(go.transform, false);
        var areaRT = area.GetComponent<RectTransform>();
        areaRT.anchorMin = Vector2.zero; areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = new Vector2(14f, 6f); areaRT.offsetMax = new Vector2(-82f, -6f);
        area.AddComponent<RectMask2D>();

        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.transform.SetParent(area.transform, false);
        Stretch(phGo);
        var ph = phGo.AddComponent<TextMeshProUGUI>();
        ph.text = defaultText; ph.font = fnt; ph.fontSize = 24f;
        ph.color = new Color(1f,1f,1f,0.3f);
        ph.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        ph.enableWordWrapping = false;

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(area.transform, false);
        Stretch(txtGo);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text = ""; txt.font = fnt; txt.fontSize = 24f; txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        txt.enableWordWrapping = false;

        var ctrGo  = MakeEmpty(go.transform, "Counter");
        SetRT(ctrGo, new Vector2(w / 2f - 40f, 0f), new Vector2(76f, h));
        var ctrTmp = ctrGo.AddComponent<TextMeshProUGUI>();
        ctrTmp.text = $"0 / 25"; ctrTmp.font = fnt; ctrTmp.fontSize = 15f;
        ctrTmp.color = new Color(1f,1f,1f,0.5f);
        ctrTmp.alignment = TextAlignmentOptions.Right | TextAlignmentOptions.Midline;
        ctrTmp.overflowMode = TextOverflowModes.Overflow;
        counter = ctrTmp;

        var field = go.AddComponent<TMP_InputField>();
        var so = new SerializedObject(field);
        so.FindProperty("m_TextViewport").objectReferenceValue  = areaRT;
        so.FindProperty("m_TextComponent").objectReferenceValue = txt;
        so.FindProperty("m_Placeholder").objectReferenceValue   = ph;
        so.FindProperty("m_TargetGraphic").objectReferenceValue = bg;
        so.FindProperty("m_ContentType").enumValueIndex         = (int)TMP_InputField.ContentType.Standard;
        so.FindProperty("m_LineType").enumValueIndex            = 0;
        so.ApplyModifiedProperties();
        field.interactable = true; field.customCaretColor = true;
        field.caretColor = Color.white; field.caretWidth = 2; field.caretBlinkRate = 0.85f;
        field.characterLimit = 25; field.text = defaultText;
        return field;
    }

    static void WireArray<T>(SerializedObject so, string propName, T[] arr, int count) where T : Object
    {
        var prop = so.FindProperty(propName);
        prop.arraySize = count;
        for (int i = 0; i < count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
    }

    static void MakeLabel(Transform parent, TMP_FontAsset fnt, string text, float size, FontStyles style, Color color)
    {
        var go = MakeEmpty(parent, "Label"); Stretch(go);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = fnt; tmp.fontSize = size; tmp.fontStyle = style;
        tmp.color = color; tmp.alignment = TextAlignmentOptions.Center;
        tmp.overflowMode = TextOverflowModes.Overflow; tmp.enableWordWrapping = false;
    }

    static void MakeSparkle(Transform parent, string name, Vector2 pos, float size)
    {
        var go  = MakeEmpty(parent, name);
        SetRT(go, pos, new Vector2(28f, 28f));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "✦";
        tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        tmp.fontSize = size; tmp.color = new Color(0.7f,0.9f,1f,0.8f);
        tmp.alignment = TextAlignmentOptions.Center; tmp.overflowMode = TextOverflowModes.Overflow;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f,0.5f); rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetRT(GameObject go, Vector2 pos, Vector2 size)
    { var rt=go.GetComponent<RectTransform>(); rt.anchoredPosition=pos; rt.sizeDelta=size; }

    static GameObject MakeEmpty(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f,0.5f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#"+h, out Color c); return c; }
}
