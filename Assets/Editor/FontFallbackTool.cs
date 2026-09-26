using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// Le pone a Fredoka una fuente de reserva para los caracteres que no tiene.
///
/// EL PROBLEMA: Fredoka-Medium mapea 320 glifos y su tabla de reserva estaba VACÍA.
/// Un carácter que no tenga —δ, por ejemplo— no se dibuja: sale un hueco, sin aviso ni
/// error. Este proyecto ya pagó esa lección con los emojis de los iconos.
///
/// LA SOLUCIÓN: TextMesh Pro ya trae "LiberationSans SDF - Fallback" con el modo de
/// atlas DINÁMICO, o sea que genera los glifos bajo demanda desde el TTF en vez de
/// tener un atlas fijo. LiberationSans.ttf mapea 2294 glifos e incluye griego, así que
/// δ sale de ahí sin crear ningún asset.
///
/// LO QUE ESTO NO ARREGLA: los subíndices (₂) no están en NINGUNA de las dos fuentes,
/// así que las fórmulas siguen en ASCII ("H2O"). Si algún día se quieren de verdad, el
/// camino es la etiqueta &lt;sub&gt; de TMP, que no necesita el carácter.
///
/// OJO: la reserva es GLOBAL A LA FUENTE. Afecta a toda la app, no solo a las capas.
/// Es seguro —solo actúa donde hoy no se dibuja nada— pero conviene saberlo.
/// </summary>
public static class FontFallbackTool
{
    const string FREDOKA  = "Assets/Fonts/Fredoka-Medium SDF.asset";
    const string FALLBACK = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";

    [MenuItem("ChemiTech/Fuente: reserva para δ y otros glifos")]
    public static void Apply()
    {
        var fredoka  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FREDOKA);
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FALLBACK);

        if (!fredoka || !fallback)
        {
            EditorUtility.DisplayDialog("No pude aplicarlo",
                (!fredoka  ? "No encontré " + FREDOKA  + "\n" : "") +
                (!fallback ? "No encontré " + FALLBACK : ""), "OK");
            return;
        }

        var so   = new SerializedObject(fredoka);
        var list = so.FindProperty("m_FallbackFontAssetTable");
        if (list == null)
        {
            EditorUtility.DisplayDialog("No pude aplicarlo",
                "Esta versión de TextMesh Pro no tiene 'm_FallbackFontAssetTable'.", "OK");
            return;
        }

        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == fallback)
            {
                EditorUtility.DisplayDialog("Ya estaba puesto",
                    "Fredoka ya tiene LiberationSans como reserva. No se cambió nada.", "OK");
                return;
            }

        int at = list.arraySize;
        list.arraySize = at + 1;
        list.GetArrayElementAtIndex(at).objectReferenceValue = fallback;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(fredoka);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FontFallback] ✓ Fredoka ya tiene {list.arraySize} fuente(s) de reserva. " +
                  "δ, δ+ y δ− pasan a dibujarse.");
        EditorUtility.DisplayDialog("¡Listo!",
            "Fredoka ya tiene reserva.\n\n" +
            "Para comprobarlo: pon \"δ+ δ−\" en cualquier texto de una escena y dale a Play. " +
            "Antes salían huecos.\n\n" +
            "Los subíndices (H₂O) SIGUEN sin funcionar: ese carácter no está en ninguna de " +
            "las dos fuentes. Las fórmulas se quedan en ASCII.", "OK");
    }
}
