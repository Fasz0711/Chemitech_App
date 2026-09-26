using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>
/// Convierte ClaseDocenteScene en "la pizarra como universo": le añade el HUD creativo
/// para que el docente pueda construir, y el botón que alterna entre las dos interfaces.
///
/// ES ADITIVA: no recrea el Canvas ni toca el panel de conducción, solo lo agrupa bajo
/// UI_Pizarra para poder apagarlo entero. Se puede volver a correr; rehace lo suyo.
///
/// EL HUD CREATIVO SE COPIA DE ZonaJuegoScene en vez de reconstruirse. El selector de
/// átomos son ~200 líneas de modal con buscador, filtros, rejilla y seis ranuras, y una
/// segunda copia escrita a mano se separaría de la original en cuanto alguien tocara
/// una. Copiando, es literalmente el mismo HUD.
///
/// OJO: "ChemiTech/Build Clase Docente Scene" RECREA EL CANVAS ENTERO y se lleva todo
/// esto por delante. Si hay que correrlo, correr esto después.
/// </summary>
public static class PizarraUniversoTool
{
    const string TEACHER_SCENE = "Assets/Scenes/ClaseDocenteScene.unity";
    const string ZONA_SCENE    = "Assets/Scenes/ZonaJuegoScene.unity";

    static readonly Color PANEL = Hex("33356B");
    static readonly Color GREEN = Hex("2ECC71");
    static readonly Color AMBER = Hex("F5A623");

    // Lo que se conserva del HUD copiado. Es una lista de lo que SE QUEDA y no de lo que
    // se borra a propósito: ZonaJuegoScene ha ido recibiendo cosas por otras herramientas
    // (indicador de detección, modal de descubrimiento…) y una lista de borrado se
    // quedaría corta en silencio cada vez que alguien añada algo allí.
    static readonly string[] KEEP =
    {
        "BtnRecentrar",        // recentrar la cámara
        "StatusBanner",        // "Detectando…" / "¡Molécula formada!"
        "DPad", "VertPad",     // mover el átomo activo o la cámara
        "Hotbar",              // las seis ranuras
        "BtnPlace", "BtnSelector",
        "DeleteBar", "CancelBar",
        "PlacementReticle",
        "AtomSelectorModal",   // el selector completo
        "CollisionModal",
        "AtomSelector",        // el GameObject con AtomSelectorController
    };

    // El panel de conducción que pasa a vivir bajo UI_Pizarra. Se mueven tal cual: el
    // contenedor es un rect estirado idéntico al Canvas, así que las anclas y los
    // desplazamientos de cada hijo siguen valiendo lo mismo y nada se recoloca.
    static readonly string[] PIZARRA_UI =
    {
        "EmptyHint", "HighlightBar", "BtnQuitarResaltado", "BtnModoSumar",
        "ActionBar", "PromptRow", "ThinkingIndicator", "PreviewPanel",
    };

    static TMP_FontAsset fnt;
    static Sprite        rounded;
    static readonly List<string> problems = new List<string>();

    [MenuItem("ChemiTech/Pizarra: convertir en Universo")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("La pizarra como universo",
                "Añade el HUD creativo a ClaseDocenteScene y el botón para alternar.\n\n" +
                "Es aditiva: no recrea el Canvas.\n" +
                "Copia el HUD desde ZonaJuegoScene (que NO se modifica).\n\n¿Continuar?",
                "Sí, continuar", "Cancelar"))
            return;

        problems.Clear();
        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        if (!fnt)     problems.Add("No encontré la fuente Fredoka-Medium SDF.");
        if (!rounded) problems.Add("No encontré el sprite rounded-panel.");

        var scene  = EditorSceneManager.OpenScene(TEACHER_SCENE, OpenSceneMode.Single);
        var canvas = FindRoot(scene, "Canvas");
        var mgrGo  = FindDeepInScene(scene, "ClaseDocenteManager");
        var camGo  = FindRoot(scene, "Main Camera");

