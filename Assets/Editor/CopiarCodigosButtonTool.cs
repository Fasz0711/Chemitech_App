using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Herramienta ADITIVA: agrega "Copiar al portapapeles" al modal de códigos de
/// MisClasesScene, junto al de cerrar.
///
/// POR QUE: las contrasenas de los alumnos solo se ven UNA vez, al crear la clase. Si el
/// docente cierra ese modal sin apuntarlas, la unica salida es reponerlas alumno por
/// alumno. Copiarlas de un toque es la red de seguridad.
///
/// Clona el boton de cerrar para heredar su estilo, y reparte los dos a los lados del
/// centro que ocupaba el original. Idempotente: si ya existe, no vuelve a mover nada.
///
/// Menu: ChemiTech -> Add -> Copiar Codigos Button
/// </summary>
public static class CopiarCodigosButtonTool
{
    static readonly Color COPY = Hex("5A5FA5");
    const float GAP = 14f;

    [MenuItem("ChemiTech/Add/Copiar Codigos Button")]
    public static void Add()
    {
        var scene  = EditorSceneManager.GetActiveScene();
        var canvas = FindRoot(scene, "Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Copiar codigos", "Abre primero MisClasesScene.", "OK");
            return;
        }

        var close = FindDeep(canvas.transform, "BtnCodesClose");
        if (close == null)
        {
            EditorUtility.DisplayDialog("Copiar codigos",
                "No encontre BtnCodesClose. Es esta MisClasesScene?", "OK");
            return;
        }

        var closeRT = close.GetComponent<RectTransform>();
        var existing = FindDeep(canvas.transform, "BtnCodesCopy");

        GameObject copy;
        if (existing != null)
        {
            copy = existing.gameObject;   // ya estaba: no se recolocan
        }
        else
        {
            copy = Object.Instantiate(close.gameObject, close.parent);
            copy.name = "BtnCodesCopy";

            // Reparte los dos a los lados del centro que ocupaba el de cerrar.
            float half = closeRT.sizeDelta.x * 0.5f + GAP;
            var copyRT = copy.GetComponent<RectTransform>();
            copyRT.anchoredPosition  = closeRT.anchoredPosition + new Vector2(-half, 0f);
            closeRT.anchoredPosition = closeRT.anchoredPosition + new Vector2( half, 0f);
        }

        var label = copy.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label) label.text = "Copiar al portapapeles";
        var img = copy.GetComponent<Image>();
        if (img) img.color = COPY;

        var btn = copy.GetComponent<Button>();
        if (btn) btn.onClick = new Button.ButtonClickedEvent();

        // Cableado en el manager
        var mgr = Object.FindFirstObjectByType<MisClasesManager>();
        if (mgr == null)
        {
            EditorUtility.DisplayDialog("Copiar codigos",
                "No encontre MisClasesManager; el boton quedo sin cablear.", "OK");
            return;
        }
        var so = new SerializedObject(mgr);
        var prop = so.FindProperty("btnCodesCopy");
        if (prop == null)
        {
            EditorUtility.DisplayDialog("Copiar codigos",
                "MisClasesManager no tiene el campo 'btnCodesCopy'. Compilo el script?", "OK");
            return;
        }
        prop.objectReferenceValue = btn;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[CopiarCodigosButtonTool] OK: boton agregado y cableado.");
        EditorUtility.DisplayDialog("Listo",
            "Boton \"Copiar al portapapeles\" agregado al modal de codigos.", "OK");
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform c in parent)
        {
            if (c.name == name) return c;
            var r = FindDeep(c, name);
            if (r) return r;
        }
        return null;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out Color c); return c; }
}
