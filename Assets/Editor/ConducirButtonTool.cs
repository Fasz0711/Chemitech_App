using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Herramienta ADITIVA: agrega el botón "Conducir" a la plantilla de tarjeta de
/// MisClasesScene. Se dejó fuera en la Fase 1 a propósito, porque ClaseDocenteScene
/// todavía no existía y un botón muerto es peor que ninguno.
///
/// CLONA un botón que ya está en la tarjeta para heredar estilo y tamaño, y calcula su
/// posición a partir de la separación REAL entre los botones existentes: así sigue
/// cuadrando aunque la tarjeta se haya ajustado a mano.
///
/// Toca la PLANTILLA, no las tarjetas: el manager las clona en runtime.
///
/// Menú: ChemiTech → Add → Conducir Button
/// </summary>
public static class ConducirButtonTool
{
    static readonly Color CONDUCIR = Hex("19A7CE");

    [MenuItem("ChemiTech/Add/Conducir Button")]
    public static void Add()
    {
        var scene  = EditorSceneManager.GetActiveScene();
        var canvas = FindRoot(scene, "Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Conducir Button",
                "Abre primero MisClasesScene (no encontré el Canvas).", "OK");
            return;
        }

        var card = FindDeep(canvas.transform, "CardTemplate");
        if (card == null)
        {
            EditorUtility.DisplayDialog("Conducir Button",
                "No encontré CardTemplate. ¿Es esta MisClasesScene?", "OK");
            return;
        }

        var start = FindDeep(card, "BtnStart");
        var stop  = FindDeep(card, "BtnStop");
        if (start == null || stop == null)
        {
            EditorUtility.DisplayDialog("Conducir Button",
                "La tarjeta no tiene BtnStart y BtnStop; no puedo deducir el estilo.", "OK");
            return;
        }

        // Idempotente: si ya existe se reutiliza.
        var existing = FindDeep(card, "BtnConducir");
        GameObject conducir;
        if (existing != null)
        {
            conducir = existing.gameObject;
        }
        else
        {
            conducir = Object.Instantiate(start.gameObject, card);
            conducir.name = "BtnConducir";
        }

        // Posición: un paso más a la izquierda del botón que ya esté más a la izquierda.
        // El paso se deduce de la distancia real entre los dos botones existentes, así
        // que si alguien reacomodó la tarjeta, esto la sigue.
        var rtStart = start.GetComponent<RectTransform>();
        var rtStop  = stop.GetComponent<RectTransform>();
        var rtNew   = conducir.GetComponent<RectTransform>();

        float step = Mathf.Abs(rtStart.anchoredPosition.x - rtStop.anchoredPosition.x);
        if (step < 1f) step = rtStart.sizeDelta.x + 20f;   // si estaban superpuestos

        float leftmost = Mathf.Min(rtStart.anchoredPosition.x, rtStop.anchoredPosition.x);
        rtNew.anchoredPosition = new Vector2(leftmost - step, rtStart.anchoredPosition.y);

        // Texto y color propios
        var label = conducir.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label) label.text = "Conducir";
        var img = conducir.GetComponent<Image>();
        if (img) img.color = CONDUCIR;

        // El clon arrastra el onClick que tuviera el original en el inspector; el manager
        // registra el suyo en runtime al clonar la tarjeta.
        var btn = conducir.GetComponent<Button>();
        if (btn) btn.onClick = new Button.ButtonClickedEvent();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[ConducirButtonTool] ✓ Botón Conducir agregado a la plantilla de tarjeta.");
        EditorUtility.DisplayDialog("¡Listo!",
            "Botón \"Conducir\" agregado a CardTemplate.\n\n" +
            "Solo lo ve el docente, y solo en clases que no hayan terminado.\n" +
            "Revisa que quepa en la tarjeta y muévelo si hace falta.", "OK");
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
