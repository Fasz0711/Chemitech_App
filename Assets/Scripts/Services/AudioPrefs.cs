using UnityEngine;

/// <summary>
/// Preferencias de audio, guardadas POR CUENTA (ver AccountPrefs): cada usuario
/// recuerda su volumen en este dispositivo y el invitado siempre arranca en los
/// valores por defecto.
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
        get => AccountPrefs.GetInt(KEY_MUSIC, DEFAULT_MUSIC);
        set => AccountPrefs.SetInt(KEY_MUSIC, Mathf.Clamp(value, 0, 100));
    }

    public static int Sfx
    {
        get => AccountPrefs.GetInt(KEY_SFX, DEFAULT_SFX);
        set => AccountPrefs.SetInt(KEY_SFX, Mathf.Clamp(value, 0, 100));
    }
}
