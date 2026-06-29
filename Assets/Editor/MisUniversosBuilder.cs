using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builder ADITIVO de MisUniversosScene. NO usa NewScene: abre la escena
/// existente y solo AGREGA lo que falta (estado de lista, badge, manager),
/// agrupando el estado vacío ya presente. Re-ejecutarlo NO resetea posiciones.
/// </summary>
public static class MisUniversosBuilder
{
    const string ScenePath = "Assets/Scenes/MisUniversosScene.unity";

    // Aplica el estilo nuevo al BtnEditar de la plantilla en la ESCENA ACTUAL,
    // sin tocar nada más (aditivo / idempotente).
    [MenuItem("ChemiTech/Fix/MisUniversos Editar Button")]
    static void FixEditarButton()
    {
        var mgr = Object.FindObjectOfType<MisUniversosManager>();
        GameObject template = null;
        if (mgr != null)
        {
            var mso = new SerializedObject(mgr);
            template = mso.FindProperty("cardTemplate").objectReferenceValue as GameObject;
        }
        if (template == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró el CardTemplate.\n¿Abriste MisUniversosScene?", "OK");
            return;
        }

        var editGo = FindChildRecursive(template.transform, "BtnEditar");
        if (editGo == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró 'BtnEditar' en el CardTemplate.", "OK");
            return;
        }

        var rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        var iconSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/edit.png");

        // Botón: cuadrado redondeado, color #5A5FA5
        var img = editGo.GetComponent<Image>();
        if (img) { if (rounded) img.sprite = rounded; img.type = Image.Type.Sliced; img.color = Hex("5A5FA5"); }
        var le = editGo.GetComponent<LayoutElement>() ?? editGo.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = 56f; le.minHeight = le.preferredHeight = 56f;

        // Quitar el "✎" (Label de texto)
        var label = editGo.transform.Find("Label");
        if (label != null) Object.DestroyImmediate(label.gameObject);

        // Ícono placeholder (cambiar luego: BtnEditar → Icon → Sprite)
        var iconT = editGo.transform.Find("Icon");
        var iconGo = iconT != null ? iconT.gameObject
                                   : new GameObject("Icon", typeof(RectTransform), typeof(Image));
        if (iconT == null) iconGo.transform.SetParent(editGo.transform, false);
        var irt = iconGo.GetComponent<RectTransform>();
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = Vector2.zero;
        irt.sizeDelta = new Vector2(30f, 30f);
        var iimg = iconGo.GetComponent<Image>();
        iimg.sprite = iconSpr; iimg.color = Color.white; iimg.preserveAspect = true; iimg.raycastTarget = false;

        EditorUtility.SetDirty(template);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[MisUniversosBuilder] ✓ BtnEditar: #5A5FA5, cuadrado redondeado, ícono placeholder.");
        EditorUtility.DisplayDialog("¡Listo!",
            "BtnEditar actualizado (color #5A5FA5, cuadrado redondeado, ícono placeholder).\n\n" +
            "Para cambiar el ícono luego: CardTemplate → BtnEditar → Icon → cambia el Sprite.\n" +
            "Guarda con Ctrl+S.", "OK");
    }

    [MenuItem("ChemiTech/Build Mis Universos Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir Mis Universos Scene (aditivo)",
            "Esto ABRE la escena existente y solo AGREGA la funcionalidad de lista.\n" +
            "NO borra ni reposiciona lo que ya tienes.\n\n¿Continuar?",
            "Sí, continuar", "Cancelar"))
            return;

        // ── Abrir la escena existente (sin destruir nada) ─────────────────────
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // ── Assets ────────────────────────────────────────────────────────────
        var fnt        = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        var roundedSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        var uiSpr      = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var circleSpr  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
        var iconSprites = LoadIconSprites();

