using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ambientación 3D de la zona de juego. Por ahora, la rejilla de la plataforma:
/// da escala y sensación de profundidad al rotar la cámara, y es la referencia
/// contra la que se lee la marca de suelo de PlacementGuides — sobre un plano
/// liso esa marca no se puede comparar con nada.
///
/// Es información espacial y no cuesta nada (una textura sobre geometría que ya
/// existe), así que NO depende del nivel de Calidad ni de Efectos.
///
/// Se aplica sobre una INSTANCIA del material, nunca sobre Zone_Platform.mat:
/// tocar el material compartido modificaría el asset del proyecto y en el Editor
/// te dejaría el cambio pegado al salir del Play Mode.
/// </summary>
public class ZoneEnvironment : MonoBehaviour
{
    // La plataforma es un Plane con escala 2.6 → 26 unidades de lado.
    const float PLATFORM_SIZE = 26f;
    const float CELL_SIZE     = 2f;    // una celda cada 2 unidades (el átomo mide ~1.1)
    const int   CELL_PIXELS   = 128;
    const int   LINE_PIXELS   = 3;

    static readonly Color GRID_BG   = new Color(0.102f, 0.129f, 0.314f, 1f); // #1A2150, el de la plataforma
    static readonly Color GRID_LINE = new Color(0.180f, 0.290f, 0.560f, 1f); // azul más claro, sin gritar

    // Giro lentísimo del cielo en Efectos = Alto. Una vuelta completa tarda unos
    // 12 minutos: no se percibe como movimiento, solo evita que el fondo se
    // sienta una foto pegada.
    const float SKY_DRIFT_DEG_PER_SEC = 0.5f;

    Texture2D gridTex, skyTex;
    Material  skyMat;
    float     skyAngle;

    void Start()
    {
        ApplyPlatformGrid();
        ApplyStarfield();
    }

    void Update()
    {
        if (skyMat == null) return;
        if (GraphicsManager.Instance.Effects != GraphicsLevel.Alto) return;

        skyAngle = (skyAngle + SKY_DRIFT_DEG_PER_SEC * Time.deltaTime) % 360f;
        skyMat.SetFloat("_Rotation", skyAngle);
    }

    void ApplyPlatformGrid()
    {
        var plat = FindPlatformRenderer();
        if (plat == null)
        {
            Debug.LogWarning("[ZoneEnvironment] No se encontró 'Platform'; la rejilla no se aplicó.");
            return;
        }

        gridTex = ProceduralTextures.GridCell(CELL_PIXELS, GRID_BG, GRID_LINE, LINE_PIXELS);

        // .material (no .sharedMaterial) crea una copia propia de este renderer.
        var mat = plat.material;
        mat.SetTexture("_BaseMap", gridTex);
        mat.SetColor("_BaseColor", Color.white);   // el color ya viene en la textura

        float tiles = PLATFORM_SIZE / CELL_SIZE;
        mat.SetTextureScale("_BaseMap", new Vector2(tiles, tiles));
    }

    /// <summary>
    /// Cielo estrellado. Reemplaza el color sólido #0A1233 de la cámara, que hoy
    /// deja la zona de juego como un vacío plano pese a que el juego promete un
    /// "universo molecular".
    ///
    /// La RESOLUCIÓN sigue el nivel de Calidad (pesa en memoria de video, que es
    /// donde duele en teléfonos flojos) y el GIRO solo ocurre en Efectos = Alto.
    /// El cielo en sí se ve siempre: dibujarlo no cuesta más que rellenar con un
    /// color, y es la identidad visual del juego.
    /// </summary>
    void ApplyStarfield()
    {
        var shader = Shader.Find("Skybox/Panoramic");
        if (shader == null)
        {
            Debug.LogWarning("[ZoneEnvironment] 'Skybox/Panoramic' no está en la build; " +
                             "se mantiene el fondo de color sólido. Agrégalo en " +
                             "Project Settings → Graphics → Always Included Shaders.");
            return;
        }

        int width = GraphicsManager.Instance.Quality switch
        {
            GraphicsLevel.Bajo  => 512,
            GraphicsLevel.Medio => 1024,
            _                   => 1536,
        };
        int stars = width;            // densidad proporcional al detalle
        int blobs = width / 128;

        skyTex = ProceduralTextures.Starfield(width, width / 2, stars, blobs);

        skyMat = new Material(shader);
        skyMat.SetTexture("_MainTex", skyTex);
        skyMat.SetFloat("_Mapping", 1f);    // Latitude/Longitude
        skyMat.SetFloat("_ImageType", 0f);  // 360°
        skyMat.SetFloat("_Exposure", 1f);
        skyMat.SetFloat("_Rotation", 0f);

        RenderSettings.skybox = skyMat;

        var cam = Camera.main;
        if (cam) cam.clearFlags = CameraClearFlags.Skybox;

        ApplyAmbient();
    }

    /// <summary>
    /// La escena venía tomando la luz ambiental del skybox gris por defecto, que
    /// nunca se veía porque la cámara borraba con color sólido.
    ///
    /// Al poner un cielo estrellado —que es casi negro— dejar el ambiente en modo
    /// Skybox APAGARÍA la cara en sombra de cada átomo. Por eso se fija a un
    /// degradado explícito en la paleta del juego: mantiene el relleno y además
    /// las sombras dejan de ser de ese gris parduzco genérico.
    /// </summary>
    static void ApplyAmbient()
    {
        RenderSettings.ambientMode         = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.16f, 0.26f, 0.44f);
        RenderSettings.ambientEquatorColor = new Color(0.11f, 0.15f, 0.32f);
        RenderSettings.ambientGroundColor  = new Color(0.05f, 0.07f, 0.16f);
    }

    /// <summary>
    /// Busca el plano de la plataforma. El builder lo crea como 'Platform' dentro
    /// de 'PlayArea'; si alguien lo movió, se cae a una búsqueda por nombre.
    /// </summary>
    static Renderer FindPlatformRenderer()
    {
        var area = GameObject.Find("PlayArea");
        if (area != null)
        {
            var t = area.transform.Find("Platform");
            if (t != null) return t.GetComponent<Renderer>();
        }

        var go = GameObject.Find("Platform");
        return go != null ? go.GetComponent<Renderer>() : null;
    }

    void OnDestroy()
    {
        // Texturas y material creados por código: nadie más los libera.
        if (gridTex) Destroy(gridTex);
        if (skyTex)  Destroy(skyTex);
        if (skyMat)  Destroy(skyMat);
    }
}
