using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>
/// Monta la pantalla de explicación de molécula en las dos escenas de clase y en el universo, y el botón
/// que la abre.
///
/// EL VISOR 3D NECESITA SU PROPIA CÁMARA, y eso es lo delicado aquí. Se resuelve
/// SEPARÁNDOLO EN EL ESPACIO: el escenario de la molécula vive a 1000 unidades de
/// altura, fuera del alcance de la cámara de clase (que está acotada a y<=10 y recorta
/// a 250) y fuera del alcance de la del visor (que recorta a 100). Ninguna de las dos
/// puede ver el mundo de la otra, sin tocar capas ni ajustes de proyecto.
///
/// Y LA CÁMARA DEL VISOR NO SE ETIQUETA MainCamera. En el diario sí lo está porque es la
/// única de su escena; aquí robaría Camera.main y con ella el cielo estrellado
/// (ZoneEnvironment) y el respaldo de detección de toques del alumno.
///
/// Es aditiva y se puede volver a correr. Abre las tres escenas: guarda antes.
/// </summary>
public static class ExplicacionPanelTool
{
    const string TEACHER  = "Assets/Scenes/ClaseDocenteScene.unity";
    const string STUDENT  = "Assets/Scenes/ClaseEstudianteScene.unity";
    const string UNIVERSE = "Assets/Scenes/ZonaJuegoScene.unity";

    const float STAGE_Y = 1000f;   // lejos de la clase, ver nota de arriba

    static readonly Color PANEL   = Hex("242659");
    static readonly Color CARD    = Hex("141633");
    static readonly Color CYAN    = Hex("19A7CE");
    static readonly Color GREEN   = Hex("2ECC71");
    static readonly Color PURPLE  = Hex("5A5FA5");
    static readonly Color DIM     = Hex("A2A2A2");

    static TMP_FontAsset fnt;
    static Sprite        rounded;
    static readonly List<string> problems = new List<string>();

