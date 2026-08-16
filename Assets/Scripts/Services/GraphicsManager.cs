using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Aplica los dos ejes de Ajustes → Gráficos a todo el juego. Igual que
/// AudioManager, se autocrea al arrancar y vive con DontDestroyOnLoad, así que
/// funciona en las 19 escenas sin cablear nada en el inspector.
///
///  • CALIDAD (coste de render): sombras de la luz principal, antialiasing de
///    cámara y luces de relleno/contraluz.
///  • EFECTOS (post-procesado): bloom, viñeta y tonemapping sobre un Volume
///    global que este manager crea por código.
///
/// Ambos ejes se guardan POR CUENTA (GraphicsPrefs), así que al iniciar o
/// cerrar sesión hay que releer y re-aplicar: eso se detecta comparando el
/// userId contra el último aplicado.
///
/// NOTA sobre el render scale: vive en el UniversalRenderPipelineAsset, no en
/// la escena. Cambiarlo en runtime desde el Editor MODIFICA el .asset y te deja
/// la configuración pisada al salir del Play Mode, así que aquí no se toca; ese
/// eje necesita tres URP assets separados y está pendiente.
/// </summary>
public class GraphicsManager : MonoBehaviour
{
    public static GraphicsManager Instance
    {
        get
        {
            if (_instance == null) Bootstrap();
            return _instance;
        }
    }
    static GraphicsManager _instance;

    /// <summary>Nivel de efectos actual. Lo consulta el gameplay para decidir si
    /// lanza partículas o micro-animaciones.</summary>
    public GraphicsLevel Effects { get; private set; } = GraphicsPrefs.DEFAULT_EFFECTS;
    public GraphicsLevel Quality { get; private set; } = GraphicsPrefs.DEFAULT_QUALITY;

    // Umbral por encima de 1.0 para que el bloom solo agarre lo emisivo en HDR
    // (el resaltado de selección del átomo, que es color × 1.6) y deje la UI
    // intacta: los colores normales nunca pasan de 1.0.
    const float BLOOM_THRESHOLD = 1.1f;

    Volume        volume;
    VolumeProfile profile;
    Bloom         bloom;
    Vignette      vignette;
    Tonemapping   tonemapping;

    Light fillLight, rimLight;
    string appliedUser;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        new GameObject("GraphicsManager").AddComponent<GraphicsManager>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        BuildVolume();
        BuildExtraLights();
        ReloadFromPrefs();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (_instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (profile) Destroy(profile);
    }

    // ── Construcción ──────────────────────────────────────────────────────────

    void BuildVolume()
    {
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "ChemiTech_RuntimeProfile";

        tonemapping = profile.Add<Tonemapping>(true);
        bloom       = profile.Add<Bloom>(true);
        vignette    = profile.Add<Vignette>(true);

        // Tonemapping siempre activo: sin él, el HDR se recorta de golpe y los
        // colores saturados del juego se ven planos incluso en Efectos = Bajo.
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value         = TonemappingMode.Neutral;

        bloom.threshold.overrideState = true;
        bloom.threshold.value         = BLOOM_THRESHOLD;
        bloom.intensity.overrideState = true;
        bloom.scatter.overrideState   = true;

        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.4f;

        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal      = true;
        volume.priority      = 100f;   // por encima de cualquier Volume de escena
        volume.sharedProfile = profile;
    }

    /// <summary>
    /// Relleno y contraluz. Son direccionales, así que solo importa su rotación,
    /// no su posición: pueden vivir en el propio manager persistente y alumbran
    /// la escena 3D sin necesidad de tocar ZonaJuegoScene.
    /// </summary>
    void BuildExtraLights()
    {
        fillLight = MakeLight("FillLight", new Vector3(30f, 150f, 0f), new Color(0.55f, 0.68f, 1f), 0.35f);
        rimLight  = MakeLight("RimLight",  new Vector3(8f, -160f, 0f), new Color(0.25f, 0.90f, 1f), 0.55f);
    }

