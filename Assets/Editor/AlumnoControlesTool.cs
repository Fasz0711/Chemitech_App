using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Le da al alumno mandos para MOVERSE, no solo para girar.
///
/// La escena del alumno no tenía ninguno: solo se podía arrastrar para rotar la vista, y
/// por eso se sentía clavada. Con la cámara en primera persona eso es aún más limitante,
/// porque girar sin poder avanzar no te acerca a nada.
///
/// Copia el d-pad, las flechas verticales y "Recentrar" del HUD de ZonaJuegoScene (que NO
/// se modifica) y los conecta a la cámara del alumno. Sin colocación: el alumno mira y se
/// mueve, pero no edita nada.
///
/// De paso retira "Ver la vista del docente": la vista se comparte desde el botón del
/// DOCENTE, que es quien decide cuándo todos miran lo mismo.
///
/// Es aditiva y se puede volver a correr.
/// </summary>
public static class AlumnoControlesTool
{
    const string STUDENT_SCENE = "Assets/Scenes/ClaseEstudianteScene.unity";
    const string ZONA_SCENE    = "Assets/Scenes/ZonaJuegoScene.unity";

    // Lo que se trae del HUD de la zona de juego.
    static readonly string[] COPY = { "DPad", "VertPad", "BtnRecentrar" };

    static readonly List<string> problems = new List<string>();

    [MenuItem("ChemiTech/Alumno: mandos de movimiento")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Mandos para el alumno",
                "Añade d-pad, flechas verticales y Recentrar a ClaseEstudianteScene, y " +
                "quita el botón \"Ver la vista del docente\".\n\n" +
                "Copia los mandos desde ZonaJuegoScene (que NO se modifica).\n\n¿Continuar?",
                "Sí, continuar", "Cancelar"))
            return;

        problems.Clear();

        var scene  = EditorSceneManager.OpenScene(STUDENT_SCENE, OpenSceneMode.Single);
        var canvas = FindRoot(scene, "Canvas");
        var camGo  = FindRoot(scene, "Main Camera");
        if (!canvas || !camGo)
        {
            EditorUtility.DisplayDialog("No pude empezar",
                "Falta Canvas o Main Camera en ClaseEstudianteScene.", "OK");
            return;
        }

        var orbit = camGo.GetComponent<OrbitCameraController>();
        if (!orbit) problems.Add("Main Camera no tiene OrbitCameraController.");

        // ── 1) Fuera el botón de la vista del docente ─────────────────────────
        var old = FindDeep(canvas.transform, "BtnVolverVista");
        if (old) Object.DestroyImmediate(old);

        // ── 2) Los mandos, copiados ───────────────────────────────────────────
        var group = EnsureChild(canvas.transform, "MoveControls");
        Stretch(group);
        for (int i = group.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(group.transform.GetChild(i).gameObject);

        if (!CopyFromZona(scene, group.transform)) return;

        // ── 3) Conectados a la cámara, sin colocación ─────────────────────────
        var binder = Ensure<PlacementHudBinder>(EnsureChild(canvas.transform, "StudentControls"));
        var so = new SerializedObject(binder);
        // 'placement' se queda vacío A PROPÓSITO: sin él, el binder manda las flechas
        // directamente a la cámara, que es justo lo que el alumno necesita.
        Set(so, "cam",          orbit);
        Set(so, "btnRecentrar", Comp<Button>(FindDeep(group.transform, "BtnRecentrar")));
        Set(so, "padUp",        Comp<HoldButton>(FindDeep(group.transform, "PadUp")));
        Set(so, "padDown",      Comp<HoldButton>(FindDeep(group.transform, "PadDown")));
        Set(so, "padLeft",      Comp<HoldButton>(FindDeep(group.transform, "PadLeft")));
        Set(so, "padRight",     Comp<HoldButton>(FindDeep(group.transform, "PadRight")));
        Set(so, "vertUp",       Comp<HoldButton>(FindDeep(group.transform, "VertUp")));
        Set(so, "vertDown",     Comp<HoldButton>(FindDeep(group.transform, "VertDown")));
        so.ApplyModifiedProperties();

        // Los avisos y modales, por encima de los mandos.
        Last(canvas.transform, "Notice");
        Last(canvas.transform, "CopyModal");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
        Report();
    }

    static bool CopyFromZona(Scene target, Transform into)
    {
        var zona = EditorSceneManager.OpenScene(ZONA_SCENE, OpenSceneMode.Additive);
        var copies = new List<GameObject>();
        try
        {
            var zonaCanvas = FindRoot(zona, "Canvas");
            var hud = zonaCanvas ? zonaCanvas.transform.Find("HUD") : null;
            if (!hud)
            {
                problems.Add("No encontré Canvas/HUD en ZonaJuegoScene.");
                return false;
            }

            foreach (var name in COPY)
            {
                var src = FindDeep(hud, name);
                if (!src) { problems.Add($"No encontré '{name}' en el HUD de la zona de juego."); continue; }

                var copy = Object.Instantiate(src);
                copy.name = name;
                copy.transform.SetParent(null, false);
                SceneManager.MoveGameObjectToScene(copy, target);
                copies.Add(copy);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(zona, true);   // sin guardar
        }

        foreach (var c in copies) c.transform.SetParent(into, false);
        return copies.Count > 0;
    }

    static void Report()
    {
        if (problems.Count == 0)
        {
            Debug.Log("[AlumnoControles] ✓ El alumno ya puede moverse.");
            EditorUtility.DisplayDialog("¡Listo!",
                "El alumno tiene d-pad, flechas verticales y Recentrar.\n\n" +
                "El d-pad avanza y se desplaza en horizontal; las flechas suben y bajan.\n" +
                "\"Ver la vista del docente\" se quitó: ahora la vista la comparte el " +
                "docente con su botón.", "OK");
            return;
        }

        var msg = string.Join("\n  • ", problems);
        Debug.LogWarning("[AlumnoControles] Quedaron cosas sin resolver:\n  • " + msg);
        EditorUtility.DisplayDialog("Terminó, pero con avisos",
            "Se aplicó lo que se pudo. Sin resolver:\n\n  • " + msg, "OK");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { problems.Add($"El campo '{prop}' no existe en {so.targetObject.GetType().Name}."); return; }
        if (!value)      problems.Add($"'{prop}' se quedó vacío: no encontré a qué apuntar.");
        p.objectReferenceValue = value;
    }

    static T Comp<T>(GameObject go) where T : Component => go ? go.GetComponent<T>() : null;

    static GameObject FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t && t.name == name) return t.gameObject;
        return null;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects()) if (go.name == name) return go;
        return null;
    }

    static GameObject EnsureChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t) return t.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c ? c : go.AddComponent<T>();
    }

    static void Last(Transform canvas, string name)
    {
        var t = canvas.Find(name);
        if (t) t.SetAsLastSibling();
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (!rt) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
