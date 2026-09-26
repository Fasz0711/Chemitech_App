using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Cambia la retícula de colocación por una CRUZ.
///
/// Antes era un círculo con un punto dentro, y ese punto tapaba justo lo que hay que
/// mirar: el sitio exacto donde va a caer el átomo. La cruz se abre en el centro, así
/// que marca el punto sin cubrirlo.
///
/// Trabaja sobre LA ESCENA ABIERTA y sobre todas las retículas que encuentre. Hay que
/// correrla en ZonaJuegoScene y en ClaseDocenteScene (cada una tiene la suya, porque la
/// de la pizarra es una copia).
/// </summary>
public static class ReticleCrossTool
{
    const string RETICLE = "PlacementReticle";

    // Un brazo de 14 px con 5 px de hueco en el centro: la cruz señala el punto y lo
    // deja ver. Con los brazos pegados volveríamos a tapar el centro, que es el defecto
    // que tenía el punto.
    const float ARM       = 14f;
    const float THICK     = 2.5f;
    const float GAP       = 5f;
    const float RETICLE_W = 2f * (GAP + ARM);

    static readonly Color INK = new Color(1f, 1f, 1f, 0.92f);

    [MenuItem("ChemiTech/Retícula: cruz (escena abierta)")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        int done = 0;

        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == RETICLE) { Rebuild(t.gameObject); done++; }

        if (done == 0)
        {
            EditorUtility.DisplayDialog("No encontré la retícula",
                $"No hay ningún '{RETICLE}' en {scene.name}.\n\n" +
                "Ábrela en ZonaJuegoScene o en ClaseDocenteScene.", "OK");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[ReticleCross] ✓ {done} retícula(s) convertidas a cruz en {scene.name}.");
        EditorUtility.DisplayDialog("¡Listo!",
            $"{done} retícula(s) ahora son una cruz en {scene.name}.\n\n" +
            "Acuérdate de correrlo también en la otra escena.", "OK");
    }

    static void Rebuild(GameObject reticle)
    {
        // Fuera el círculo y el punto. Se borran todos los hijos y no solo los que
        // conocemos: si alguien añadió algo, lo que queda es una cruz limpia.
        for (int i = reticle.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(reticle.transform.GetChild(i).gameObject);

        var rt = reticle.GetComponent<RectTransform>();
        if (rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(RETICLE_W, RETICLE_W);
        }

        float off = GAP + ARM * 0.5f;
        Arm(reticle.transform, "Up",    new Vector2(0f,  off), new Vector2(THICK, ARM));
        Arm(reticle.transform, "Down",  new Vector2(0f, -off), new Vector2(THICK, ARM));
        Arm(reticle.transform, "Left",  new Vector2(-off, 0f), new Vector2(ARM, THICK));
        Arm(reticle.transform, "Right", new Vector2( off, 0f), new Vector2(ARM, THICK));
    }

    static void Arm(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        // Sin sprite: una Image sin sprite pinta un rectángulo blanco, que es justo lo
        // que hace falta y no depende de ningún asset.
        var img = go.AddComponent<Image>();
        img.color = INK;
        img.raycastTarget = false;
    }
}
