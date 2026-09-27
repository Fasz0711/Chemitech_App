using UnityEngine;

/// <summary>
/// Brillo de la pantalla, guardado POR CUENTA igual que el volumen: cada usuario recuerda
/// el suyo en este dispositivo y el invitado arranca en el valor por defecto.
///
/// 0..100 para coincidir con el slider de Ajustes; el paso a exposición real lo hace
/// GraphicsManager. 50 es NEUTRO —ni más claro ni más oscuro que sin ajuste— para que el
/// centro del slider sea el aspecto que el juego tiene diseñado.
/// </summary>
public static class BrightnessPrefs
{
    const string KEY = "chemitech_video_brightness";

    public const int DEFAULT = 50;

    public static int Value
    {
        get => AccountPrefs.GetInt(KEY, DEFAULT);
        set => AccountPrefs.SetInt(KEY, Mathf.Clamp(value, 0, 100));
    }
}
