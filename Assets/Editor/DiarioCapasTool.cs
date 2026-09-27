using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>
/// Le añade al visor del diario los tres interruptores de capas: símbolos,
/// electronegatividad y tipo de enlace con δ+/δ−.
///
/// NO monta un modal encima. Esta pantalla YA muestra la molécula en 3D con el mismo
/// visor que usa la pantalla de explicación —es el mismo componente— así que abrir un
/// modal sería tapar una molécula con la misma molécula. Se le dan los interruptores al
/// visor que ya está.
///
/// Recoloca el visor para hacerles sitio: encoge un poco y sube, y las tres filas van
/// debajo. Es aditiva y se puede volver a correr.
/// </summary>
public static class DiarioCapasTool
{
    const string SCENE = "Assets/Scenes/MoleculeDetailScene.unity";

    static readonly Color CARD = Hex("0E1130");
    static readonly Color CYAN = Hex("19A7CE");
    static readonly Color DIM  = Hex("A2A2A2");

    static TMP_FontAsset fnt;
    static Sprite        rounded;
    static readonly List<string> problems = new List<string>();

    [MenuItem("ChemiTech/Diario: capas en el visor 3D")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Capas en el diario",
                "Añade los tres interruptores de capas al visor de MoleculeDetailScene " +
                "y recoloca el visor para hacerles sitio.\n\n" +
                "Abre esa escena: guarda antes lo que tengas sin guardar.\n\n¿Continuar?",
                "Sí, continuar", "Cancelar"))
            return;

        problems.Clear();
        fnt     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Fredoka-Medium SDF.asset");
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        if (!fnt)     problems.Add("No encontré la fuente Fredoka-Medium SDF.");
        if (!rounded) problems.Add("No encontré el sprite rounded-panel.");

        var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);

        var panel = FindDeepInScene(scene, "ViewerPanel");
        if (!panel)
        {
            EditorUtility.DisplayDialog("No pude empezar",
                "No encontré 'ViewerPanel' en MoleculeDetailScene.", "OK");
            return;
        }

        // El visor se encoge y sube: las tres filas necesitan la mitad inferior.
        var viewer = FindDeep(panel.transform, "Viewer");
        if (viewer) SetRT(viewer, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(470f, 410f));
        else problems.Add("No encontré el 'Viewer' dentro de ViewerPanel.");

        var hint = FindDeep(panel.transform, "Hint");
        if (hint) SetRT(hint, new Vector2(0.5f, 0.5f), new Vector2(0f, -122f), new Vector2(600f, 30f));

        MakeText(panel.transform, "LayersCaption", "Capas",
                 new Vector2(0.5f, 0.5f), new Vector2(-250f, -158f), new Vector2(160f, 30f),
                 19f, DIM, TextAlignmentOptions.Left, FontStyles.Bold);

        var tglSymbols = MakeToggle(panel.transform, "TglSymbols", "Símbolos",
                                    new Vector2(0f, -196f));
        var tglEn      = MakeToggle(panel.transform, "TglElectronegativity", "Electronegatividad",
                                    new Vector2(0f, -250f));
        var tglBonds   = MakeToggle(panel.transform, "TglBondTypes", "Tipo de enlace (δ+ / δ−)",
                                    new Vector2(0f, -304f));

        // Reproducir el guion, si esta molécula tiene. Oculto hasta saberlo.
        var btnPlay = MakeButton(panel.transform, "BtnPlay", "Ver cómo ocurre",
                                 new Vector2(0f, -352f), new Vector2(300f, 48f));
        btnPlay.gameObject.SetActive(false);

        var caption = MakeText(panel.transform, "Caption", "",
                               new Vector2(0.5f, 0.5f), new Vector2(0f, -398f), new Vector2(620f, 52f),
                               19f, Hex("BFE9F2"), TextAlignmentOptions.Center, FontStyles.Normal);

        var mgr = FindInScene<MoleculeDetailManager>(scene);
        if (!mgr) problems.Add("No encontré MoleculeDetailManager.");
        else
        {
            var so = new SerializedObject(mgr);
            Set(so, "tglSymbols",           tglSymbols);
            Set(so, "tglElectronegativity", tglEn);
            Set(so, "tglBondTypes",         tglBonds);
            Set(so, "btnPlay",              btnPlay);
            Set(so, "captionLabel",         caption);
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
        Report();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static Button MakeButton(Transform parent, string name, string label,
                             Vector2 pos, Vector2 size)
    {
        var go = EnsureChild(parent, name);
        SetRT(go, new Vector2(0.5f, 0.5f), pos, size);
        var img = Ensure<Image>(go);
        img.sprite = rounded; img.type = Image.Type.Sliced; img.color = Hex("2ECC71");
        var btn = Ensure<Button>(go); btn.targetGraphic = img;

        var lbl = EnsureChild(go.transform, "Label");
        var lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var tmp = Ensure<TextMeshProUGUI>(lbl);
        tmp.text = label; tmp.font = fnt; tmp.fontSize = 21f;
        tmp.fontStyle = FontStyles.Bold; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return btn;
    }

    static Toggle MakeToggle(Transform parent, string name, string label, Vector2 pos)
    {
        var go = EnsureChild(parent, name);
        SetRT(go, new Vector2(0.5f, 0.5f), pos, new Vector2(560f, 48f));

        var box = EnsureChild(go.transform, "Box");
        SetRT(box, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
        var boxImg = Ensure<Image>(box);
        boxImg.sprite = rounded; boxImg.type = Image.Type.Sliced; boxImg.color = CARD;

        var check = EnsureChild(box.transform, "Check");
        SetRT(check, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
        var checkImg = Ensure<Image>(check);
        checkImg.sprite = rounded; checkImg.type = Image.Type.Sliced; checkImg.color = CYAN;

        MakeText(go.transform, "Label", label,
                 new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(500f, 40f),
                 21f, Color.white, TextAlignmentOptions.Left, FontStyles.Normal);

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
        if (!value)      problems.Add($"'{prop}' se quedó vacío.");
        p.objectReferenceValue = value;
    }

    static GameObject FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t && t.name == name) return t.gameObject;
        return null;
    }

    static GameObject FindDeepInScene(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            var found = FindDeep(go.transform, name);
            if (found) return found;
        }
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

    static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c ? c : go.AddComponent<T>();
    }

    static void SetRT(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        if (!rt) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
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
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void Report()
    {
        if (problems.Count == 0)
        {
            Debug.Log("[DiarioCapas] ✓ Interruptores añadidos al visor del diario.");
            EditorUtility.DisplayDialog("¡Listo!",
                "El visor del diario ya tiene sus tres capas.\n\n" +
                "Electronegatividad y tipo de enlace solo se pueden encender si el " +
                "servidor mandó esos datos en el detalle de la molécula.", "OK");
            return;
        }

        var msg = string.Join("\n  • ", problems);
        Debug.LogWarning("[DiarioCapas] Quedaron cosas sin resolver:\n  • " + msg);
        EditorUtility.DisplayDialog("Terminó, pero con avisos",
            "Se aplicó lo que se pudo. Sin resolver:\n\n  • " + msg, "OK");
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