        if (!canvas || !mgrGo || !camGo)
        {
            EditorUtility.DisplayDialog("No pude empezar",
                "Falta Canvas, ClaseDocenteManager o Main Camera en ClaseDocenteScene.", "OK");
            return;
        }

        var mgr   = mgrGo.GetComponent<ClaseDocenteManager>();
        var orbit = camGo.GetComponent<OrbitCameraController>();
        var cam   = camGo.GetComponent<Camera>();
        if (!mgr)   problems.Add("El objeto ClaseDocenteManager no tiene el componente.");
        if (!orbit) problems.Add("Main Camera no tiene OrbitCameraController.");

        // ── 1) El panel de conducción, agrupado para poder apagarlo entero ────
        var uiPizarra = EnsureGroup(canvas.transform, "UI_Pizarra");
        foreach (var name in PIZARRA_UI)
        {
            var t = canvas.transform.Find(name);
            if (t) t.SetParent(uiPizarra.transform, false);
            else if (!uiPizarra.transform.Find(name))
                problems.Add($"No encontré '{name}' para mover a UI_Pizarra.");
        }

        // ── 2) El HUD creativo, copiado de la zona de juego ───────────────────
        var uiUniverso = CopyCreativeHud(scene, canvas.transform);
        if (!uiUniverso) return;   // CopyCreativeHud ya avisó

        // ── 3) Colocación de átomos y detección ───────────────────────────────
        var atomMat  = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Atom_Base.mat");
        var ghostMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Atom_Ghost.mat");
        var bondMat  = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Bond.mat");
        if (!atomMat)  problems.Add("Falta Assets/Materials/Atom_Base.mat");
        if (!ghostMat) problems.Add("Falta Assets/Materials/Atom_Ghost.mat (lo crea el builder de ZonaJuego).");
        if (!bondMat)  problems.Add("Falta Assets/Materials/Bond.mat");

        var placeGo = EnsureChild(canvas.transform, "AtomPlacement");
        var place   = Ensure<AtomPlacementController>(placeGo);
        var u       = uiUniverso.transform;

        var pso = new SerializedObject(place);
        Set(pso, "cam",              cam);
        Set(pso, "orbit",            orbit);
        Set(pso, "atomBaseMaterial", atomMat);
        Set(pso, "previewMaterial",  ghostMat);
        Set(pso, "reticleRoot",      FindDeep(u, "PlacementReticle"));
        Set(pso, "reticleDot",       Comp<Image>(FindDeep(u, "Dot")));
        Set(pso, "btnPlace",         Comp<Button>(FindDeep(u, "BtnPlace")));
        Set(pso, "cancelRoot",       FindDeep(u, "CancelBar"));
        Set(pso, "btnCancel",        Comp<Button>(FindDeep(u, "CancelBar")));
        Set(pso, "btnDeleteRoot",    FindDeep(u, "DeleteBar"));
        Set(pso, "btnDelete",        Comp<Button>(FindDeep(u, "DeleteBar")));
        Set(pso, "collisionModal",   FindDeep(u, "CollisionModal"));
        Set(pso, "btnCollisionOk",   Comp<Button>(FindDeep(u, "BtnEntendido")));
        Set(pso, "labelFont",        fnt);
        // La pizarra no dibuja una plataforma, así que el límite puede ser más ancho que
        // en la zona de juego. Hace falta: la lección del puente de hidrógeno separa dos
        // moléculas ~20 Å, y con el límite de 11.5 no cabrían.
        SetFloat(pso, "platformHalf", 24f);
        SetFloat(pso, "maxHeight",    18f);
        pso.ApplyModifiedProperties();

        // El selector vive dentro del HUD copiado, así que sus referencias internas ya
        // están remapeadas; la que apunta FUERA (a la colocación) es la que hay que rehacer.
        var selector = Comp<AtomSelectorController>(FindDeep(u, "AtomSelector"));
        if (selector) { var s = new SerializedObject(selector); Set(s, "placement", place); s.ApplyModifiedProperties(); }
        else problems.Add("No encontré AtomSelectorController en el HUD copiado.");