    [MenuItem("ChemiTech/Clases: pantalla de explicación de molécula")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Pantalla de explicación",
                "Monta el modal de capas (símbolos, electronegatividad, tipo de enlace) " +
                "y el botón \"Ver explicación\" en las dos escenas de clase y en el universo.\n\n" +
                "Abre las tres escenas: guarda antes lo que tengas sin guardar.\n\n¿Continuar?",
                "Sí, continuar", "Cancelar"))
            return;

        problems.Clear();
        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        if (!fnt)     problems.Add("No encontré la fuente Fredoka-Medium SDF.");
        if (!rounded) problems.Add("No encontré el sprite rounded-panel.");

        foreach (var path in new[] { TEACHER, STUDENT, UNIVERSE })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            BuildInto(scene, path);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.Refresh();
        Report();
    }

    static void BuildInto(Scene scene, string path)
    {
        bool teacher  = path == TEACHER;
        bool universe = path == UNIVERSE;

        var canvas = FindRoot(scene, "Canvas");
        if (!canvas) { problems.Add($"No hay Canvas en {scene.name}."); return; }

        var atomMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Atom_Base.mat");
        var bondMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Bond.mat");
        if (!atomMat) problems.Add("Falta Assets/Materials/Atom_Base.mat");
        if (!bondMat) problems.Add("Falta Assets/Materials/Bond.mat");

        // ── El mundo aparte del visor ─────────────────────────────────────────
        var stageWorld = FindRoot(scene, "MoleculeStage");
        if (!stageWorld)
        {
            stageWorld = new GameObject("MoleculeStage");
            SceneManager.MoveGameObjectToScene(stageWorld, scene);
        }
        stageWorld.transform.position = Vector3.zero;

        var stage = EnsureChild3D(stageWorld.transform, "StageRoot");
        stage.transform.position = new Vector3(0f, STAGE_Y, 0f);

        var camGo = EnsureChild3D(stageWorld.transform, "MoleculeCamera");
        camGo.transform.SetPositionAndRotation(new Vector3(0f, STAGE_Y, -8f), Quaternion.identity);
        // NO se etiqueta MainCamera (ver nota de la clase) ni lleva AudioListener: ya hay
        // uno en la escena y dos hacen que Unity avise en cada carga.
        camGo.tag = "Untagged";
        var vcam = Ensure<Camera>(camGo);
        vcam.clearFlags      = CameraClearFlags.SolidColor;
        vcam.backgroundColor = Hex("0E1130");
        vcam.orthographic    = false;
        vcam.fieldOfView     = 40f;
        vcam.nearClipPlane   = 0.1f;
        vcam.farClipPlane    = 100f;
        vcam.enabled         = false;   // la enciende el visor al abrirse

        // ── El modal ──────────────────────────────────────────────────────────
        var panel = EnsureChild(canvas.transform, "ExplanationPanel");
        Stretch(panel);
        ClearChildren(panel.transform);

        // El fondo SÍ captura toques: con el modal abierto no se debe poder rotar la
        // clase ni pulsar un botón de detrás sin querer.
        var backdrop = EnsureChild(panel.transform, "Backdrop");
        Stretch(backdrop);
        var bImg = Ensure<Image>(backdrop);
        bImg.color = new Color(0f, 0f, 0f, 0.78f);
        bImg.raycastTarget = true;

        var card = EnsureChild(panel.transform, "Card");
        SetRT(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 840f));
        var cImg = Ensure<Image>(card);
        cImg.sprite = rounded; cImg.type = Image.Type.Sliced; cImg.color = PANEL;

        var nameLabel = MakeText(card.transform, "Name", "Molécula",
                                 new Vector2(0f, 1f), new Vector2(48f, -36f), new Vector2(700f, 56f),
                                 38f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        var formulaLabel = MakeText(card.transform, "Formula", "",
                                    new Vector2(0f, 1f), new Vector2(48f, -96f), new Vector2(700f, 44f),
                                    26f, Hex("BFE9F2"), TextAlignmentOptions.Left, FontStyles.Normal);

        var btnClose = MakeButton(card.transform, "BtnClose", "Cerrar", PURPLE,
                                  new Vector2(1f, 1f), new Vector2(-48f, -36f), new Vector2(190f, 56f), 22f);

        // Visor 3D a la izquierda
        var viewerGo = EnsureChild(card.transform, "Viewer");
        SetRT(viewerGo, new Vector2(0f, 0.5f), new Vector2(60f, -40f), new Vector2(700f, 600f));
        var raw = Ensure<RawImage>(viewerGo);
        raw.color = Color.white;
        var viewer = Ensure<MoleculeViewer3D>(viewerGo);

        MakeText(card.transform, "Hint", "Toca y arrastra para rotar",
                 new Vector2(0f, 0.5f), new Vector2(60f, -300f), new Vector2(700f, 30f),
                 19f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Center, FontStyles.Normal);

        // Dos animaciones, dos botones. La de la izquierda se DEDUCE de la química de la
        // molécula y existe para todas; la de la derecha es un guion escrito y solo
        // aparece en las dos que lo tienen. Cuentan cosas distintas, así que no se
        // sustituyen: una explica cómo se forma ESTA molécula, la otra por qué dos aguas
        // se atraen.
        var btnPlay = MakeButton(card.transform, "BtnPlay", "Ver cómo se forma", GREEN,
                                 new Vector2(0f, 0.5f), new Vector2(60f, -344f), new Vector2(330f, 56f), 21f);
        btnPlay.gameObject.SetActive(false);

        var btnPlayExtra = MakeButton(card.transform, "BtnPlayExtra", "Ver más", PURPLE,
                                      new Vector2(0f, 0.5f), new Vector2(410f, -344f), new Vector2(350f, 56f), 20f);
        btnPlayExtra.gameObject.SetActive(false);

        // El texto del paso. Es MEDIA EXPLICACIÓN: la animación muestra qué pasa y esta
        // línea dice por qué, así que tiene su propio sitio y no comparte con la pista.
        var caption = MakeText(card.transform, "Caption", "",
                               new Vector2(0f, 0.5f), new Vector2(60f, -398f), new Vector2(700f, 60f),
                               21f, Hex("BFE9F2"), TextAlignmentOptions.Center, FontStyles.Normal);

        // ── Tarjeta 2D a la derecha ───────────────────────────────────────────
        var card2D = EnsureChild(card.transform, "Card2D");
        SetRT(card2D, new Vector2(1f, 1f), new Vector2(-60f, -110f), new Vector2(680f, 330f));
        var c2Img = Ensure<Image>(card2D);
        c2Img.sprite = rounded; c2Img.type = Image.Type.Sliced; c2Img.color = CARD;
        MakeText(card2D.transform, "Caption", "Fórmula estructural",
                 new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(640f, 32f),
                 20f, DIM, TextAlignmentOptions.Center, FontStyles.Normal);

        // Donde se dibujan los átomos y los enlaces planos. Va separado del rótulo para
        // que Structure2DCard pueda vaciarlo entero sin llevarse el título por delante.
        var drawing = EnsureChild(card2D.transform, "Drawing");
        SetRT(drawing, new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(620f, 250f));

        // Respaldo: la fórmula molecular, para las moléculas sin fórmula plana (la sal).
        var card2DText = MakeText(card2D.transform, "Text", "",
                                  new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(640f, 120f),
                                  64f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var flat = Ensure<Structure2DCard>(card2D);
        var fso  = new SerializedObject(flat);
        Set(fso, "canvasArea", drawing.GetComponent<RectTransform>());
        Set(fso, "labelFont",  fnt);
        fso.ApplyModifiedProperties();

        // ── Capas, bajo la tarjeta ────────────────────────────────────────────
        MakeText(card.transform, "LayersCaption", "Capas",
                 new Vector2(1f, 1f), new Vector2(-60f, -470f), new Vector2(680f, 34f),
                 22f, DIM, TextAlignmentOptions.Left, FontStyles.Bold);

        var tglSymbols = MakeToggle(card.transform, "TglSymbols", "Símbolos",
                                    new Vector2(-60f, -516f));
        var tglEn      = MakeToggle(card.transform, "TglElectronegativity", "Electronegatividad",
                                    new Vector2(-60f, -584f));
        var tglBonds   = MakeToggle(card.transform, "TglBondTypes", "Tipo de enlace (δ+ / δ−)",
                                    new Vector2(-60f, -652f));

        var comp = Ensure<ExplanationPanel>(panel);

        // ── Cableado ──────────────────────────────────────────────────────────
        var vso = new SerializedObject(viewer);
        Set(vso, "cam",          vcam);
        Set(vso, "stageRoot",    stage.transform);
        Set(vso, "atomMaterial", atomMat);
        Set(vso, "bondMaterial", bondMat);
        Set(vso, "labelFont",    fnt);
        Set(vso, "target",       raw);
        vso.ApplyModifiedProperties();

        var pso = new SerializedObject(comp);
        Set(pso, "nameLabel",            nameLabel);
        Set(pso, "formulaLabel",         formulaLabel);
        Set(pso, "viewer",               viewer);
        Set(pso, "tglSymbols",           tglSymbols);
        Set(pso, "tglElectronegativity", tglEn);
        Set(pso, "tglBondTypes",         tglBonds);
        Set(pso, "card2D",               card2D);
        Set(pso, "card2DText",           card2DText);
        Set(pso, "structure2D",          flat);
        Set(pso, "btnClose",             btnClose);
        Set(pso, "btnPlay",              btnPlay);
        Set(pso, "btnPlayExtra",         btnPlayExtra);
        Set(pso, "captionLabel",         caption);
        pso.ApplyModifiedProperties();

        // ── El botón que lo abre, y el manager ────────────────────────────────
        // En el universo el botón va ARRIBA A LA IZQUIERDA, bajo el de pausa: abajo lo
        // ocupan la barra de ranuras y el de colocar, que se usan constantemente.
        Button btnExplicar =
            teacher  ? MakeButton(canvas.transform, "BtnExplicar", "Ver explicación", CYAN,
                                  new Vector2(0f, 1f), new Vector2(1290f, -108f), new Vector2(280f, 56f), 22f)
          : universe ? MakeButton(canvas.transform, "BtnExplicar", "Ver explicación", CYAN,
                                  new Vector2(0f, 1f), new Vector2(24f, -100f), new Vector2(280f, 56f), 22f)
                     : MakeButton(canvas.transform, "BtnExplicar", "Ver explicación", CYAN,
                                  new Vector2(0.5f, 0f), new Vector2(-300f, 80f), new Vector2(440f, 62f), 23f);
        btnExplicar.gameObject.SetActive(false);   // aparece al elegir una molécula

        if (teacher)       WireManager(FindInScene<ClaseDocenteManager>(scene),    "ClaseDocenteManager",    btnExplicar, panel);
        else if (universe) WireManager(FindInScene<ZonaJuegoManager>(scene),       "ZonaJuegoManager",       btnExplicar, panel);
        else               WireManager(FindInScene<ClaseEstudianteManager>(scene), "ClaseEstudianteManager", btnExplicar, panel);

        // El modal por encima de todo, y apagado.
        panel.transform.SetAsLastSibling();
        panel.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Los tres managers declaran los MISMOS DOS campos, así que se cablean
    /// igual. Si alguno los renombra, Set() lo dice en el informe en vez de dejar el
    /// botón muerto en silencio.</summary>
    static void WireManager(Component mgr, string nombre, Button btn, GameObject panel)
    {
        if (!mgr) { problems.Add($"No encontré {nombre}."); return; }
        var so = new SerializedObject(mgr);
        Set(so, "btnExplicar",      btn);
        Set(so, "explanationPanel", panel);
        so.ApplyModifiedProperties();
    }

    static Toggle MakeToggle(Transform parent, string name, string label, Vector2 pos)
    {
        var go = EnsureChild(parent, name);
        SetRT(go, new Vector2(1f, 1f), pos, new Vector2(680f, 58f));

        var box = EnsureChild(go.transform, "Box");
        SetRT(box, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(46f, 46f));
        var boxImg = Ensure<Image>(box);
        boxImg.sprite = rounded; boxImg.type = Image.Type.Sliced; boxImg.color = CARD;

        var check = EnsureChild(box.transform, "Check");
        SetRT(check, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
        var checkImg = Ensure<Image>(check);
        checkImg.sprite = rounded; checkImg.type = Image.Type.Sliced; checkImg.color = CYAN;

        MakeText(go.transform, "Label", label,
                 new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(600f, 46f),
                 23f, Color.white, TextAlignmentOptions.Left, FontStyles.Normal);

        var tgl = Ensure<Toggle>(go);
        tgl.targetGraphic = boxImg;
        tgl.graphic       = checkImg;
        tgl.isOn          = false;
        return tgl;
    }

    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { problems.Add($"El campo '{prop}' no existe en {so.targetObject.GetType().Name}."); return; }
        if (!value)      problems.Add($"'{prop}' se quedó vacío: no encontré a qué apuntar.");
        p.objectReferenceValue = value;
    }

    static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects()) if (go.name == name) return go;
        return null;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found) return found;
        }
        return null;
    }

    static GameObject EnsureChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t) return t.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>Hijo SIN RectTransform: es geometría del mundo, no interfaz.</summary>
    static GameObject EnsureChild3D(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t) return t.gameObject;
        var go = new GameObject(name);
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

    static void Report()
    {
        if (problems.Count == 0)
        {
            Debug.Log("[Explicacion] ✓ Pantalla montada en las tres escenas.");
            EditorUtility.DisplayDialog("¡Listo!",
                "El modal está montado en las tres escenas.\n\n" +
                "Para verlo: entra a una clase con algo en la pizarra, TOCA UN ÁTOMO y " +
                "aparecerá \"Ver explicación\".\n\n" +
                "Las capas de electronegatividad y tipo de enlace solo se pueden encender " +
                "si esa molécula trae el dato; desde la pizarra sí lo trae.", "OK");
            return;
        }

        var msg = string.Join("\n  • ", problems);
        Debug.LogWarning("[Explicacion] Quedaron cosas sin resolver:\n  • " + msg);
        EditorUtility.DisplayDialog("Terminó, pero con avisos",
            "Se aplicó lo que se pudo. Sin resolver:\n\n  • " + msg, "OK");
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