        // ── Localizar nodos existentes ────────────────────────────────────────
        var canvas = FindInScene(scene, "Canvas");
        if (canvas == null) { EditorUtility.DisplayDialog("Error", "No se encontró 'Canvas' en la escena.", "OK"); return; }
        var contentPanel = FindChildRecursive(canvas.transform, "ContentPanel");
        if (contentPanel == null) { EditorUtility.DisplayDialog("Error", "No se encontró 'ContentPanel'.", "OK"); return; }
        var headerPanel     = FindChildRecursive(canvas.transform, "HeaderPanel");
        var btnAtras        = FindChildRecursive(canvas.transform, "BtnAtras")?.GetComponent<Button>();
        var btnCrearEmptyGo = FindChildRecursive(contentPanel.transform, "BtnCrear");

        // ── 1) Agrupar el estado vacío existente (solo una vez) ───────────────
        var emptyState = FindChildRecursive(contentPanel.transform, "EmptyState");
        if (emptyState == null)
        {
            emptyState = MakeFill(contentPanel.transform, "EmptyState");
            emptyState.transform.SetAsFirstSibling();
            var toMove = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in contentPanel.transform)
                if (child.gameObject != emptyState) toMove.Add(child);
            foreach (var t in toMove)
                t.SetParent(emptyState.transform, true); // worldPositionStays = true (preserva posición)
        }

        // ── 2) Crear el estado de lista (solo una vez) ────────────────────────
        var listState = FindChildRecursive(contentPanel.transform, "ListState");
        if (listState == null)
        {
            listState = BuildListState(contentPanel.transform, fnt, roundedSpr,
                out _listContent, out _cardTemplate, out _btnCrearList);
            listState.SetActive(false);
        }
        else
        {
            _listContent  = FindChildRecursive(listState.transform, "Content")?.GetComponent<RectTransform>();
            _cardTemplate = FindChildRecursive(listState.transform, "CardTemplate");
            _btnCrearList = FindChildRecursive(listState.transform, "BtnCrearList")?.GetComponent<Button>();
        }

        // ── 3) Badge de conteo en el avatar (solo una vez) ────────────────────
        GameObject badgeGroup = null; TextMeshProUGUI badgeTxt = null;
        if (headerPanel != null)
        {
            var avatar = FindChildRecursive(headerPanel.transform, "AvatarIcon");
            if (avatar != null)
            {
                badgeGroup = FindChildRecursive(avatar.transform, "CountBadge");
                if (badgeGroup == null)
                    badgeGroup = BuildCountBadge(avatar.transform, circleSpr, fnt, out badgeTxt);
                else
                    badgeTxt = FindChildRecursive(badgeGroup.transform, "Num")?.GetComponent<TextMeshProUGUI>();
            }
        }

        // ── 4) Manager: reusar o crear, y reconectar referencias ──────────────
        var mgrGo = FindChildRecursive(canvas.transform, "MisUniversosManager");
        if (mgrGo == null)
        {
            mgrGo = new GameObject("MisUniversosManager", typeof(RectTransform));
            mgrGo.transform.SetParent(canvas.transform, false);
        }
        var mgr = mgrGo.GetComponent<MisUniversosManager>();
        if (mgr == null) mgr = mgrGo.AddComponent<MisUniversosManager>();

        var so = new SerializedObject(mgr);
        SetRef(so, "btnAtras",        btnAtras);
        SetRef(so, "countBadge",      badgeTxt);
        SetRef(so, "countBadgeGroup", badgeGroup);
        SetRef(so, "emptyStateGroup", emptyState);
        SetRef(so, "listGroup",       listState);
        SetRef(so, "listContent",     _listContent);
        SetRef(so, "cardTemplate",    _cardTemplate);
        SetRef(so, "btnCrearEmpty",   btnCrearEmptyGo?.GetComponent<Button>());
        SetRef(so, "btnCrearList",    _btnCrearList);
        WireSprites(so, "iconSprites", iconSprites);
        so.ApplyModifiedProperties();

        // Modales de error (sin espacio / universo dañado)
        BuildErrorModals();

        // ── Guardar ───────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[MisUniversosBuilder] ✓ Lista agregada (aditivo, sin resetear posiciones).");
        EditorUtility.DisplayDialog("¡Listo!",
            "Se agregó la lista de universos sin tocar tus posiciones.\n\n" +
            "El estado vacío y la lista alternan según haya universos guardados.",
            "OK");
    }

    // refs temporales compartidas entre ramas
    static RectTransform _listContent;
    static GameObject    _cardTemplate;
    static Button        _btnCrearList;

    // ── Construcción del estado de lista ──────────────────────────────────────
    static GameObject BuildListState(Transform parent, TMP_FontAsset fnt, Sprite rounded,
        out RectTransform listContent, out GameObject cardTemplate, out Button btnCrearList)
    {
        var listState = MakeFill(parent, "ListState");

        // Botón "+ Crear nuevo Universo" arriba (top-stretch)
        var btnGo = MakeEmpty(listState.transform, "BtnCrearList");
        var btnRT = btnGo.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0f, 1f); btnRT.anchorMax = new Vector2(1f, 1f);
        btnRT.pivot = new Vector2(0.5f, 1f);
        btnRT.offsetMin = new Vector2(22f, -86f); btnRT.offsetMax = new Vector2(-22f, -22f);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = rounded; btnImg.type = Image.Type.Sliced; btnImg.color = Hex("19A7CE");
        btnCrearList = btnGo.AddComponent<Button>(); btnCrearList.targetGraphic = btnImg;
        var btnLbl = MakeEmpty(btnGo.transform, "Label"); Stretch(btnLbl);
        var btnTmp = btnLbl.AddComponent<TextMeshProUGUI>();
        btnTmp.text = "+ Crear nuevo Universo"; btnTmp.font = fnt; btnTmp.fontSize = 28f;
        btnTmp.fontStyle = FontStyles.Bold; btnTmp.color = Color.white;
        btnTmp.alignment = TextAlignmentOptions.Center; btnTmp.overflowMode = TextOverflowModes.Overflow;

        // ScrollView (debajo del botón, llena el resto)
        var scrollGo = MakeEmpty(listState.transform, "ScrollView");
        var scrollRT = scrollGo.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0f); scrollRT.anchorMax = new Vector2(1f, 1f);
        scrollRT.offsetMin = new Vector2(22f, 20f); scrollRT.offsetMax = new Vector2(-22f, -98f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 26f;

        // Viewport
        var vpGo = MakeEmpty(scrollGo.transform, "Viewport");
        var vpRT = vpGo.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(0f, 0f); vpRT.offsetMax = new Vector2(-16f, 0f);
        vpRT.pivot = new Vector2(0f, 1f);
        var vpImg = vpGo.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.02f);
        vpGo.AddComponent<RectMask2D>();

        // Content
        var contentGo = MakeEmpty(vpGo.transform, "Content");
        var contentRT = contentGo.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f); contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f); contentRT.anchoredPosition = Vector2.zero;
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f; vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vpRT; scroll.content = contentRT;

        // Scrollbar vertical
        var sbGo = MakeEmpty(scrollGo.transform, "ScrollbarV");
        var sbRT = sbGo.GetComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(1f, 0f); sbRT.anchorMax = new Vector2(1f, 1f);
        sbRT.pivot = new Vector2(1f, 1f);
        sbRT.offsetMin = new Vector2(-12f, 0f); sbRT.offsetMax = new Vector2(0f, 0f);
        var sbImg = sbGo.AddComponent<Image>(); sbImg.sprite = rounded; sbImg.type = Image.Type.Sliced;
        sbImg.color = new Color(1f, 1f, 1f, 0.08f);
        var scrollbar = sbGo.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
        var handleGo = MakeEmpty(sbGo.transform, "Handle"); Stretch(handleGo);
        var handleImg = handleGo.AddComponent<Image>(); handleImg.sprite = rounded; handleImg.type = Image.Type.Sliced;
        handleImg.color = new Color(1f, 1f, 1f, 0.30f);
        scrollbar.targetGraphic = handleImg; scrollbar.handleRect = handleGo.GetComponent<RectTransform>();
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        // Plantilla de tarjeta (inactiva)
        cardTemplate = BuildCardTemplate(contentGo.transform, fnt, rounded);
        cardTemplate.SetActive(false);

        listContent = contentRT;
        return listState;
    }

    static GameObject BuildCardTemplate(Transform parent, TMP_FontAsset fnt, Sprite rounded)
    {
        var card = MakeEmpty(parent, "CardTemplate");
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = rounded; cardImg.type = Image.Type.Sliced; cardImg.color = Hex("3A3D70");
        var le = card.AddComponent<LayoutElement>(); le.minHeight = 96f; le.preferredHeight = 96f;
        var hlg = card.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 12, 12); hlg.spacing = 16f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        // IconFrame + Icon
        var frameGo = MakeEmpty(card.transform, "IconFrame");
        var frameImg = frameGo.AddComponent<Image>();
        frameImg.sprite = rounded; frameImg.type = Image.Type.Sliced; frameImg.color = Hex("4DD9E8");
        var frameLe = frameGo.AddComponent<LayoutElement>();
        frameLe.minWidth = 72f; frameLe.preferredWidth = 72f; frameLe.minHeight = 72f; frameLe.preferredHeight = 72f;
        var iconGo = MakeEmpty(frameGo.transform, "Icon");
        var iconRT = iconGo.GetComponent<RectTransform>();
        iconRT.anchorMin = Vector2.zero; iconRT.anchorMax = Vector2.one;
        iconRT.offsetMin = new Vector2(11f, 11f); iconRT.offsetMax = new Vector2(-11f, -11f);
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.color = Color.white; iconImg.preserveAspect = true; iconImg.raycastTarget = false;

        // TextColumn
        var colGo = MakeEmpty(card.transform, "TextColumn");
        var colLe = colGo.AddComponent<LayoutElement>(); colLe.flexibleWidth = 1f;
        var cvlg = colGo.AddComponent<VerticalLayoutGroup>();
        cvlg.spacing = 2f; cvlg.childAlignment = TextAnchor.MiddleLeft;
        cvlg.childControlWidth = true; cvlg.childControlHeight = true;
        cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
        var nameGo = MakeEmpty(colGo.transform, "NameLabel");
        var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
        nameTmp.text = "Nombre"; nameTmp.font = fnt; nameTmp.fontSize = 27f;
        nameTmp.fontStyle = FontStyles.Bold; nameTmp.color = Color.white;
        nameTmp.alignment = TextAlignmentOptions.Left; nameTmp.enableWordWrapping = false;
        nameTmp.overflowMode = TextOverflowModes.Ellipsis;
        var timeGo = MakeEmpty(colGo.transform, "TimeLabel");
        var timeTmp = timeGo.AddComponent<TextMeshProUGUI>();
        timeTmp.text = "Hace un momento"; timeTmp.font = fnt; timeTmp.fontSize = 16f;
        timeTmp.color = new Color(1f, 1f, 1f, 0.55f);
        timeTmp.alignment = TextAlignmentOptions.Left; timeTmp.enableWordWrapping = false;

        // BtnJugar
        var jugarGo = MakeEmpty(card.transform, "BtnJugar");
        var jugarImg = jugarGo.AddComponent<Image>();
        jugarImg.sprite = rounded; jugarImg.type = Image.Type.Sliced; jugarImg.color = Hex("19A7CE");
        var jugarLe = jugarGo.AddComponent<LayoutElement>();
        jugarLe.minWidth = 110f; jugarLe.preferredWidth = 110f; jugarLe.minHeight = 56f; jugarLe.preferredHeight = 56f;
        jugarGo.AddComponent<Button>().targetGraphic = jugarImg;
        var jLbl = MakeEmpty(jugarGo.transform, "Label"); Stretch(jLbl);
        var jTmp = jLbl.AddComponent<TextMeshProUGUI>();
        jTmp.text = "Jugar"; jTmp.font = fnt; jTmp.fontSize = 24f; jTmp.fontStyle = FontStyles.Bold;
        jTmp.color = Color.white; jTmp.alignment = TextAlignmentOptions.Center; jTmp.overflowMode = TextOverflowModes.Overflow;

        // BtnEditar (cuadrado redondeado + ícono placeholder)
        var editGo = MakeEmpty(card.transform, "BtnEditar");
        var editImg = editGo.AddComponent<Image>();
        editImg.sprite = rounded; editImg.type = Image.Type.Sliced; editImg.color = Hex("5A5FA5");
        var editLe = editGo.AddComponent<LayoutElement>();
        editLe.minWidth = 56f; editLe.preferredWidth = 56f; editLe.minHeight = 56f; editLe.preferredHeight = 56f;
        editGo.AddComponent<Button>().targetGraphic = editImg;
        var eIcon = MakeEmpty(editGo.transform, "Icon");
        eIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(30f, 30f);
        var eIconImg = eIcon.AddComponent<Image>();
        eIconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/edit.png");
        eIconImg.color = Color.white; eIconImg.preserveAspect = true; eIconImg.raycastTarget = false;

        return card;
    }

    static GameObject BuildCountBadge(Transform avatar, Sprite circleSpr, TMP_FontAsset fnt, out TextMeshProUGUI num)
    {
        var badge = MakeEmpty(avatar, "CountBadge");
        var rt = badge.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-2f, 2f);
        rt.sizeDelta = new Vector2(26f, 26f);
        var img = badge.AddComponent<Image>();
        img.sprite = circleSpr; img.type = Image.Type.Simple; img.color = Hex("F5A623");
        var numGo = MakeEmpty(badge.transform, "Num"); Stretch(numGo);
        num = numGo.AddComponent<TextMeshProUGUI>();
        num.text = "0"; num.font = fnt; num.fontSize = 16f; num.fontStyle = FontStyles.Bold;
        num.color = Hex("1E2050"); num.alignment = TextAlignmentOptions.Center;
        num.overflowMode = TextOverflowModes.Overflow;
        return badge;
    }

    // ── Helpers de assets ─────────────────────────────────────────────────────
    static Sprite[] LoadIconSprites()
    {
        var arr = new Sprite[UniverseTheme.IconNames.Length];
        for (int i = 0; i < arr.Length; i++)
            arr[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Icons/{UniverseTheme.IconNames[i]}.png");
        return arr;
    }

    // ── Helpers de jerarquía ──────────────────────────────────────────────────
    static GameObject FindInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            var found = FindChildRecursive(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    static GameObject FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child.gameObject;
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static void SetRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[MisUniversosBuilder] propiedad '{prop}' no existe."); return; }
        p.objectReferenceValue = value;
    }

    static void WireSprites(SerializedObject so, string prop, Sprite[] arr)
    {
        var p = so.FindProperty(prop);
        if (p == null) return;
        p.arraySize = arr.Length;
        for (int i = 0; i < arr.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
    }

    // ── Helpers de RectTransform ──────────────────────────────────────────────
    static GameObject MakeFill(Transform parent, string name)
    {
        var go = MakeEmpty(parent, name);
        Stretch(go);
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }

    // ══ Modales de error ═══════════════════════════════════════════════════════
    static Sprite s_rounded, s_ui, s_circle;
    static TMP_FontAsset s_fnt;

    static readonly Color MODAL_BG  = Hex("242659");
    static readonly Color TEXT_GRAY = new Color(1f, 1f, 1f, 0.78f);

    // Agrega/actualiza los modales en la escena ACTUAL (aditivo, idempotente).
    [MenuItem("ChemiTech/Fix/MisUniversos Error Modals")]
    static void FixErrorModals()
    {
        if (!BuildErrorModals()) return;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("¡Listo!",
            "Modales de error agregados/actualizados (sin espacio / universo dañado).\nGuarda con Ctrl+S.", "OK");
    }

    static bool BuildErrorModals()
    {
        s_fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        s_rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        s_ui      = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        s_circle  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");

        var canvas = GameObject.Find("Canvas");
        var mgr    = Object.FindObjectOfType<MisUniversosManager>();
        if (canvas == null || mgr == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró 'Canvas' o 'MisUniversosManager' (¿abriste MisUniversosScene?).", "OK");
            return false;
        }

        // Regenerar (idempotente): destruir los previos y rehacer
        var oldS = canvas.transform.Find("StorageModal"); if (oldS) Object.DestroyImmediate(oldS.gameObject);
        var oldC = canvas.transform.Find("CorruptModal"); if (oldC) Object.DestroyImmediate(oldC.gameObject);

        var sModal = BuildStorageModal(canvas.transform, out var sBtn, out var sFill, out var sVal);
        var cModal = BuildCorruptModal(canvas.transform, out var cBtn);
        sModal.SetActive(false);
        cModal.SetActive(false);

        var so = new SerializedObject(mgr);
        so.FindProperty("storageModal").objectReferenceValue      = sModal;
        so.FindProperty("btnStorageVolver").objectReferenceValue  = sBtn;
        so.FindProperty("storageBarFill").objectReferenceValue    = sFill;
        so.FindProperty("storageValueLabel").objectReferenceValue = sVal;
        so.FindProperty("corruptModal").objectReferenceValue      = cModal;
        so.FindProperty("btnCorruptVolver").objectReferenceValue  = cBtn;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(mgr);
        return true;
    }

    static GameObject BuildStorageModal(Transform canvas, out Button btnVolver, out Image barFill, out TextMeshProUGUI valueLabel)
    {
        var modal = MakeFill(canvas, "StorageModal");
        var dim = modal.AddComponent<Image>(); dim.color = new Color(0f, 0f, 0f, 0.62f);

        var panel = MakePanel(modal.transform, "Panel", Vector2.zero, new Vector2(700f, 520f), MODAL_BG);
        AddBorder(panel, Color.white, 1f);

        MakeText(panel.transform, "Title", "No se pudo crear el universo", new Vector2(0f, 195f),
            new Vector2(640f, 70f), 36f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold, true);
        MakeText(panel.transform, "Body",
            "Tu dispositivo no tiene espacio suficiente para guardar este universo. El listado quedó intacto, sin entradas dañadas.",
            new Vector2(0f, 105f), new Vector2(600f, 90f), 22f, TEXT_GRAY, TextAlignmentOptions.Center, FontStyles.Normal, true);

        // Barra de almacenamiento
        MakeText(panel.transform, "BarLabel", "Almacenamiento del dispositivo", new Vector2(-115f, 32f),
            new Vector2(360f, 28f), 20f, TEXT_GRAY, TextAlignmentOptions.Left, FontStyles.Normal);
        valueLabel = MakeText(panel.transform, "StorageValue", "—", new Vector2(235f, 32f),
            new Vector2(180f, 28f), 20f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);

        MakePanel(panel.transform, "BarTrack", new Vector2(0f, -2f), new Vector2(600f, 22f), Hex("141738"));
        var fillGo = MakeImg(panel.transform, "BarFill", new Vector2(0f, -2f), new Vector2(600f, 22f), Hex("F5A623"), s_rounded);
        barFill = fillGo.GetComponent<Image>();
        barFill.type       = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        barFill.fillAmount = 0.95f;

        // Tip
        var tip = MakePanel(panel.transform, "TipBox", new Vector2(0f, -88f), new Vector2(600f, 70f), Hex("33356B"));
        MakeImg(tip.transform, "Bulb", new Vector2(-272f, 0f), new Vector2(24f, 24f), Hex("F5C543"), s_circle);
        MakeText(tip.transform, "Tip", "Libera espacio o elimina algún universo que ya no uses para crear uno nuevo.",
            new Vector2(18f, 0f), new Vector2(520f, 56f), 18f, TEXT_GRAY, TextAlignmentOptions.Left, FontStyles.Normal, true);

        btnVolver = MakeButton(panel.transform, "BtnVolver", "Volver al listado", new Vector2(0f, -190f),
            new Vector2(440f, 86f), Hex("3A3D70"), Color.white);

        return modal;
    }

    static GameObject BuildCorruptModal(Transform canvas, out Button btnVolver)
    {
        var modal = MakeFill(canvas, "CorruptModal");
        var dim = modal.AddComponent<Image>(); dim.color = new Color(0f, 0f, 0f, 0.62f);

        var panel = MakePanel(modal.transform, "Panel", Vector2.zero, new Vector2(700f, 430f), MODAL_BG);
        AddBorder(panel, Color.white, 1f);

        MakeText(panel.transform, "Title", "No se pudo abrir este universo", new Vector2(0f, 140f),
            new Vector2(640f, 70f), 36f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold, true);
        MakeText(panel.transform, "Body",
            "El archivo de este universo está dañado o no se puede leer. Los demás universos siguen intactos.",
            new Vector2(0f, 45f), new Vector2(580f, 90f), 22f, TEXT_GRAY, TextAlignmentOptions.Center, FontStyles.Normal, true);

        var tip = MakePanel(panel.transform, "TipBox", new Vector2(0f, -55f), new Vector2(600f, 76f), Hex("253266"));
        MakeImg(tip.transform, "Bulb", new Vector2(-272f, 0f), new Vector2(24f, 24f), Hex("4A90E2"), s_circle);
        MakeText(tip.transform, "Tip", "Si el problema persiste, puedes eliminarlo y crear uno nuevo. Los descubrimientos de tu diario no se perderán.",
            new Vector2(18f, 0f), new Vector2(520f, 60f), 18f, TEXT_GRAY, TextAlignmentOptions.Left, FontStyles.Normal, true);

        btnVolver = MakeButton(panel.transform, "BtnVolver", "Volver al listado", new Vector2(0f, -160f),
            new Vector2(440f, 86f), Hex("3A3D70"), Color.white);

        return modal;
    }

    // ── Helpers de UI para los modales ─────────────────────────────────────────
    static void SetRT2(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static Image MakePanel(Transform p, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = MakeEmpty(p, name); SetRT2(go, pos, size);
        var img = go.AddComponent<Image>(); img.sprite = s_rounded; img.type = Image.Type.Sliced; img.color = color;
        return img;
    }

    static GameObject MakeImg(Transform p, string name, Vector2 pos, Vector2 size, Color color, Sprite spr)
    {
        var go = MakeEmpty(p, name); SetRT2(go, pos, size);
        var img = go.AddComponent<Image>(); img.color = color; if (spr != null) img.sprite = spr;
        img.preserveAspect = true;
        return go;
    }

    static TextMeshProUGUI MakeText(Transform p, string name, string text, Vector2 pos, Vector2 size,
        float fs, Color c, TextAlignmentOptions a, FontStyles st, bool wrap = false)
    {
        var go = MakeEmpty(p, name); SetRT2(go, pos, size);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.font = s_fnt; t.fontSize = fs; t.color = c; t.alignment = a; t.fontStyle = st;
        t.enableWordWrapping = wrap; t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static Button MakeButton(Transform p, string name, string label, Vector2 pos, Vector2 size, Color bg, Color tc)
    {
        var img = MakePanel(p, name, pos, size, bg);
        var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img;
        var cb = b.colors; cb.highlightedColor = new Color(1f, 1f, 1f, 0.92f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f); b.colors = cb;
        MakeText(img.transform, "Label", label, Vector2.zero, size, 30f, tc, TextAlignmentOptions.Center, FontStyles.Bold);
        return b;
    }

    static void AddBorder(Image panel, Color color, float alpha)
    {
        var parent = panel.transform.parent;
        var rt = panel.rectTransform;
        var b = MakeImg(parent, panel.name + "_Border", rt.anchoredPosition, rt.sizeDelta + new Vector2(8f, 8f),
            new Color(color.r, color.g, color.b, alpha), s_rounded);
        var brt = (RectTransform)b.transform;
        brt.anchorMin = rt.anchorMin; brt.anchorMax = rt.anchorMax; brt.pivot = rt.pivot;
        brt.anchoredPosition = rt.anchoredPosition;
        var bimg = b.GetComponent<Image>(); bimg.type = Image.Type.Sliced; bimg.preserveAspect = false;
        b.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());
    }
}