        var molGo   = EnsureChild(canvas.transform, "MoleculeSystem");
        var bondMgr = Ensure<BondManager>(molGo);
        var banner  = FindDeep(u, "StatusBanner");
        var bso = new SerializedObject(bondMgr);
        Set(bso, "placement",    place);
        Set(bso, "bondMaterial", bondMat);
        Set(bso, "bannerRoot",   banner);
        Set(bso, "bannerBg",     Comp<Image>(banner));
        Set(bso, "bannerText",   banner ? Comp<TextMeshProUGUI>(FindDeep(banner.transform, "Label")) : null);
        bso.ApplyModifiedProperties();

        // D-pad, flechas y "Recentrar": HoldButton.onHold es un delegado normal, hay que
        // asignarlo desde código y por eso existe este componente.
        var binder = Ensure<PlacementHudBinder>(EnsureChild(canvas.transform, "UniverseControls"));
        var hso = new SerializedObject(binder);
        Set(hso, "placement",    place);
        Set(hso, "cam",          orbit);
        Set(hso, "btnRecentrar", Comp<Button>(FindDeep(u, "BtnRecentrar")));
        Set(hso, "padUp",        Comp<HoldButton>(FindDeep(u, "PadUp")));
        Set(hso, "padDown",      Comp<HoldButton>(FindDeep(u, "PadDown")));
        Set(hso, "padLeft",      Comp<HoldButton>(FindDeep(u, "PadLeft")));
        Set(hso, "padRight",     Comp<HoldButton>(FindDeep(u, "PadRight")));
        Set(hso, "vertUp",       Comp<HoldButton>(FindDeep(u, "VertUp")));
        Set(hso, "vertDown",     Comp<HoldButton>(FindDeep(u, "VertDown")));
        hso.ApplyModifiedProperties();

        // ── 4) Controles nuevos, visibles en los DOS modos ────────────────────
        var tint = EnsureChild(canvas.transform, "EditTint");
        Stretch(tint);
        var tintImg = Ensure<Image>(tint);
        // Sutil a propósito: el docente tiene que seguir viendo los colores reales de los
        // átomos para explicar. Es una señal de "estás preparando", no un modo oscuro.
        tintImg.color = new Color(AMBER.r, AMBER.g, AMBER.b, 0.10f);
        tintImg.raycastTarget = false;
        tint.SetActive(false);

        var btnModo = MakeButton(canvas.transform, "BtnModo", "Ir a universo", PANEL,
                                 new Vector2(0f, 1f), new Vector2(340f, -108f), new Vector2(260f, 56f), 22f);
        var btnPub  = MakeButton(canvas.transform, "BtnPublicar", "Mostrar a la clase", GREEN,
                                 new Vector2(0f, 1f), new Vector2(616f, -108f), new Vector2(300f, 56f), 22f);

