using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dibuja enlaces ya resueltos como cilindros 3D. NO decide qué enlaces hay ni
/// habla con el backend: recibe la lista (a, b, order) y la pinta.
///
/// Está separado de BondManager porque la escena de clase necesita dibujar
/// enlaces que llegan resueltos del servidor SIN arrastrar el ciclo de detección.
///
/// No es MonoBehaviour: lo instancia quien lo usa, le pasa el mapa de átomos vivo
/// y llama a UpdateVisuals() cada frame. Al terminar, Dispose().
/// </summary>
public class BondRenderer
{
    // 'order' es cuántas LÍNEAS paralelas tiene el enlace (simple/doble/triple).
    // 'cyls' tiene una pieza por línea, o dos si el enlace es bicolor: cada mitad
    // lleva el color de su átomo, como en cualquier visor molecular. Con bicolor
    // el índice de la línea i son cyls[i*2] (lado A) y cyls[i*2+1] (lado B).
    class BondView { public int a, b, order; public GameObject[] cyls; public bool bicolor; }

    readonly List<BondView> views = new List<BondView>();

    // Un material por elemento, reutilizado entre todos sus enlaces: crear uno
    // por mitad dispararía el número de materiales en moléculas grandes.
    readonly Dictionary<int, Material> halfMats = new Dictionary<int, Material>();
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    readonly Transform root;
    readonly Material  baseMaterial;
    readonly IReadOnlyDictionary<int, Atom3D> byId;
    readonly float thickness, spacing;

    Camera cam;

    /// <param name="byId">Mapa vivo de átomos; el renderer guarda la referencia,
    /// así que el dueño puede limpiarlo y rellenarlo sin volver a avisar.</param>
    public BondRenderer(Transform root, Material bondMaterial,
                        IReadOnlyDictionary<int, Atom3D> byId,
                        float thickness, float spacing)
    {
        this.root         = root;
        this.baseMaterial = bondMaterial;
        this.byId         = byId;
        this.thickness    = thickness;
        this.spacing      = spacing;
    }

    /// <summary>Reconstruye los cilindros para el conjunto de enlaces dado.</summary>
    public void SetBonds(IEnumerable<(int a, int b, int order)> bonds)
    {
        Clear();
        if (bonds == null) return;

        // En Calidad = Bajo se mantiene el cilindro gris de una pieza: partir cada
        // enlace duplica los objetos, y un triple pasaría de 3 piezas a 6.
        bool bicolor = GraphicsManager.Instance.Quality != GraphicsLevel.Bajo;

        foreach (var (a, b, order) in bonds)
        {
            int n = Mathf.Clamp(order, 1, 3);
            var bv = new BondView
            {
                a = a, b = b, order = n, bicolor = bicolor,
                cyls = new GameObject[bicolor ? n * 2 : n],
            };

            byId.TryGetValue(a, out var atomA);
            byId.TryGetValue(b, out var atomB);

            for (int i = 0; i < n; i++)
            {
                if (bicolor)
                {
                    bv.cyls[i * 2]     = CreateCyl(HalfMatFor(atomA));
                    bv.cyls[i * 2 + 1] = CreateCyl(HalfMatFor(atomB));
                }
                else bv.cyls[i] = CreateCyl(baseMaterial);
            }

            views.Add(bv);
        }
    }

