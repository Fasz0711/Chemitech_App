using System;
using UnityEngine;

/// <summary>
/// Síntesis de audio por código: genera los AudioClip del juego sin necesitar
/// archivos de sonido en el proyecto. Los clips se construyen una sola vez al
/// arrancar (AudioManager) y quedan en memoria.
///
/// Todo se sintetiza mono a 44.1 kHz. El loop ambiental está afinado para que
/// todas sus frecuencias completen un número entero de ciclos dentro de los 8s,
/// así el bucle no produce un chasquido al repetirse.
/// </summary>
public static class ProceduralAudio
{
    const int   SR  = 44100;
    const float TAU = Mathf.PI * 2f;

    // ── Bloques de síntesis ───────────────────────────────────────────────────

    /// <summary>Envolvente percusiva: ataque lineal corto + caída exponencial.</summary>
    static float Env(float t, float attack, float decayTau)
    {
        if (t < 0f) return 0f;
        float a = (attack <= 0f) ? 1f : Mathf.Clamp01(t / attack);
        return a * Mathf.Exp(-t / decayTau);
    }

    /// <summary>
    /// Fase de un barrido lineal de frecuencia f0→f1. Hay que integrar la
    /// frecuencia; usar sin(2π·f(t)·t) directamente desafina el barrido.
    /// </summary>
    static float SweepPhase(float t, float f0, float f1, float dur)
        => TAU * (f0 * t + (f1 - f0) * t * t / (2f * dur));

    /// <summary>Una nota de la melodía: seno con envolvente, silenciada fuera de su ventana.</summary>
    static float Note(float t, float start, float freq, float dur, float amp)
    {
        float lt = t - start;
        if (lt < 0f || lt >= dur) return 0f;
        return Mathf.Sin(TAU * freq * lt) * Env(lt, 0.006f, dur * 0.35f) * amp;
    }

