using UnityEditor;
using UnityEngine;

/// <summary>
/// Los tres interruptores para PROBAR el candado sin depender del backend.
///
/// Hacen falta por una razón concreta: en cuanto el servidor dice que sí una vez, el
/// aparato lo recuerda y ya no vuelve a mostrar la pantalla. Sin una forma de olvidarlo,
/// para volver a verla habría que borrar los datos de la app en el teléfono.
///
/// "Simular servidor cerrado" SOLO FUNCIONA EN EL EDITOR: AppGate lo lee dentro de un
/// #if UNITY_EDITOR, así que no hay manera de que se escape en el APK del taller.
/// </summary>
public static class CandadoTool
{
    [MenuItem("ChemiTech/Candado: ver estado", priority = 200)]
    public static void Status()
    {
        bool open  = PlayerPrefs.GetInt(AppGate.PREF_OPEN, 0) == 1;
        bool force = PlayerPrefs.GetInt(AppGate.PREF_FORCE_LOCK, 0) == 1;

        EditorUtility.DisplayDialog("Candado",
            "Lo que este aparato recuerda:\n\n" +
            (open ? "  • YA SE ABRIÓ alguna vez → el próximo arranque empieza abierto.\n"
                  : "  • Nunca se abrió → el próximo arranque empieza CERRADO.\n") +
            (force ? "  • Simulación de servidor cerrado: ACTIVA (solo en el Editor).\n" : "") +
            "\nOjo: el que manda es el servidor. Esto solo decide cómo arranca mientras " +
            "llega su respuesta.", "OK");
    }

    [MenuItem("ChemiTech/Candado: olvidar estado (arrancar cerrado)", priority = 201)]
    public static void Forget()
    {
        PlayerPrefs.DeleteKey(AppGate.PREF_OPEN);
        PlayerPrefs.Save();
        Debug.Log("[Candado] Estado olvidado: el próximo arranque empieza cerrado.");
        EditorUtility.DisplayDialog("Candado",
            "Olvidado. El próximo arranque empieza cerrado y le preguntará al servidor.\n\n" +
            "Si el servidor dice que sí (o no tiene el endpoint todavía), se abrirá solo " +
            "en menos de 10 segundos.", "OK");
    }

    [MenuItem("ChemiTech/Candado: simular servidor cerrado", priority = 202)]
    public static void ForceLock()
    {
        PlayerPrefs.SetInt(AppGate.PREF_FORCE_LOCK, 1);
        PlayerPrefs.Save();
        EditorUtility.DisplayDialog("Candado",
            "Simulación activada. Al darle a Play verás la pantalla de bloqueo aunque el " +
            "servidor no tenga todavía el endpoint.\n\n" +
            "Solo funciona dentro del Editor: no puede llegar al APK.", "OK");
    }

    [MenuItem("ChemiTech/Candado: quitar simulación", priority = 203)]
    public static void ClearForce()
    {
        PlayerPrefs.DeleteKey(AppGate.PREF_FORCE_LOCK);
        PlayerPrefs.Save();
        EditorUtility.DisplayDialog("Candado", "Simulación quitada. Vuelve a mandar el servidor.", "OK");
    }
}
