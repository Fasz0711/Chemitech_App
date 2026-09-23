using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Herramienta ADITIVA: deja el campo de acceso de LoginScene listo para que los alumnos
/// entren con su código (3A_07), no solo con un correo.
///
/// El bloqueo real no era el texto sino el ContentType: EmailAddress activa la validación
/// de caracteres de TMP y el teclado de correo del móvil. Se pasa a Standard para que el
/// campo acepte cualquier cosa; quién es válido lo decide el servidor, no el teclado.
///
/// Menú: ChemiTech → Fix → Login por código
/// </summary>
public static class LoginCodeFieldTool
{
    const string LABEL_TEXT  = "Correo o código";
    const string PLACEHOLDER = "correo@ejemplo.com  o  3A_07";

    [MenuItem("ChemiTech/Fix/Login por código")]
    public static void Fix()
    {
        var scene = EditorSceneManager.GetActiveScene();

        var label = FindDeepInScene(scene, "LabelEmail")?.GetComponent<TextMeshProUGUI>();
        var field = FindDeepInScene(scene, "EmailField")?.GetComponent<TMP_InputField>();

        if (label == null && field == null)
        {
            EditorUtility.DisplayDialog("Login por código",
                "No encontré LabelEmail ni EmailField. ¿Es esta LoginScene?", "OK");
            return;
        }

        if (label) label.text = LABEL_TEXT;

        if (field)
        {
            // Standard: sin validación de caracteres y con teclado normal.
            field.contentType = TMP_InputField.ContentType.Standard;
            if (field.placeholder is TextMeshProUGUI ph) ph.text = PLACEHOLDER;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[LoginCodeFieldTool] ✓ Campo de acceso listo para correo o código.");
        EditorUtility.DisplayDialog("¡Listo!",
            $"Etiqueta: \"{LABEL_TEXT}\"\nContentType: Standard (acepta 3A_07)", "OK");
    }

    static Transform FindDeepInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;
            var found = FindDeep(root.transform, name);
            if (found) return found;
        }
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