    /// <summary>Orienta y coloca los cilindros. Llamar cada frame: los átomos se mueven
    /// y las líneas paralelas se orientan según la cámara.</summary>
    public void UpdateVisuals()
    {
        if (!cam) cam = Camera.main;

        foreach (var bv in views)
        {
            if (!byId.TryGetValue(bv.a, out var A)) { HideBond(bv); continue; }
            if (!byId.TryGetValue(bv.b, out var B)) { HideBond(bv); continue; }

            Vector3 pa = A.transform.position, pb = B.transform.position;
            Vector3 dir = pb - pa; float len = dir.magnitude;
            if (len < 1e-4f) continue;
            Vector3 dirN = dir / len;

            // Perpendicular al enlace, en el plano de la cámara (para ver las líneas paralelas).
            Vector3 view = cam ? ((pa + pb) * 0.5f - cam.transform.position).normalized : Vector3.forward;
            Vector3 perp = Vector3.Cross(dirN, view);
            if (perp.sqrMagnitude < 1e-4f) perp = Vector3.Cross(dirN, Vector3.up);
            perp = perp.normalized;

            // OJO: el número de LÍNEAS es bv.order, no cyls.Length. Con bicolor
            // hay dos piezas por línea, y usar la longitud del array haría que un
            // enlace doble se dibujara como cuatro líneas separadas.
            int lines = bv.order;

            // Enlaces múltiples: líneas un poco más finas y con separación proporcional
            // al grosor, para que doble/triple siempre se vean como líneas distintas.
            float t = (lines == 1) ? thickness : thickness * 0.72f;
            float sep = Mathf.Max(spacing, t * 2.6f);

            for (int i = 0; i < lines; i++)
            {
                float off = (lines == 1) ? 0f : (i - (lines - 1) * 0.5f) * sep;
                Vector3 a2 = pa + perp * off, b2 = pb + perp * off;

                if (bv.bicolor)
                {
                    Vector3 mid = (a2 + b2) * 0.5f;
                    PlaceSegment(bv.cyls[i * 2],     a2,  mid, dirN, t);
                    PlaceSegment(bv.cyls[i * 2 + 1], mid, b2,  dirN, t);
                }
                else PlaceSegment(bv.cyls[i], a2, b2, dirN, t);
            }
        }
    }

    /// <summary>Enlaces actualmente dibujados.</summary>
    public List<(int a, int b, int order)> ExportBonds()
    {
        var list = new List<(int, int, int)>();
        foreach (var bv in views) list.Add((bv.a, bv.b, bv.order));
        return list;
    }

    public void Clear()
    {
        foreach (var bv in views)
            if (bv.cyls != null)
                foreach (var c in bv.cyls) if (c) Object.Destroy(c);
        views.Clear();
    }

    /// <summary>Libera los cilindros y los materiales cacheados por elemento.</summary>
    public void Dispose()
    {
        Clear();
        foreach (var m in halfMats.Values) if (m) Object.Destroy(m);
        halfMats.Clear();
    }

    /// <summary>Material del lado del enlace que toca a 'atom', cacheado por elemento.</summary>
    Material HalfMatFor(Atom3D atom)
    {
        if (atom == null || baseMaterial == null) return baseMaterial;

        if (halfMats.TryGetValue(atom.atomIndex, out var cached) && cached) return cached;

        var m = new Material(baseMaterial);
        // Se aclara hacia blanco: con el color puro del elemento, los oscuros
        // (el carbono es #4A4E5A) darían enlaces casi negros e ilegibles.
        var c = AtomCatalog.All[atom.atomIndex].color;
        m.SetColor(BaseColorId, Color.Lerp(c, Color.white, 0.30f));

        halfMats[atom.atomIndex] = m;
        return m;
    }

    GameObject CreateCyl(Material mat)
    {
        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = "Bond";
        var col = cyl.GetComponent<Collider>(); if (col) Object.Destroy(col);
        cyl.transform.SetParent(root, false);
        if (mat) cyl.GetComponent<Renderer>().sharedMaterial = mat;
        return cyl;
    }

    static void HideBond(BondView bv)
    {
        if (bv.cyls == null) return;
        foreach (var c in bv.cyls) if (c) c.SetActive(false);
    }

    /// <summary>Coloca un cilindro cubriendo el tramo from→to.</summary>
    static void PlaceSegment(GameObject c, Vector3 from, Vector3 to, Vector3 dirN, float thickness)
    {
        if (!c) return;
        c.SetActive(true);
        c.transform.position   = (from + to) * 0.5f;
        c.transform.up         = dirN;
        // El cilindro primitivo de Unity mide 2 unidades de alto: de ahí el medio.
        c.transform.localScale = new Vector3(thickness, (to - from).magnitude * 0.5f, thickness);
    }
}
