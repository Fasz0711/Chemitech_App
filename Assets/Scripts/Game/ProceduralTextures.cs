using UnityEngine;

/// <summary>
/// Texturas generadas por código para las guías 3D de la zona de juego. Igual
/// que con el audio, evita meter archivos al proyecto y garantiza que el borde
/// quede suavizado a cualquier resolución.
/// </summary>
public static class ProceduralTextures
{
    /// <summary>
    /// Anillo blanco sobre fondo transparente. 'inner' y 'outer' van en fracción
    /// del radio (0 = centro, 1 = borde de la textura). El color se pone luego
    /// desde el material, así que la textura sirve para el anillo de selección y
    /// para la marca del suelo por igual.
    /// </summary>
    public static Texture2D Ring(int size = 128, float inner = 0.70f, float outer = 0.92f)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name       = "tex_ring",
        };

        // Ancho del degradado del borde, en las mismas unidades que inner/outer.
        // Un píxel y medio evita tanto el aliasing como un borde lavado.
        float edge   = 1.5f / (size * 0.5f);
        float half   = size * 0.5f;
        var   pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // +0.5 para medir desde el centro del píxel, no de su esquina.
            float dx = (x + 0.5f - half) / half;
            float dy = (y + 0.5f - half) / half;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);

            float a = SmoothBand(d, inner, outer, edge);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    /// <summary>Círculo relleno con borde suave (punto central de la marca de suelo).</summary>
    public static Texture2D Dot(int size = 64, float radius = 0.85f)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name       = "tex_dot",
        };

        float edge   = 1.5f / (size * 0.5f);
        float half   = size * 0.5f;
        var   pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - half) / half;
            float dy = (y + 0.5f - half) / half;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);

            float a = 1f - Smooth(radius - edge, radius, d);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    /// <summary>
    /// Una celda de rejilla: fondo liso con línea en dos de sus bordes. Al
    /// repetirse sobre la plataforma forma una cuadrícula continua — por eso la
    /// línea va solo en dos lados, si fuera en los cuatro se vería doble de
    /// grosor en cada unión entre celdas.
    ///
    /// Los colores viajan EN la textura (no en _BaseColor) porque el mapa base
    /// se multiplica por el color del material, y multiplicando solo se puede
    /// oscurecer: unas líneas más claras que el fondo no saldrían nunca.
    /// </summary>
    public static Texture2D GridCell(int size, Color background, Color lineColor, int lineWidth)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Repeat,   // imprescindible para poder repetirla
            filterMode = FilterMode.Bilinear,
            name       = "tex_grid",
        };

        var bg   = (Color32)background;
        var ln   = (Color32)lineColor;
        var px   = new Color32[size * size];
        int w    = Mathf.Clamp(lineWidth, 1, size / 2);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            px[y * size + x] = (x < w || y < w) ? ln : bg;

        tex.SetPixels32(px);
        tex.Apply(true, false);   // con mipmaps: sin ellos la rejilla hace moaré a distancia
        return tex;
    }

    /// <summary>
    /// Cielo estrellado en proyección equirectangular (2:1), para un material
    /// Skybox/Panoramic.
    ///
    /// Se eligió equirectangular en vez de cubemap a propósito: el mapeo
    /// latitud/longitud es directo y no tiene las convenciones de orientación
    /// por cara del cubemap, donde una cara volteada produce una costura visible.
    /// Aquí la única unión es en longitud 0/360, y encaja sola porque θ=0 y θ=2π
    /// dan la misma dirección.
    ///
    /// El degradado se calcula por FILA (solo depende de la latitud) y la nebulosa
    /// y las estrellas se estampan encima. Hacerlo por píxel con trigonometría
    /// costaría cientos de milisegundos a resolución alta y se notaría como un
    /// tirón al entrar al universo.
    /// </summary>
    public static Texture2D Starfield(int width, int height, int starCount, int nebulaBlobs, int seed = 1337)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            wrapMode   = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            name       = "tex_starfield",
        };

        var px  = new Color32[width * height];
        var rng = new System.Random(seed);

        // ── Degradado vertical: más profundo abajo, algo más azul arriba ──────
        Color horizon = new Color(0.020f, 0.031f, 0.098f);
        Color zenith  = new Color(0.043f, 0.071f, 0.200f);

        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            var   c = (Color32)Color.Lerp(horizon, zenith, v);
            int   row = y * width;
            for (int x = 0; x < width; x++) px[row + x] = c;
        }

        // ── Nebulosa: manchas grandes y tenues en los tonos del juego ─────────
        Color[] nebulaTints =
        {
            new Color(0.18f, 0.82f, 0.88f),  // cian
            new Color(0.55f, 0.35f, 0.71f),  // morado
            new Color(0.90f, 0.46f, 0.71f),  // rosa
        };

        for (int i = 0; i < nebulaBlobs; i++)
        {
            int cx = rng.Next(width);
            int cy = rng.Next(height);
            float r = width * (0.06f + (float)rng.NextDouble() * 0.10f);
            var tint = nebulaTints[rng.Next(nebulaTints.Length)];
            Splat(px, width, height, cx, cy, r, tint, 0.10f + (float)rng.NextDouble() * 0.06f);
        }

        // ── Estrellas ─────────────────────────────────────────────────────────
        for (int i = 0; i < starCount; i++)
        {
            // Dirección uniforme sobre la esfera: repartir u/v al azar
            // amontonaría estrellas en los polos.
            double dy    = 1.0 - 2.0 * rng.NextDouble();
            double lat   = System.Math.Asin(dy);
            float  v     = (float)(lat / System.Math.PI + 0.5);
            float  u     = (float)rng.NextDouble();

            int cx = Mathf.Clamp((int)(u * width), 0, width - 1);
            int cy = Mathf.Clamp((int)(v * height), 0, height - 1);

            float bright = 0.35f + (float)rng.NextDouble() * 0.65f;
            bool  big    = rng.NextDouble() < 0.06;     // unas pocas destacan
            float radius = big ? 2.2f : 1.1f;

            // La proyección comprime la longitud cerca de los polos: sin
            // compensar, una estrella allí saldría estirada a lo ancho.
            float lonScale = 1f / Mathf.Max(0.15f, Mathf.Cos((float)lat));

            Color tint = rng.NextDouble() < 0.18
                ? new Color(1f, 0.86f, 0.72f)   // alguna cálida
                : new Color(0.88f, 0.94f, 1f);  // el resto, blanco azulado

            Splat(px, width, height, cx, cy, radius * lonScale, tint, bright, radius);
        }

        tex.SetPixels32(px);
        tex.Apply(true, false);
        return tex;
    }

    /// <summary>
    /// Estampa un punto suave sumando luz. 'radiusY' permite achatarlo en
    /// vertical (por defecto es circular). Envuelve en horizontal para que nada
    /// se corte en la unión de longitud.
    /// </summary>
    static void Splat(Color32[] px, int w, int h, int cx, int cy,
                      float radiusX, Color tint, float intensity, float radiusY = -1f)
    {
        if (radiusY < 0f) radiusY = radiusX;

        int rx = Mathf.CeilToInt(radiusX), ry = Mathf.CeilToInt(radiusY);

        for (int dy = -ry; dy <= ry; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h) continue;                 // en los polos no se envuelve

            for (int dx = -rx; dx <= rx; dx++)
            {
                float nx = dx / radiusX, ny = dy / radiusY;
                float d2 = nx * nx + ny * ny;
                if (d2 > 1f) continue;

                float falloff = (1f - d2) * (1f - d2);     // suave hacia el borde
                int x = ((cx + dx) % w + w) % w;            // longitud: sí envuelve

                int idx = y * w + x;
                var c   = px[idx];
                px[idx] = new Color32(
                    (byte)Mathf.Min(255f, c.r + tint.r * falloff * intensity * 255f),
                    (byte)Mathf.Min(255f, c.g + tint.g * falloff * intensity * 255f),
                    (byte)Mathf.Min(255f, c.b + tint.b * falloff * intensity * 255f),
                    255);
            }
        }
    }

    /// <summary>Opacidad de una banda [inner, outer] con ambos bordes suavizados.</summary>
    static float SmoothBand(float d, float inner, float outer, float edge)
        => Smooth(inner - edge, inner, d) * (1f - Smooth(outer - edge, outer, d));

    /// <summary>Interpolación suave clásica (0 antes de a, 1 después de b).</summary>
    static float Smooth(float a, float b, float t)
    {
        if (b - a <= Mathf.Epsilon) return t >= b ? 1f : 0f;
        float k = Mathf.Clamp01((t - a) / (b - a));
        return k * k * (3f - 2f * k);
    }
}
