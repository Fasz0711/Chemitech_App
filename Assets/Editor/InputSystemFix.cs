using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Reemplaza StandaloneInputModule por InputSystemUIInputModule en la escena activa.
/// Menú: ChemiTech/Fix/Input System
/// </summary>
public static class InputSystemFix
{
    [MenuItem("ChemiTech/Fix/Input System")]
    static void Fix()
    {
#if ENABLE_INPUT_SYSTEM
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            EditorUtility.DisplayDialog("Error",
                "No se encontró EventSystem en la escena activa.", "OK");
            return;
        }

        var old = es.GetComponent<StandaloneInputModule>();
        if (old != null)
            Object.DestroyImmediate(old);

        if (es.GetComponent<InputSystemUIInputModule>() == null)
            es.gameObject.AddComponent<InputSystemUIInputModule>();

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[InputSystemFix] ✓ EventSystem actualizado a InputSystemUIInputModule.");
        EditorUtility.DisplayDialog("¡Listo!",
            "EventSystem actualizado.\nGuarda con Ctrl+S y repite para cada escena.", "OK");
#else
        EditorUtility.DisplayDialog("Aviso",
            "Input System package no detectado.\n" +
            "Ve a Edit → Project Settings → Player → Active Input Handling → Both.",
            "OK");
#endif
    }
}