    Light MakeLight(string name, Vector3 euler, Color color, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(euler);

        var l = go.AddComponent<Light>();
        l.type      = LightType.Directional;
        l.color     = color;
        l.intensity = intensity;
        l.shadows   = LightShadows.None;   // solo la principal proyecta sombras
        return l;
    }

    // ── Aplicación ────────────────────────────────────────────────────────────

    void ReloadFromPrefs()
    {
        Quality     = GraphicsPrefs.Quality;
        Effects     = GraphicsPrefs.Effects;
        appliedUser = SessionData.UserId ?? "";
        ApplyEffects();
        ApplyQuality();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Login/logout siempre implican cambio de escena. Solo re-aplicamos si
        // el usuario efectivo cambió: hacerlo en cada navegación repetiría el
        // trabajo sin motivo.
        string current = SessionData.UserId ?? "";
        if (current != appliedUser) ReloadFromPrefs();
        else                        ApplyQuality();  // la cámara y la luz son nuevas en cada escena
    }

    /// <summary>Cambia el nivel de efectos, lo aplica y lo persiste.</summary>
    public void SetEffects(GraphicsLevel level)
    {
        Effects = level;
        GraphicsPrefs.Effects = level;
        ApplyEffects();
    }

    /// <summary>Cambia el nivel de calidad, lo aplica y lo persiste.</summary>
    public void SetQuality(GraphicsLevel level)
    {
        Quality = level;
        GraphicsPrefs.Quality = level;
        ApplyQuality();
    }

    void ApplyEffects()
    {
        if (bloom == null) return;

        switch (Effects)
        {
            case GraphicsLevel.Bajo:
                bloom.active    = false;
                vignette.active = false;
                break;

            case GraphicsLevel.Medio:
                bloom.active          = true;
                bloom.intensity.value = 0.60f;
                bloom.scatter.value   = 0.60f;
                vignette.active          = true;
                vignette.intensity.value = 0.22f;
                break;

            default: // Alto
                bloom.active          = true;
                bloom.intensity.value = 1.10f;
                bloom.scatter.value   = 0.70f;
                vignette.active          = true;
                vignette.intensity.value = 0.30f;
                break;
        }
    }

    void ApplyQuality()
    {
        ApplyCamera();
        ApplyShadows();

        bool fill = Quality != GraphicsLevel.Bajo;
        bool rim  = Quality == GraphicsLevel.Alto;
        if (fillLight) fillLight.enabled = fill;
        if (rimLight)  rimLight.enabled  = rim;
    }

    // El post-procesado se activa POR CÁMARA en URP: sin esto, el Volume global
    // no pinta nada. La cámara cambia con cada escena, de ahí que se re-aplique.
    void ApplyCamera()
    {
        var cam = Camera.main;
        if (!cam) return;

        var data = cam.GetUniversalAdditionalCameraData();
        if (data == null) return;

        data.renderPostProcessing = true;

        switch (Quality)
        {
            case GraphicsLevel.Bajo:
                data.antialiasing = AntialiasingMode.None;
                break;
            case GraphicsLevel.Medio:
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                break;
            default:
                data.antialiasing        = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                break;
        }
    }

    // Las sombras se controlan en la propia luz, que es un objeto de escena:
    // es la vía segura, no toca ningún asset del pipeline.
    void ApplyShadows()
    {
        var sun = FindSunLight();
        if (!sun) return;

        sun.shadows = Quality switch
        {
            GraphicsLevel.Bajo  => LightShadows.None,
            GraphicsLevel.Medio => LightShadows.Hard,
            _                   => LightShadows.Soft,
        };
    }

    /// <summary>Luz direccional principal de la escena, excluyendo las nuestras.</summary>
    Light FindSunLight()
    {
        if (RenderSettings.sun && RenderSettings.sun.type == LightType.Directional)
            return RenderSettings.sun;

        var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Light best = null;
        foreach (var l in lights)
        {
            if (!l || l.type != LightType.Directional) continue;
            if (l == fillLight || l == rimLight) continue;
            if (best == null || l.intensity > best.intensity) best = l;
        }
        return best;
    }
}
