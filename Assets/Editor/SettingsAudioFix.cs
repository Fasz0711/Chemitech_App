using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Corrección ADITIVA de la pestaña Audio de SettingsScene.
///
/// El builder etiquetaba el segundo slider de Audio como "Efectos Visuales"
/// (copia del row de Gráficos); en Audio corresponde "Efectos de sonido".
/// SettingsBuilder ya quedó corregido, pero la escena guardada conserva el
/// texto viejo y no se puede regenerar sin perder los ajustes manuales, así
/// que este comando solo renombra el texto en la escena abierta.
///
/// Menú: ChemiTech → Fix → Settings Audio Labels
/// </summary>
public static class SettingsAudioFix
{
    [MenuItem("ChemiTech/Fix/Settings Audio Labels")]
    static void FixAudioLabels()
    {
        var canvas = GameObject.Find("Canvas");
        var pAudio = canvas != null ? FindChildRecursive(canvas.transform, "PanelAudio") : null;
        if (pAudio == null)
        {
            EditorUtility.DisplayDialog("Error",
                "No se encontró 'PanelAudio' (¿abriste SettingsScene?).", "OK");
            return;
        }

        var label = FindChildRecursive(pAudio, "EfectosLabel")?.GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            EditorUtility.DisplayDialog("Error",
                "No se encontró el texto 'EfectosLabel' dentro de PanelAudio.", "OK");
            return;
        }

        if (label.text == "Efectos de sonido")
        {
            EditorUtility.DisplayDialog("Sin cambios",
                "La etiqueta ya dice \"Efectos de sonido\".", "OK");
            return;
        }

        Undo.RecordObject(label, "Fix etiqueta de audio");
        label.text = "Efectos de sonido";

        EditorUtility.SetDirty(label);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[SettingsAudioFix] ✓ Etiqueta corregida: \"Efectos de sonido\".");
        EditorUtility.DisplayDialog("¡Listo!",
            "La pestaña Audio ahora dice \"Efectos de sonido\".\nGuarda con Ctrl+S.", "OK");
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var r = FindChildRecursive(c, name);
            if (r) return r;
        }
        return null;
    }
}
