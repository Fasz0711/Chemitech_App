using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Guías espaciales del átomo ACTIVO (el seleccionado, o la previsualización que
/// se está colocando):
///
///   • Marca en la plataforma, justo debajo, con su posición en el plano.
///   • Línea vertical que une la marca con el átomo → muestra a qué ALTURA está.
///   • Anillo alrededor del átomo seleccionado → cuál está seleccionado.
///
/// Es información de juego, no adorno: sin la línea vertical no hay forma de
/// saber a qué altura quedó un átomo al subirlo con las flechas. Por eso NO
/// depende del nivel de Efectos ni de Calidad; se ve siempre.
///
/// Todo se crea por código (geometría, texturas y materiales), así que no hay
/// que tocar ZonaJuegoScene ni cablear nada en el inspector.
/// </summary>
public class PlacementGuides : MonoBehaviour
{
    // La plataforma está en y = 0. Un pelo por encima evita que la marca pelee
    // con el suelo por el z-buffer y parpadee al girar la cámara.
    const float GROUND_Y = 0.02f;

    const float MARKER_SIZE = 2.2f;
    const float LINE_WIDTH  = 0.035f;
    const float RING_SCALE  = 2.0f;   // respecto al tamaño del átomo

    static readonly Color TINT_SELECTED = new Color(0.18f, 0.88f, 1.00f, 0.95f); // cian
    static readonly Color TINT_PREVIEW  = new Color(1.00f, 0.72f, 0.25f, 0.95f); // ámbar

    GameObject marker, line, ring;
    Material   markerMat, lineMat, ringMat;
    Transform  camT;
    float      atomScale = 1.1f;

    /// <summary>Crea las guías. 'scale' es el tamaño del átomo, para dimensionar el anillo.</summary>
    public void Setup(float scale)
    {
        atomScale = scale;
        BuildIfNeeded();
        Hide();
    }

    void BuildIfNeeded()
    {
        if (marker != null) return;

        var ringTex = ProceduralTextures.Ring();
        var dotTex  = ProceduralTextures.Dot();

        // Marca en el suelo: quad tumbado. Se le pone el anillo, no el punto,
        // porque un aro se lee mejor sobre la rejilla que una mancha sólida.
        markerMat = MakeUnlitMat(ringTex);
        marker = MakeQuad("GuideMarker", markerMat);
        marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        marker.transform.localScale    = Vector3.one * MARKER_SIZE;

        // Línea vertical.
        lineMat = MakeUnlitMat(dotTex);
        line = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        line.name = "GuideLine";
        StripCollider(line);
        line.transform.SetParent(transform, false);
        line.GetComponent<Renderer>().sharedMaterial = lineMat;

        // Anillo alrededor del átomo (mira siempre a la cámara).
        ringMat = MakeUnlitMat(ringTex);
        ring = MakeQuad("GuideRing", ringMat);
    }

    GameObject MakeQuad(string name, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        StripCollider(go);
        go.transform.SetParent(transform, false);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void StripCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);
    }

    /// <summary>URP Unlit transparente, misma receta que usa ZonaJuegoBuilder para el fantasma.</summary>
    static Material MakeUnlitMat(Texture2D tex)
    {
        // Shader.Find en runtime solo encuentra shaders incluidos en la build, y
        // ningún material del proyecto usa URP/Unlit, así que puede quedar fuera
        // aunque en el Editor funcione. Caemos a Lit, que sí está garantizado
        // por los materiales existentes.
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
            Debug.LogWarning("[PlacementGuides] URP/Unlit no está incluido en la build; " +
                             "se usa Lit. Agrégalo en Project Settings → Graphics → Always Included Shaders.");
        }

        var m = new Material(shader);
        m.SetFloat("_Surface", 1f);   // Transparent
        m.SetFloat("_Blend", 0f);     // Alpha
        m.SetFloat("_ZWrite", 0f);
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (tex) m.SetTexture("_BaseMap", tex);
        return m;
    }

    /// <summary>
    /// Coloca las guías bajo/alrededor del objetivo. 'isSelection' distingue un
    /// átomo ya colocado (cian, con anillo) de la previsualización (ámbar, sin
    /// anillo: el fantasma translúcido ya se distingue solo).
    /// </summary>
    public void Track(Transform target, bool isSelection)
    {
        if (!target) { Hide(); return; }
        BuildIfNeeded();

        Vector3 p     = target.position;
        Color   tint  = isSelection ? TINT_SELECTED : TINT_PREVIEW;
        float   height = Mathf.Max(0f, p.y - GROUND_Y);

        markerMat.SetColor("_BaseColor", tint);
        lineMat.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 0.55f));

        marker.SetActive(true);
        marker.transform.position = new Vector3(p.x, GROUND_Y, p.z);

        // La línea solo aporta si el átomo está despegado del suelo.
        bool showLine = height > 0.05f;
        line.SetActive(showLine);
        if (showLine)
        {
            line.transform.position   = new Vector3(p.x, GROUND_Y + height * 0.5f, p.z);
            line.transform.localScale = new Vector3(LINE_WIDTH, height * 0.5f, LINE_WIDTH);
        }

        ring.SetActive(isSelection);
        if (isSelection)
        {
            ringMat.SetColor("_BaseColor", tint);
            ring.transform.position   = p;
            ring.transform.localScale = Vector3.one * (atomScale * RING_SCALE);
            FaceCamera(ring.transform);
        }
    }

    public void Hide()
    {
        if (marker) marker.SetActive(false);
        if (line)   line.SetActive(false);
        if (ring)   ring.SetActive(false);
    }

    void FaceCamera(Transform t)
    {
        if (!camT)
        {
            if (!Camera.main) return;
            camT = Camera.main.transform;
        }
        t.rotation = Quaternion.LookRotation(t.position - camT.position);
    }

    void OnDestroy()
    {
        // Materiales y texturas creados por código: no los recoge nadie más.
        foreach (var m in new[] { markerMat, lineMat, ringMat })
        {
            if (!m) continue;
            var tex = m.GetTexture("_BaseMap");
            if (tex) Destroy(tex);
            Destroy(m);
        }
    }
}
