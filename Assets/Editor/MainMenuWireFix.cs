using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Cablea los botones del MainMenuManager con los GameObjects de la escena.
/// Menú: ChemiTech/Fix/Wire Main Menu
/// </summary>
public static class MainMenuWireFix
{
    [MenuItem("ChemiTech/Fix/Wire Main Menu")]
    static void Fix()
    {
        var mgr = Object.FindFirstObjectByType<MainMenuManager>();
        if (mgr == null)
        {
            EditorUtility.DisplayDialog("Error",
                "No se encontró MainMenuManager en la escena.\nAbre SampleScene primero.", "OK");
            return;
        }

        var so = new SerializedObject(mgr);
        so.FindProperty("btnJugar").objectReferenceValue        = FindBtn("BtnJugar");
        so.FindProperty("btnDiario").objectReferenceValue       = FindBtn("BtnDiario");
        so.FindProperty("btnAjustes").objectReferenceValue      = FindBtn("BtnAjustes");
        so.FindProperty("btnIniciarSesion").objectReferenceValue = FindBtn("BtnIniciarSesion");
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        string ok(Button b) => b != null ? "✓" : "✗ NO ENCONTRADO";
        EditorUtility.DisplayDialog("Wire Main Menu",
            $"btnJugar:        {ok(FindBtn("BtnJugar"))}\n" +
            $"btnDiario:       {ok(FindBtn("BtnDiario"))}\n" +
            $"btnAjustes:      {ok(FindBtn("BtnAjustes"))}\n" +
            $"btnIniciarSesion:{ok(FindBtn("BtnIniciarSesion"))}\n\n" +
            "Guarda con Ctrl+S.", "OK");
    }

    static Button FindBtn(string name)
    {
        var go = GameObject.Find(name);
        return go ? go.GetComponent<Button>() : null;
    }
}
