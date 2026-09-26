using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Le da a las dos escenas de clase la misma ambientación 3D que la zona de juego:
/// plataforma con rejilla, postes en las esquinas y cielo estrellado.
///
/// No es solo decoración. La rejilla es la referencia contra la que se lee la marca de
/// suelo al colocar un átomo —sobre un plano liso esa marca no se compara con nada— y
/// los postes dan profundidad al girar la cámara. En primera persona, sin nada fijo
/// alrededor, es fácil perder la noción de dónde estás.
///
/// LA GEOMETRÍA se crea aquí (es contenido de escena). LA REJILLA Y EL CIELO los genera
/// ZoneEnvironment en tiempo de ejecución, y por eso hace falta ZoneAmbience: en la zona
/// de juego los instancia ZonaJuegoManager, que las escenas de clase no tienen.
///
/// Es aditiva y se puede volver a correr. Abre las dos escenas, así que guarda antes lo
/// que tengas a medias.
/// </summary>
public static class AmbienteClaseTool
{
    const string TEACHER = "Assets/Scenes/ClaseDocenteScene.unity";
    const string STUDENT = "Assets/Scenes/ClaseEstudianteScene.unity";

    // Los mismos valores que la zona de juego: el docente construye en el mismo espacio
    // que el alumno juega, y que midan igual evita explicar dos escalas distintas.
    const float PLATFORM_SCALE = 2.6f;   // Plane de 10 → 26 unidades de lado
    const float POST_AT        = 10f;
    const float PLATFORM_HALF  = 11.5f;  // zona donde se puede colocar, dentro del plano
    const float MAX_HEIGHT     = 12f;

    static readonly List<string> problems = new List<string>();

    [MenuItem("ChemiTech/Clases: ambientación 3D (plataforma y cielo)")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("Ambientación 3D en las clases",
                "Añade plataforma con rejilla, postes y cielo estrellado a " +
                "ClaseDocenteScene y ClaseEstudianteScene.\n\n" +
                "Abre las dos escenas: guarda antes lo que tengas sin guardar.\n\n¿Continuar?",
                "Sí, continuar", "Cancelar"))
            return;

        problems.Clear();
        int done = 0;

        foreach (var path in new[] { TEACHER, STUDENT })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (BuildInto(scene)) done++;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.Refresh();
        Report(done);
    }

    static bool BuildInto(Scene scene)
    {
        var platMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Platform.mat");
        var postMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Post.mat");
        if (!platMat) problems.Add("Falta Assets/Materials/Zone_Platform.mat");
        if (!postMat) problems.Add("Falta Assets/Materials/Zone_Post.mat");

        var area = FindRoot(scene, "PlayArea");
        if (!area)
        {
            area = new GameObject("PlayArea");
            SceneManager.MoveGameObjectToScene(area, scene);
        }
        area.transform.position = Vector3.zero;

        // Se rehace el contenido para que volver a correr la herramienta no acumule
        // plataformas ni deje postes de una versión anterior.
        for (int i = area.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(area.transform.GetChild(i).gameObject);

        var plat = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plat.name = "Platform";   // ZoneEnvironment lo busca POR ESTE NOMBRE
        plat.transform.SetParent(area.transform, false);
        plat.transform.localScale = new Vector3(PLATFORM_SCALE, 1f, PLATFORM_SCALE);
        if (platMat) plat.GetComponent<Renderer>().sharedMaterial = platMat;

        Vector2[] corners = { new Vector2(-POST_AT, -POST_AT), new Vector2(POST_AT, -POST_AT),
                              new Vector2(-POST_AT,  POST_AT), new Vector2(POST_AT,  POST_AT) };
        for (int i = 0; i < corners.Length; i++)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = $"Post_{i}";
            post.transform.SetParent(area.transform, false);
            post.transform.localScale    = new Vector3(0.5f, 2.2f, 0.5f);
            post.transform.localPosition = new Vector3(corners[i].x, 1.1f, corners[i].y);
            if (postMat) post.GetComponent<Renderer>().sharedMaterial = postMat;
        }

        // Sin detección no hay enlaces que celebrar: los pulsos solo aplican al docente.
        bool hasDetection = FindInScene<BondManager>(scene) != null;
        var  amb = area.GetComponent<ZoneAmbience>();
        if (!amb) amb = area.AddComponent<ZoneAmbience>();
        var so = new SerializedObject(amb);
        var p  = so.FindProperty("effects");
        if (p != null) p.boolValue = hasDetection;
        so.ApplyModifiedProperties();

        // La zona en la que se puede colocar se ajusta a la plataforma que ahora SE VE.
        // Estaba en 24 de cuando el contrato exigía ångströms reales y la lección del
        // puente de hidrógeno parecía necesitar 20 Å de separación; con la escala ya
        // relativa, esa separación son ~3 unidades y sobra sitio.
        var place = FindInScene<AtomPlacementController>(scene);
        if (place)
        {
            var pso = new SerializedObject(place);
            SetFloat(pso, "platformHalf", PLATFORM_HALF);
            SetFloat(pso, "maxHeight",    MAX_HEIGHT);
            pso.ApplyModifiedProperties();
        }

        return true;
    }

    static void SetFloat(SerializedObject so, string prop, float value)
    {
        var p = so.FindProperty(prop);
        if (p == null) { problems.Add($"El campo '{prop}' no existe en {so.targetObject.GetType().Name}."); return; }
        p.floatValue = value;
    }

    /// <summary>Busca en la escena INCLUYENDO objetos desactivados: el HUD creativo del
    /// docente vive apagado hasta que se cambia de modo, y FindObjectOfType no lo vería.</summary>
    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found) return found;
        }
        return null;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects()) if (go.name == name) return go;
        return null;
    }

    static void Report(int done)
    {
        if (problems.Count == 0)
        {
            Debug.Log($"[AmbienteClase] ✓ Ambientación puesta en {done} escena(s).");
            EditorUtility.DisplayDialog("¡Listo!",
                "Las dos escenas de clase ya tienen plataforma, postes y cielo.\n\n" +
                "La rejilla y las estrellas se generan al DAR A PLAY, no se ven en el " +
                "editor: las dibuja ZoneEnvironment en tiempo de ejecución.", "OK");
            return;
        }

        var msg = string.Join("\n  • ", problems);
        Debug.LogWarning("[AmbienteClase] Quedaron cosas sin resolver:\n  • " + msg);
        EditorUtility.DisplayDialog("Terminó, pero con avisos",
            "Se aplicó lo que se pudo. Sin resolver:\n\n  • " + msg, "OK");
    }
}