    /// <summary>Rellena un AudioClip evaluando 'sample' en cada instante.</summary>
    static AudioClip Build(string name, float seconds, Func<float, float> sample)
    {
        int n = Mathf.CeilToInt(seconds * SR);
        var data = new float[n];
        for (int i = 0; i < n; i++)
            data[i] = Mathf.Clamp(sample(i / (float)SR), -1f, 1f);

        var clip = AudioClip.Create(name, n, 1, SR, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ── Efectos de interfaz ───────────────────────────────────────────────────

    /// <summary>Click seco y corto para cualquier botón de la UI.</summary>
    public static AudioClip UiClick()
    {
        var rng = new System.Random(7);
        return Build("sfx_ui_click", 0.05f, t =>
        {
            float env   = Env(t, 0.001f, 0.010f);
            float tick  = Mathf.Sin(TAU * 1800f * t);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            return (tick * 0.6f + noise * 0.4f) * env * 0.5f;
        });
    }

    /// <summary>Tick limpio y agudo: cambiar de pestaña, elegir ícono/color/avatar.</summary>
    public static AudioClip UiToggle()
    {
        return Build("sfx_ui_toggle", 0.032f, t =>
            Mathf.Sin(TAU * 2600f * t) * Env(t, 0.0008f, 0.007f) * 0.34f);
    }

    /// <summary>Barrido descendente: volver atrás, cerrar una pantalla.</summary>
    public static AudioClip UiBack()
    {
        const float D = 0.09f;
        return Build("sfx_ui_back", D, t =>
            Mathf.Sin(SweepPhase(t, 900f, 520f, D)) * Env(t, 0.003f, 0.028f) * 0.40f);
    }

    /// <summary>Dos notas ascendentes: confirmar, avanzar, guardar.</summary>
    public static AudioClip UiPrimary()
    {
        return Build("sfx_ui_primary", 0.16f, t =>
              Note(t, 0.00f, 659.25f, 0.10f, 0.30f)    // Mi5
            + Note(t, 0.06f, 987.77f, 0.12f, 0.28f));  // Si5
    }

    /// <summary>Click grave y amortiguado, sin movimiento tonal: cancelar o descartar.</summary>
    public static AudioClip UiCancel()
    {
        var rng = new System.Random(41);
        return Build("sfx_ui_cancel", 0.07f, t =>
        {
            float body  = Mathf.Sin(TAU * 420f * t);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.15f;
            return (body + noise) * Env(t, 0.002f, 0.018f) * 0.38f;
        });
    }

    /// <summary>Dos tonos graves descendentes con aspereza: acción destructiva.</summary>
    public static AudioClip UiDanger()
    {
        return Build("sfx_ui_danger", 0.28f, t =>
        {
            float tones = Note(t, 0.00f, 320f, 0.14f, 0.32f)
                        + Note(t, 0.10f, 240f, 0.20f, 0.32f);
            float grit  = Mathf.Sign(Mathf.Sin(TAU * 55f * t)) * 0.12f;
            return tones * (0.88f + grit);
        });
    }

    /// <summary>Zumbido descendente con trémolo: la acción falló.</summary>
    public static AudioClip UiError()
    {
        const float D = 0.30f;
        return Build("sfx_ui_error", D, t =>
        {
            float tone = Mathf.Sin(SweepPhase(t, 400f, 300f, D));
            float sq   = Mathf.Sign(tone) * 0.35f;                       // aspereza
            float trem = 0.65f + 0.35f * Mathf.Sin(TAU * 22f * t);       // vibración de "error"
            return (tone * 0.60f + sq) * trem * Env(t, 0.004f, 0.10f) * 0.42f;
        });
    }

    // ── Efectos de la zona de juego ───────────────────────────────────────────

    /// <summary>"Pop" ascendente al colocar un átomo.</summary>
    public static AudioClip PlaceAtom()
    {
        const float D = 0.16f;
        return Build("sfx_place_atom", D, t =>
        {
            float body = Mathf.Sin(SweepPhase(t, 320f, 720f, D));
            float harm = Mathf.Sin(SweepPhase(t, 640f, 1440f, D)) * 0.25f;
            return (body + harm) * Env(t, 0.004f, 0.045f) * 0.5f;
        });
    }

    /// <summary>Barrido descendente al borrar un átomo.</summary>
    public static AudioClip DeleteAtom()
    {
        var rng = new System.Random(23);
        const float D = 0.20f;
        return Build("sfx_delete_atom", D, t =>
        {
            float body  = Mathf.Sin(SweepPhase(t, 700f, 200f, D));
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.18f;
            return (body + noise) * Env(t, 0.003f, 0.055f) * 0.45f;
        });
    }

    /// <summary>Golpe grave con aspereza: colisión de átomos.</summary>
    public static AudioClip Collision()
    {
        const float D = 0.22f;
        return Build("sfx_collision", D, t =>
        {
            float body  = Mathf.Sin(SweepPhase(t, 190f, 90f, D));
            float grit  = Mathf.Sign(Mathf.Sin(TAU * 62f * t)) * 0.22f;  // aspereza tipo cuadrada
            return body * (0.78f + grit) * Env(t, 0.002f, 0.050f) * 0.55f;
        });
    }

    /// <summary>Arpegio ascendente de 3 notas: la molécula quedó formada.</summary>
    public static AudioClip MoleculeFormed()
    {
        return Build("sfx_molecule_formed", 0.50f, t =>
              Note(t, 0.00f, 523.25f, 0.30f, 0.34f)   // Do5
            + Note(t, 0.09f, 659.25f, 0.30f, 0.32f)   // Mi5
            + Note(t, 0.18f, 783.99f, 0.32f, 0.30f)); // Sol5
    }

    /// <summary>Fanfarria de 4 notas con brillo: primer descubrimiento de una molécula.</summary>
    public static AudioClip Discovery()
    {
        return Build("sfx_discovery", 0.95f, t =>
        {
            float melody =
                  Note(t, 0.00f,  523.25f, 0.26f, 0.30f)   // Do5
                + Note(t, 0.10f,  659.25f, 0.26f, 0.29f)   // Mi5
                + Note(t, 0.20f,  783.99f, 0.28f, 0.28f)   // Sol5
                + Note(t, 0.32f, 1046.50f, 0.60f, 0.30f);  // Do6
            // Octava superior a bajo volumen: le da el brillo de "logro".
            float shimmer = Note(t, 0.32f, 2093.00f, 0.55f, 0.07f);
            return melody + shimmer;
        });
    }

    // ── Música ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pad ambiental de 8s en La menor, pensado como PLACEHOLDER reemplazable:
    /// en cuanto haya un track real, basta con pasárselo a AudioManager.SetMusic().
    /// Las frecuencias (110/165/220/330 Hz) y los LFO (0.125/0.25/0.375 Hz) son
    /// múltiplos exactos de 1/8s, así que el bucle empalma sin chasquido.
    /// </summary>
    public static AudioClip AmbientLoop()
    {
        const float D = 8f;
        return Build("music_ambient_placeholder", D, t =>
        {
            float a  = Mathf.Sin(TAU * 110f * t) * (0.55f + 0.45f * Mathf.Sin(TAU * 0.125f * t))         * 0.30f;
            float b  = Mathf.Sin(TAU * 165f * t) * (0.55f + 0.45f * Mathf.Sin(TAU * 0.250f * t))         * 0.20f;
            float c  = Mathf.Sin(TAU * 220f * t) * (0.55f + 0.45f * Mathf.Sin(TAU * 0.375f * t))         * 0.14f;
            float sh = Mathf.Sin(TAU * 330f * t) * (0.50f + 0.50f * Mathf.Sin(TAU * 0.125f * t + 1.7f))  * 0.05f;
            return (a + b + c + sh) * 0.6f;
        });
    }
}
