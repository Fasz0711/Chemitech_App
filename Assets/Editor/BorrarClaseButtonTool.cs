using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Herramienta ADITIVA: agrega el botón "Borrar" a la plantilla de tarjeta de
/// MisClasesScene, dentro de ActionsRow.
///
/// No calcula posiciones: ActionsRow tiene un HorizontalLayoutGroup que ignora a los
/// hijos inactivos, así que basta con ser hijo suyo y el reparto se recalcula solo según
/// qué botones muestre el manager para cada rol y estado.
///
/// Menú: ChemiTech → Add → Borrar Clase Button
/// </summary>
public static class BorrarClaseButtonTool
{
    static readonly Color DANGER = Hex("C0392B");

    [MenuItem("ChemiTech/Add/Borrar Clase Button")]
    public static void Add()
    {
        var scene  = EditorSceneManager.GetActiveScene();
        var canvas = FindRoot(scene, "Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Borrar Clase", "Abre primero MisClasesScene.", "OK");
            return;
        }

        var card = FindDeep(canvas.transform, "CardTemplate");
        var row  = card ? FindDeep(card, "ActionsRow") : null;
        if (row == null)
        {
            EditorUtility.DisplayDialog("Borrar Clase",
                "No encontré CardTemplate/ActionsRow.\n\nCorre antes: ChemiTech > Add > Card Actions Row.", "OK");
            return;
        }

        var model = FindDeep(card, "BtnStop");
        if (model == null)
        {
            EditorUtility.DisplayDialog("Borrar Clase", "No encontré BtnStop para copiar su estilo.", "OK");
            return;
        }

        var existing = FindDeep(card, "BtnBorrar");
        GameObject borrar;
        if (existing != null) borrar = existing.gameObject;
        else
        {
            borrar = Object.Instantiate(model.gameObject, row);
            borrar.name = "BtnBorrar";
        }

        if (borrar.transform.parent != row) borrar.transform.SetParent(row, false);
        borrar.transform.SetAsLastSibling();

        var label = borrar.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label) label.text = "Borrar";
        var img = borrar.GetComponent<Image>();
        if (img) img.color = DANGER;

        // El clon arrastra el onClick del original; el manager registra el suyo al clonar
        // la tarjeta en runtime.
        var btn = borrar.GetComponent<Button>();
        if (btn) btn.onClick = new Button.ButtonClickedEvent();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[BorrarClaseButtonTool] OK: boton Borrar agregado a ActionsRow.");
        EditorUtility.DisplayDialog("Listo",
            "Boton \"Borrar\" agregado.\n\n" +
            "Solo lo ve el docente, y solo en clases que NO esten en curso.", "OK");
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
