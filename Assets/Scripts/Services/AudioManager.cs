using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Audio global del juego. Vive en un GameObject con DontDestroyOnLoad que se
/// crea solo al arrancar (RuntimeInitializeOnLoadMethod), así que TODAS las
/// escenas tienen sonido sin necesidad de cablear nada en el inspector.
///
/// Responsabilidades:
///  • Sintetiza los clips al inicio (ProceduralAudio) — el proyecto no tiene
///    archivos de audio.
///  • Reproduce la música en bucle y los efectos puntuales.
///  • Aplica el volumen de AudioPrefs y lo persiste cuando Ajustes lo cambia.
///  • Engancha automáticamente un click a cada Button de cada escena.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null) Bootstrap();
            return _instance;
        }
    }
    static AudioManager _instance;

    // Techo de la música: debe quedar por debajo de los efectos aunque el slider
    // esté al 100.
    const float MUSIC_HEADROOM = 0.55f;

    // Ruta dentro de Resources (sin extensión) del track de fondo opcional.
    const string MUSIC_RESOURCE_PATH = "Music/bgm_main";

    AudioSource musicSource, sfxSource;
    AudioClip   clipUiClick, clipPlace, clipDelete, clipCollision, clipFormed, clipDiscovery;
    float       sfxGain;   // cacheado: los efectos se disparan muy seguido

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        new GameObject("AudioManager").AddComponent<AudioManager>(); // Awake hace el resto
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        BuildClips();
        BuildSources();
        ApplyVolumes();

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Nos creamos antes de que cargue la primera escena, así que aquí todavía
        // no hay botones que cablear: se hace tras el primer frame. Es red de
        // seguridad por si sceneLoaded no alcanzara a la escena inicial (el
        // marcador UiClickSfx evita que se cablee dos veces).
        StartCoroutine(BindAfterStart());

        if (musicSource.clip != null) musicSource.Play();
    }

    void OnDestroy()
    {
        if (_instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ── Construcción ──────────────────────────────────────────────────────────

    void BuildClips()
    {
        clipUiClick   = ProceduralAudio.UiClick();
        clipPlace     = ProceduralAudio.PlaceAtom();
        clipDelete    = ProceduralAudio.DeleteAtom();
        clipCollision = ProceduralAudio.Collision();
        clipFormed    = ProceduralAudio.MoleculeFormed();
        clipDiscovery = ProceduralAudio.Discovery();
    }

    void BuildSources()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip        = LoadMusicClip();
        musicSource.loop        = true;
        musicSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop        = false;
        sfxSource.playOnAwake = false;
    }

    /// <summary>
    /// Track de fondo. Si existe Assets/Resources/Music/bgm_main.(ogg|mp3|wav)
    /// se usa ese; si no, cae en el pad sintetizado. Así, para cambiar la música
    /// basta con soltar el archivo con ese nombre: no hay que tocar código ni
    /// cablear nada en el inspector.
    /// </summary>
    static AudioClip LoadMusicClip()
    {
        var clip = Resources.Load<AudioClip>(MUSIC_RESOURCE_PATH);
        if (clip != null)
        {
            Debug.Log($"[Audio] Música: '{clip.name}' ({clip.length:0.0}s) desde Resources.");
            return clip;
        }

        Debug.Log($"[Audio] Sin '{MUSIC_RESOURCE_PATH}' en Resources: se usa el pad sintetizado.");
        return ProceduralAudio.AmbientLoop();
    }

    // ── Volumen ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 0..100 → ganancia. Se eleva al cuadrado porque el oído percibe el volumen
    /// de forma logarítmica: con escala lineal, la mitad del slider ya suena casi
    /// igual de fuerte y el recorrido útil se concentra al final.
    /// </summary>
    static float Gain(int value0to100)
    {
        float v = Mathf.Clamp01(value0to100 / 100f);
        return v * v;
    }

    void ApplyVolumes()
    {
        if (musicSource) musicSource.volume = Gain(AudioPrefs.Music) * MUSIC_HEADROOM;
        sfxGain = Gain(AudioPrefs.Sfx);
    }

    /// <summary>Aplica y persiste el volumen de música (0..100). Lo llama Ajustes.</summary>
    public void SetMusicVolume(int value0to100)
    {
        AudioPrefs.Music = value0to100;
        ApplyVolumes();
    }

    /// <summary>Aplica y persiste el volumen de efectos (0..100). Lo llama Ajustes.</summary>
    public void SetSfxVolume(int value0to100)
    {
        AudioPrefs.Sfx = value0to100;
        sfxGain = Gain(value0to100);
    }

    /// <summary>
    /// Reemplaza el pad sintetizado por un track real sin tocar nada más del
    /// sistema (volumen, persistencia y bucle siguen funcionando igual).
    /// </summary>
    public void SetMusic(AudioClip clip)
    {
        if (!musicSource) return;
        musicSource.Stop();
        musicSource.clip = clip;
        ApplyVolumes();
        if (clip != null) musicSource.Play();
    }

    // ── Reproducción de efectos ───────────────────────────────────────────────

    void PlaySfx(AudioClip clip)
    {
        if (!clip || !sfxSource) return;
        if (sfxGain <= 0f) return;         // silenciado: no gastamos una voz
        sfxSource.PlayOneShot(clip, sfxGain);
    }

    public void PlayUiClick()        => PlaySfx(clipUiClick);
    public void PlayPlaceAtom()      => PlaySfx(clipPlace);
    public void PlayDeleteAtom()     => PlaySfx(clipDelete);
    public void PlayCollision()      => PlaySfx(clipCollision);
    public void PlayMoleculeFormed() => PlaySfx(clipFormed);
    public void PlayDiscovery()      => PlaySfx(clipDiscovery);

    // ── Click automático en todos los botones ─────────────────────────────────

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureListener();
        BindButtons();                      // botones que ya existen en la escena
        StartCoroutine(BindAfterStart());   // tarjetas instanciadas en Start() (diario, universos…)
    }

    IEnumerator BindAfterStart()
    {
        yield return null;
        EnsureListener();
        BindButtons();
    }

    /// <summary>
    /// Cablea el click a cada Button que no lo tenga. Incluye los inactivos
    /// porque los modales arrancan apagados. UiClickSfx marca los ya cableados
    /// para no sonar dos veces al re-escanear.
    /// </summary>
    void BindButtons()
    {
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in buttons)
        {
            if (!b || b.GetComponent<UiClickSfx>()) continue;
            b.gameObject.AddComponent<UiClickSfx>();
            b.onClick.AddListener(PlayUiClick);
        }
    }

    /// <summary>
    /// Sin AudioListener no se oye nada. Los builders lo ponen en la cámara de
    /// cada escena; esto solo cubre el caso de una escena que se quedara sin él.
    /// </summary>
    void EnsureListener()
    {
        var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var own = GetComponent<AudioListener>();

        if (listeners.Length == 0) { gameObject.AddComponent<AudioListener>(); return; }

        // La escena trae el suyo: retiramos el de respaldo, dos listeners activos
        // hacen que Unity avise y solo uno se usa igual.
        if (own && listeners.Length > 1) Destroy(own);
    }
}
