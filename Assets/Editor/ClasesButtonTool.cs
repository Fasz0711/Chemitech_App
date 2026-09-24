using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Herramienta ADITIVA: agrega el botón "Clases" al menú principal.
///
/// CLONA BtnAjustes en vez de construir uno nuevo, para heredar estilo, tamaño, sprites
/// y efectos sin tener que replicar el layout del menú (que está en coordenadas de Figma
/// convertidas). Su posición sale del espaciado REAL entre Diario y Ajustes, así queda
/// alineado aunque alguien haya movido la columna a mano.
///
/// Menú: ChemiTech → Add → Clases Button
/// </summary>
public static class ClasesButtonTool
{
    const string ICON_SPRITE = "Assets/Sprites/person-icon.png";

    [MenuItem("ChemiTech/Add/Clases Button")]
    public static void Add()
    {
        var scene  = EditorSceneManager.GetActiveScene();
        var canvas = FindRoot(scene, "Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Clases Button",
                "Abre primero SampleScene (no encontré el Canvas).", "OK");
            return;
        }

        var ajustes = FindDeep(canvas.transform, "BtnAjustes");
        var diario  = FindDeep(canvas.transform, "BtnDiario");
        if (ajustes == null || diario == null)
        {
            EditorUtility.DisplayDialog("Clases Button",
                "No encontré BtnAjustes y BtnDiario. ¿Es esta SampleScene?", "OK");
            return;
        }

        // Idempotente: si ya existe se reutiliza, no se duplica.
        var existing = FindDeep(canvas.transform, "BtnClases");
        GameObject clases;
        if (existing != null)
        {
            clases = existing.gameObject;
        }
        else
        {
            clases = Object.Instantiate(ajustes.gameObject, ajustes.parent);
            clases.name = "BtnClases";
            clases.transform.SetSiblingIndex(ajustes.GetSiblingIndex() + 1);
        }

        // Posición: un "paso" por debajo de Ajustes, usando la separación real
        // Diario → Ajustes. Si la columna se movió a mano, sigue cuadrando.
        var rtA = ajustes.GetComponent<RectTransform>();
        var rtD = diario.GetComponent<RectTransform>();
        var rtC = clases.GetComponent<RectTransform>();
        Vector2 step = rtA.anchoredPosition - rtD.anchoredPosition;
        rtC.anchoredPosition = rtA.anchoredPosition + step;

        // Texto y icono propios
        SetLabel(clases, "Clases");
        SetIcon(clases, AssetDatabase.LoadAssetAtPath<Sprite>(ICON_SPRITE));

        // El clon arrastra el onClick que tuviera Ajustes en el inspector; el manager
        // registra el suyo en runtime, así que aquí se limpia para no navegar dos veces.
        var btn = clases.GetComponent<Button>();
        if (btn) btn.onClick = new Button.ButtonClickedEvent();

        // Cableado en el manager
        var mgr = FindManager(canvas.transform);
        if (mgr == null)
        {
            EditorUtility.DisplayDialog("Clases Button",
                "No encontré MainMenuManager en la escena; el botón quedó sin cablear.", "OK");
            return;
        }

        var so = new SerializedObject(mgr);
        var prop = so.FindProperty("btnClases");
        if (prop == null)
        {
            EditorUtility.DisplayDialog("Clases Button",
                "MainMenuManager no tiene el campo 'btnClases'. ¿Compiló el script?", "OK");
            return;
        }
        prop.objectReferenceValue = btn;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[ClasesButtonTool] ✓ Botón Clases agregado y cableado.");
        EditorUtility.DisplayDialog("¡Listo!",
            "Botón \"Clases\" agregado al menú.\n\n" +
            "Se muestra solo con sesión iniciada; el invitado no lo ve.\n" +
            "Revisa que quepa en pantalla y muévelo si hace falta.", "OK");
    }

    static void SetLabel(GameObject root, string text)
    {
        var tmp = root.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp) tmp.text = text;
    }

    static void SetIcon(GameObject root, Sprite sprite)
    {
        if (sprite == null) return;
        var icon = FindDeep(root.transform, "Icon");
        var img  = icon ? icon.GetComponent<Image>() : null;
        if (img) img.sprite = sprite;
    }

    static MainMenuManager FindManager(Transform canvas)
    {
        var mgr = canvas.GetComponentInChildren<MainMenuManager>(true);
        if (mgr) return mgr;
        return Object.FindFirstObjectByType<MainMenuManager>();
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
}