        var badge = EnsureChild(canvas.transform, "UnpublishedBadge");
        SetRT(badge, new Vector2(0f, 1f), new Vector2(932f, -108f), new Vector2(340f, 56f));
        var badgeImg = Ensure<Image>(badge);
        badgeImg.sprite = rounded; badgeImg.type = Image.Type.Sliced;
        badgeImg.color = AMBER; badgeImg.raycastTarget = false;
        MakeText(badge.transform, "Text", "Hay cambios sin publicar",
                 new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 48f),
                 21f, Hex("1E2050"), TextAlignmentOptions.Center, FontStyles.Bold);
        badge.SetActive(false);

        // Debajo de la fila de botones (que ocupa de -108 a -164).
        var summary = MakeText(canvas.transform, "SceneSummary", "",
                               new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(1100f, 38f),
                               21f, Hex("BFE9F2"), TextAlignmentOptions.Center, FontStyles.Normal);
        summary.gameObject.SetActive(false);

        var limits = MakeText(canvas.transform, "LimitsLabel", "",
                              new Vector2(0.5f, 1f), new Vector2(0f, -216f), new Vector2(900f, 40f),
                              22f, AMBER, TextAlignmentOptions.Center, FontStyles.Bold);
        limits.gameObject.SetActive(false);

        // ── 5) Orden de dibujado ──────────────────────────────────────────────
        // El tinte va sobre el 3D pero DEBAJO de toda la UI, y los avisos y modales
        // encima de todo o quedarían tapados por el HUD creativo.
        var rotate = canvas.transform.Find("RotateCatcher");
        int at = rotate ? rotate.GetSiblingIndex() + 1 : 0;
        tint.transform.SetSiblingIndex(at);
        Last(canvas.transform, "Notice");
        Last(canvas.transform, "StopModal");
        Last(canvas.transform, "DiscoveryModal");

        // Rotar la cámara pasa a llevarlo AtomPlacementController, como en la zona de
        // juego. Con los dos activos cada arrastre giraría el doble.
        //
        // Y NO BASTA CON APAGAR EL COMPONENTE: la capa que lo lleva es una Image a
        // pantalla completa que sigue capturando raycasts, y AtomPlacementController
        // ignora todo lo que caiga "sobre UI". Con el raycast encendido no vería ni un
        // solo toque: ni rotar, ni seleccionar un átomo, ni colocar.
        var drag = canvas.GetComponentInChildren<DragRotateCatcher>(true);
        if (drag)
        {
            drag.enabled = false;
            var img = drag.GetComponent<Image>();
            if (img) img.raycastTarget = false;
        }
        else problems.Add("No encontré DragRotateCatcher: comprueba que nada más rote la cámara.");

        // ── 6) El manager ─────────────────────────────────────────────────────
        if (mgr)
        {
            var m = new SerializedObject(mgr);
            Set(m, "placement",        place);
            Set(m, "bonds",            bondMgr);
            Set(m, "uiPizarra",        uiPizarra);
            Set(m, "uiUniverso",       uiUniverso);
            Set(m, "btnModo",          btnModo);
            Set(m, "lblModo",          btnModo.GetComponentInChildren<TextMeshProUGUI>(true));
            Set(m, "btnPublicar",      btnPub);
            Set(m, "unpublishedBadge", badge);
            Set(m, "limitsLabel",      limits);
            Set(m, "sceneSummary",     summary);
            Set(m, "editTint",         tint);
            m.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();

        Report();
    }

    // ── Copia del HUD creativo ────────────────────────────────────────────────

    static GameObject CopyCreativeHud(Scene target, Transform canvas)
    {
        var old = canvas.Find("UI_Universo");
        if (old) Object.DestroyImmediate(old.gameObject);

        var zona = EditorSceneManager.OpenScene(ZONA_SCENE, OpenSceneMode.Additive);
        GameObject copy = null;
        try
        {
            var zonaCanvas = FindRoot(zona, "Canvas");
            var hud = zonaCanvas ? zonaCanvas.transform.Find("HUD") : null;
            if (!hud)
            {
                problems.Add("No encontré Canvas/HUD en ZonaJuegoScene: no pude copiar el HUD creativo.");
                return null;
            }

            copy = Object.Instantiate(hud.gameObject);
            copy.name = "UI_Universo";
            copy.transform.SetParent(null, false);
            SceneManager.MoveGameObjectToScene(copy, target);
        }
        finally
        {
            // Sin guardar: ZonaJuegoScene no se toca.
            EditorSceneManager.CloseScene(zona, true);
        }

        if (!copy) return null;
        copy.transform.SetParent(canvas, false);
        Stretch(copy);

        // Fuera lo que no aplica: cronómetro, guardado, pausa, tutorial y lo que otras
        // herramientas hayan ido dejando en el HUD de la zona de juego.
        var keep = new HashSet<string>(KEEP);
        for (int i = copy.transform.childCount - 1; i >= 0; i--)
        {
            var child = copy.transform.GetChild(i);
            if (!keep.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
        }

        // El tutorial se engancha a objetos que acabamos de borrar.
        foreach (var t in copy.GetComponentsInChildren<TutorialManager>(true)) Object.DestroyImmediate(t);

        copy.SetActive(false);   // se entra conduciendo, no construyendo
        return copy;
    }

    // ── Verificación ──────────────────────────────────────────────────────────

    /// <summary>Una referencia mal puesta no da error: el campo se queda en null y el
    /// botón simplemente no hace nada. Por eso se comprueban todas y se informa.</summary>
    static void Report()
    {
        if (problems.Count == 0)
        {
            Debug.Log("[PizarraUniverso] ✓ Listo: ClaseDocenteScene tiene los dos modos.");
            EditorUtility.DisplayDialog("¡Listo!",
                "ClaseDocenteScene ya es un universo.\n\n" +
                "Entra conduciendo (modo pizarra). El botón \"Ir a universo\" está bajo el " +
                "nombre de la clase.\n\nRevisa en el Inspector que ClaseDocenteManager " +
                "tenga sus referencias nuevas.", "OK");
            return;
        }

        var msg = string.Join("\n  • ", problems);
        Debug.LogWarning("[PizarraUniverso] Quedaron cosas sin resolver:\n  • " + msg);
        EditorUtility.DisplayDialog("Terminó, pero con avisos",
            "Se aplicó lo que se pudo. Sin resolver:\n\n  • " + msg, "OK");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { problems.Add($"El campo '{prop}' no existe en {so.targetObject.GetType().Name}."); return; }
        if (!value)      problems.Add($"'{prop}' se quedó vacío: no encontré a qué apuntar.");
        p.objectReferenceValue = value;
    }

    static void SetFloat(SerializedObject so, string prop, float value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { problems.Add($"El campo '{prop}' no existe."); return; }
        p.floatValue = value;
    }

    static T Comp<T>(GameObject go) where T : Component => go ? go.GetComponent<T>() : null;

    static GameObject FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects()) if (go.name == name) return go;
        return null;
    }

    static GameObject FindDeepInScene(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == name) return go;
            var found = FindDeep(go.transform, name);
            if (found) return found;
        }
        return null;
    }

    static GameObject EnsureGroup(Transform parent, string name)
    {
        var go = EnsureChild(parent, name);
        Stretch(go);
        return go;
    }

    static GameObject EnsureChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t) return t.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c ? c : go.AddComponent<T>();
    }

    static RectTransform Rect(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        return rt ? rt : go.AddComponent<RectTransform>();
    }

    static void Last(Transform canvas, string name)
    {
        var t = canvas.Find(name);
        if (t) t.SetAsLastSibling();
    }

    static void Stretch(GameObject go)
    {
        var rt = Rect(go);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    static void SetRT(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = Rect(go);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    static Button MakeButton(Transform parent, string name, string label, Color color,
                             Vector2 anchor, Vector2 pos, Vector2 size, float fontSize)
    {
        var go = EnsureChild(parent, name);
        SetRT(go, anchor, pos, size);
        var img = Ensure<Image>(go);
        img.sprite = rounded; img.type = Image.Type.Sliced; img.color = color;
        var btn = Ensure<Button>(go); btn.targetGraphic = img;

        var lbl = EnsureChild(go.transform, "Label");
        Stretch(lbl);
        var tmp = Ensure<TextMeshProUGUI>(lbl);
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
        var go = EnsureChild(parent, name);
        SetRT(go, anchor, pos, size);
        var tmp = Ensure<TextMeshProUGUI>(go);
        tmp.text = text; tmp.font = fnt; tmp.fontSize = fontSize;
        tmp.fontStyle = style; tmp.color = color; tmp.alignment = align;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
