using UnityEngine;

/// <summary>
/// Preferencias de audio, guardadas POR DISPOSITIVO (PlayerPrefs global, sin
/// userId). El volumen depende del entorno físico —auriculares, lugar, hora—
/// no de la cuenta, así que se comparte entre invitado y cualquier usuario y
/// sobrevive al cambio de sesión.
///
/// Los valores son 0..100 para coincidir con los sliders de Ajustes; el paso a
/// ganancia real lo hace AudioManager.
/// </summary>
public static class AudioPrefs
{
    const string KEY_MUSIC = "chemitech_audio_music";
    const string KEY_SFX   = "chemitech_audio_sfx";

    // Coinciden con los valores iniciales que pinta SettingsBuilder.
    public const int DEFAULT_MUSIC = 75;
    public const int DEFAULT_SFX   = 60;

    public static int Music
    {
        get => PlayerPrefs.GetInt(KEY_MUSIC, DEFAULT_MUSIC);
        set => PlayerPrefs.SetInt(KEY_MUSIC, Mathf.Clamp(value, 0, 100));
    }

    public static int Sfx
    {
        get => PlayerPrefs.GetInt(KEY_SFX, DEFAULT_SFX);
        set => PlayerPrefs.SetInt(KEY_SFX, Mathf.Clamp(value, 0, 100));
    }

    /// <summary>
    /// Vuelca a disco. Se llama al salir de Ajustes, no en cada frame del
    /// slider: PlayerPrefs.Save() escribe el archivo completo y arrastrar el
    /// control dispararía decenas de escrituras por segundo.
    /// </summary>
    public static void Flush() => PlayerPrefs.Save();
}
