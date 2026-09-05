using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Herramienta ADITIVA: agrega un slider de zoom vertical en el lado izquierdo de la HUD,
/// junto a un pequeño icono de lupa. El slider controla la distancia de la cámara (6–34).
/// Menú: ChemiTech → Add → Zoom Slider (ZonaJuego)
/// </summary>
public static class ZoomSliderTool
{
    [MenuItem("ChemiTech/Add/Zoom Slider (ZonaJuego)")]
    public static void AddZoomSlider()
    {
        var scene  = EditorSceneManager.GetActiveScene();
        var canvas = FindRootWith<Canvas>(scene, "Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Zoom Slider",
                "Abre primero ZonaJuegoScene (no encontré el Canvas en la escena activa).", "OK");
            return;
        }

        var hud = FindDeep(canvas.transform, "HUD");
        if (hud == null)
        {
            EditorUtility.DisplayDialog("Zoom Slider",
                "No encontré el HUD en la escena. ¿Es esta ZonaJuegoScene?", "OK");
            return;
        }

        var mgrGo = FindDeep(canvas.transform, "ZonaJuegoManager");
        if (mgrGo == null)
        {
            EditorUtility.DisplayDialog("Zoom Slider",
                "No encontré ZonaJuegoManager en el Canvas. ¿Es esta ZonaJuegoScene?", "OK");
            return;
        }
        var mgr = mgrGo.GetComponent<ZonaJuegoManager>();
        if (mgr == null)
        {
            EditorUtility.DisplayDialog("Zoom Slider",
                "ZonaJuegoManager no tiene el componente. ¿Es esta ZonaJuegoScene?", "OK");
            return;
        }

        // Main Camera está en los roots de la escena, no dentro del Canvas
        var camGo = FindRootGameObject(scene, "Main Camera");
        if (camGo == null)
        {
            EditorUtility.DisplayDialog("Zoom Slider",
                "No encontré Main Camera en la escena. ¿Es esta ZonaJuegoScene?", "OK");
            return;
        }
        var camCtrl = camGo.GetComponent<OrbitCameraController>();
        if (camCtrl == null)
        {
            EditorUtility.DisplayDialog("Zoom Slider",
                "Main Camera no tiene OrbitCameraController. ¿Es esta ZonaJuegoScene?", "OK");
            return;
        }

        // Busca/crea el grupo ZoomSlider (idempotente).
        var zoomGroupT = DirectChild(hud.transform, "ZoomSlider");
        GameObject zoomGroup;
        if (zoomGroupT != null)
        {
            zoomGroup = zoomGroupT.gameObject;
            Debug.Log("[ZoomSliderTool] ZoomSlider ya existe. Reemplazando...");
            Object.DestroyImmediate(zoomGroup);
        }

        zoomGroup = new GameObject("ZoomSlider", typeof(RectTransform));
        zoomGroup.transform.SetParent(hud.transform, false);
        var zoomRT = zoomGroup.GetComponent<RectTransform>();
        zoomRT.anchorMin = new Vector2(0f, 0.5f);
        zoomRT.anchorMax = new Vector2(0f, 0.5f);
        zoomRT.pivot = new Vector2(0.5f, 0.5f);
        zoomRT.anchoredPosition = new Vector2(44f, -60f);  // debajo del D-pad
        zoomRT.sizeDelta = new Vector2(60f, 200f);

        // ── Slider (con background + fill + handle) ───────────────────────────
        var sliderGo = new GameObject("Slider", typeof(RectTransform));
        sliderGo.transform.SetParent(zoomGroup.transform, false);
        var sliderRT = sliderGo.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0.5f, 0f);
        sliderRT.anchorMax = new Vector2(0.5f, 1f);
        sliderRT.pivot = new Vector2(0.5f, 0.5f);
        sliderRT.offsetMin = Vector2.zero;
        sliderRT.offsetMax = Vector2.zero;

        // Background
        var bgGo = new GameObject("Background", typeof(RectTransform));
        bgGo.transform.SetParent(sliderGo.transform, false);
        var bgRT = bgGo.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.18f, 0.18f, 0.22f, 0.7f);
        bgImg.raycastTarget = true;

        // Fill rect
        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(sliderGo.transform, false);
        var fillRT = fillGo.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0.5f, 0f);
        fillRT.anchorMax = new Vector2(0.5f, 0f);
        fillRT.pivot = new Vector2(0.5f, 0f);
        fillRT.sizeDelta = new Vector2(8f, 0f);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Login/rounded-panel.png");
        fillImg.type = Image.Type.Sliced;
        fillImg.color = Hex("B9A7F0");
        fillImg.raycastTarget = false;

        // Handle
        var handleGo = new GameObject("Handle", typeof(RectTransform));
        handleGo.transform.SetParent(sliderGo.transform, false);
        var handleRT = handleGo.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(16f, 16f);
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/AtomCircle.png");
        handleImg.color = Hex("B9A7F0");
        handleImg.raycastTarget = true;

        // Slider component
        var slider = sliderGo.AddComponent<Slider>();
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.direction = Slider.Direction.BottomToTop;
        slider.minValue = 6f;
        slider.maxValue = 34f;
        slider.value = 16f;
        slider.wholeNumbers = false;
        slider.interactable = true;

        // ── Icono de zoom (lupa, arriba) ───────────────────────────────────────
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(zoomGroup.transform, false);
        var iconRT = iconGo.GetComponent<RectTransform>();
        iconRT.anchorMin = iconRT.anchorMax = iconRT.pivot = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = new Vector2(0f, 100f);
        iconRT.sizeDelta = new Vector2(20f, 20f);
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/gear-icon.png");
        iconImg.color = Color.white;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // ── Conectar slider a cámara ───────────────────────────────────────────
        slider.onValueChanged.AddListener(value =>
        {
            Debug.Log($"[ZoomSlider] valor cambió a {value}");
            camCtrl.SetZoomDistance(value);
        });

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[ZoomSliderTool] ✓ Zoom slider agregado (lado izquierdo, debajo del D-pad).");
        EditorUtility.DisplayDialog("¡Listo!",
            "Zoom slider agregado al lado izquierdo (debajo del D-pad).\n\nRango: 6–34 (distancia de cámara).", "OK");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    static GameObject FindRootGameObject(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static T FindRootWith<T>(Scene scene, string preferName) where T : Component
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == preferName && go.GetComponent<T>() != null) return go.GetComponent<T>();
        foreach (var go in scene.GetRootGameObjects())
        {
            var c = go.GetComponent<T>(); if (c) return c;
        }
        return null;
    }

    static Transform DirectChild(Transform parent, string name)
    {
        foreach (Transform c in parent) if (c.name == name) return c;
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

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
}
