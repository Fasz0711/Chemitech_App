using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Visor 3D de una molécula: construye esferas (átomos) + cilindros (enlaces) a
/// partir de la estructura del diario, los renderiza con una cámara dedicada a un
/// RenderTexture y los muestra en un RawImage. Arrastrar sobre el RawImage rota la
/// molécula. Reutiliza los materiales URP de ZonaJuego (Atom_Base / Bond).
/// Va en el mismo GameObject que el RawImage.
///
/// LLEVA TAMBIÉN LAS CAPAS del plan de validación: símbolos, electronegatividad y tipo
/// de enlace con δ+/δ−. Se encienden por separado. Es el MISMO visor que usa el diario
/// —que las deja apagadas— a propósito: dos visores para lo mismo se separan en cuanto
/// alguien toca uno.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class MoleculeViewer3D : MonoBehaviour, IDragHandler
{
    [SerializeField] private Camera        cam;         // cámara dedicada (renderiza a RT)
    [SerializeField] private Transform     stageRoot;   // contenedor de la molécula (se rota)
    [SerializeField] private Material      atomMaterial; // Atom_Base.mat (URP Lit)
    [SerializeField] private Material      bondMaterial; // Bond.mat
    [SerializeField] private TMP_FontAsset labelFont;
    [SerializeField] private RawImage      target;      // RawImage propio

    [Header("Ajustes")]
    [SerializeField] private int   rtSize        = 800;
    [SerializeField] private float atomScale     = 0.9f;
    [SerializeField] private float bondThickness = 0.14f;
    [SerializeField] private float bondSpacing   = 0.22f;
    [SerializeField] private float fitFactor     = 0.70f;
    [SerializeField] private float rotateSpeed    = 0.35f;
    [SerializeField] private float idleSpin       = 8f;   // grados/seg cuando no se arrastra

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // Colores de la capa de tipo de enlace. El covalente no polar se queda con el gris
    // del material para que los otros dos destaquen contra él y no compitan entre sí.
    static readonly Color POLAR_COLOR = new Color(0.96f, 0.65f, 0.14f);   // ámbar
    static readonly Color IONIC_COLOR = new Color(0.65f, 0.45f, 0.95f);   // violeta

    RenderTexture rt;
    readonly List<Transform>       atomTf   = new List<Transform>();
    readonly List<Transform>       labelTf  = new List<Transform>();
    readonly List<float>           atomRad  = new List<float>();
    bool dragging;

    // Etiquetas de las capas. Cada una se guarda con el átomo al que se pega y cuánto se
    // separa de él, porque la posición se recalcula cada frame contra la cámara.
    struct Pegada { public Transform tf; public int atom; public float up; }
    readonly List<Pegada>  enLabels    = new List<Pegada>();   // electronegatividad
    readonly List<Pegada>  deltaLabels = new List<Pegada>();   // δ+ / δ−
    readonly List<Renderer> bondRend   = new List<Renderer>();
    readonly List<string>   bondKind   = new List<string>();

    // Los enlaces se recolocan cuando un átomo se mueve. Antes se dibujaban una vez y se
    // quedaban donde nacieron, que valía porque la molécula era estática; en una
    // animación un 'move' los dejaría flotando desconectados.
    // El 'kind' va AQUÍ y no en una lista paralela: bondKind es por CILINDRO —un enlace
    // doble añade dos entradas— así que indexarla por enlace se desalinea en cuanto hay
    // un doble, y el enlace siguiente se pintaría del color equivocado.
    struct BondRec { public int a, b, order; public string kind; }
    readonly List<BondRec> bondRecs = new List<BondRec>();

    // Lo que dibuja cada primitiva: el electrón que viaja y las líneas de atracción.
    readonly List<Transform> effectTf = new List<Transform>();

    /// <summary>Capas encendidas. El diario las deja apagadas y se ve como siempre.</summary>
    public bool ShowSymbols          { get; private set; } = true;
    public bool ShowElectronegativity { get; private set; }
    public bool ShowBondTypes        { get; private set; }

    void Reset() { target = GetComponent<RawImage>(); }

    /// <summary>Lo que usa el diario. Desde que el servidor enriqueció el detalle, su
    /// estructura SÍ trae electronegatividad y tipo de enlace, así que las tres capas
    /// funcionan igual que en la pizarra. Si algún día llegara sin ellos, en queda en 0 y
    /// kind vacío, y esas dos capas simplemente no se pueden encender.</summary>
    public void Show(JournalStructure s)
    {
        var atoms = new List<ExplanationContext.Atom>();
        var bonds = new List<ExplanationContext.Bond>();

        if (s?.atoms != null)
            foreach (var a in s.atoms)
            {
                var p = a?.position;
                atoms.Add(new ExplanationContext.Atom
                {
                    element  = a?.type,
                    position = p != null ? new Vector3(p.x, p.y, p.z) : Vector3.zero,
                    en       = a != null ? a.en : 0f,
                    charge   = a != null ? a.charge : 0,
                });
            }

        if (s?.bonds != null)
            foreach (var b in s.bonds)
                bonds.Add(new ExplanationContext.Bond
                {
                    beginAtomId = b.beginAtomId, endAtomId = b.endAtomId,
                    order       = b.order,
                    kind        = b.kind ?? "",
                    negativeEnd = b.negativeEnd,
                });

        Show(atoms, bonds);
    }

    /// <summary>La molécula con toda su química. Las posiciones llegan en las unidades
    /// que sean: aquí se centran y se escalan para que quepa en el visor, así que da
    /// igual si vienen de la pizarra, del diario o del universo.</summary>
    public void Show(IList<ExplanationContext.Atom> atoms, IList<ExplanationContext.Bond> bonds)
    {
        EnsureRenderTexture();
        Clear();
        if (atoms == null || atoms.Count == 0) return;

        int n = atoms.Count;
        var local = new Vector3[n];
        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < n; i++) { local[i] = atoms[i].position; centroid += local[i]; }
        centroid /= n;

        float maxLen = 1e-4f;
        for (int i = 0; i < n; i++)
        {
            local[i] -= centroid;
            maxLen = Mathf.Max(maxLen, local[i].magnitude);
        }

        float targetRadius = VisibleRadius() * fitFactor;
        float scale = (n == 1) ? 0f : targetRadius / maxLen;

        for (int i = 0; i < n; i++)
        {
            SpawnAtom(atoms[i].element, local[i] * scale);
            SpawnEnLabel(i, atoms[i].en);
        }

        if (bonds != null)
            foreach (var b in bonds)
            {
                if (b.beginAtomId < 0 || b.beginAtomId >= n) continue;
                if (b.endAtomId   < 0 || b.endAtomId   >= n) continue;
                int order = Mathf.Clamp(b.order, 1, 3);
                bondRecs.Add(new BondRec { a = b.beginAtomId, b = b.endAtomId, order = order, kind = b.kind ?? "" });
                SpawnBond(atomTf[b.beginAtomId].localPosition, atomTf[b.endAtomId].localPosition,
                          order, b.kind);
                SpawnDeltas(b);
            }

        ApplyLayers();
    }

    // ── Construcción ────────────────────────────────────────────────────────────
    void SpawnAtom(string type, Vector3 localPos)
    {
        int idx = AtomCatalog.IndexOf(type);
        Color col = idx >= 0 ? AtomCatalog.All[idx].color : new Color(0.55f, 0.58f, 0.65f);

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Atom_" + type;
        var sc = sphere.GetComponent<Collider>(); if (sc) Destroy(sc);
        sphere.transform.SetParent(stageRoot, false);
        sphere.layer = stageRoot.gameObject.layer;
        sphere.transform.localPosition = localPos;
        sphere.transform.localScale = Vector3.one * atomScale;

        if (atomMaterial)
        {
            var mat = new Material(atomMaterial);
            mat.SetColor(BaseColorId, col);
            sphere.GetComponent<Renderer>().sharedMaterial = mat;
        }

        atomTf.Add(sphere.transform);
        atomRad.Add(atomScale * 0.5f);

        // Etiqueta (símbolo) — la reposiciona LateUpdate mirando a la cámara
        var lblGo = new GameObject("Label_" + type);
        lblGo.transform.SetParent(stageRoot, false);
        var tmp = lblGo.AddComponent<TextMeshPro>();
        tmp.text = type;
        tmp.font = labelFont;
        tmp.fontSize = (!string.IsNullOrEmpty(type) && type.Length > 1) ? 5.2f : 6.4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        float lum = 0.299f * col.r + 0.587f * col.g + 0.114f * col.b;
        tmp.color = lum > 0.65f ? new Color(0.10f, 0.10f, 0.18f) : Color.white;
        var lrt = tmp.GetComponent<RectTransform>();
        lrt.sizeDelta = new Vector2(4f, 4f);
        labelTf.Add(lblGo.transform);
    }

    void SpawnBond(Vector3 a, Vector3 b, int order, string kind)
    {
        Vector3 dir = b - a;
        float len = dir.magnitude;
        if (len < 1e-4f) return;
        Vector3 dirN = dir / len;

        Vector3 perp = Vector3.Cross(dirN, Vector3.up);
        if (perp.sqrMagnitude < 1e-4f) perp = Vector3.Cross(dirN, Vector3.right);
        perp = perp.normalized;

        float t = order == 1 ? bondThickness : bondThickness * 0.72f;
        float spacing = Mathf.Max(bondSpacing, t * 2.6f);

        for (int i = 0; i < order; i++)
        {
            float off = (order == 1) ? 0f : (i - (order - 1) * 0.5f) * spacing;
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "Bond";
            var cc = cyl.GetComponent<Collider>(); if (cc) Destroy(cc);
            cyl.transform.SetParent(stageRoot, false);
            cyl.layer = stageRoot.gameObject.layer;

            // Instancia propia del material: recolorear el compartido cambiaría TODOS los
            // enlaces del proyecto, y en el Editor se quedaría pegado al asset.
            var rend = cyl.GetComponent<Renderer>();
            if (bondMaterial) rend.sharedMaterial = new Material(bondMaterial);
            bondRend.Add(rend);
            bondKind.Add(kind ?? "");

            Vector3 a2 = a + perp * off, b2 = b + perp * off;
            cyl.transform.localPosition = (a2 + b2) * 0.5f;
            cyl.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dirN);
            cyl.transform.localScale = new Vector3(t, len * 0.5f, t);
        }
    }

    /// <summary>Etiqueta con el valor de Pauling, bajo el símbolo. Un 0 significa que no
    /// se sabe —el diario no manda electronegatividad— y entonces no se crea nada: mejor
    /// sin etiqueta que con un "0.00" que parece un dato.</summary>
    void SpawnEnLabel(int atomIndex, float en)
    {
        if (en <= 0f) return;

        var go = new GameObject("EN");
        go.transform.SetParent(stageRoot, false);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = en.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        tmp.font = labelFont;
        tmp.fontSize = 3.6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.75f, 0.92f, 1f);
        tmp.GetComponent<RectTransform>().sizeDelta = new Vector2(6f, 3f);

        enLabels.Add(new Pegada { tf = go.transform, atom = atomIndex, up = -0.52f });
    }

    /// <summary>δ− en el extremo que el servidor marca, δ+ en el otro.
    ///
    /// 'negativeEnd' es el ÍNDICE DEL ÁTOMO dentro de la molécula, no un booleano: por eso
    /// hay que compararlo con los dos extremos en vez de suponer que el δ− va siempre en
    /// el primero. Si no es polar llega -1 y no se dibuja nada.</summary>
    void SpawnDeltas(ExplanationContext.Bond b)
    {
        if (b.negativeEnd < 0) return;
        if (b.negativeEnd != b.beginAtomId && b.negativeEnd != b.endAtomId) return;

        int positive = (b.negativeEnd == b.beginAtomId) ? b.endAtomId : b.beginAtomId;
        SpawnDelta(b.negativeEnd, "δ-");
        SpawnDelta(positive,      "δ+");
    }

    void SpawnDelta(int atomIndex, string text)
    {
        if (atomIndex < 0 || atomIndex >= atomTf.Count) return;

        var go = new GameObject("Delta");
        go.transform.SetParent(stageRoot, false);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.font = labelFont;   // la δ sale de la fuente de reserva; sin ella, un hueco
        tmp.fontSize = 4.4f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = text.EndsWith("-") ? new Color(1f, 0.45f, 0.45f)
                                       : new Color(0.55f, 0.75f, 1f);
        tmp.GetComponent<RectTransform>().sizeDelta = new Vector2(5f, 3f);

        deltaLabels.Add(new Pegada { tf = go.transform, atom = atomIndex, up = 0.60f });
    }

    // ── Piezas para la animación ──────────────────────────────────────────────

    /// <summary>Cuántos átomos hay dibujados. Lo consulta el reproductor para descartar
    /// un guion cuyos índices no cuadren en vez de reventar a media explicación.</summary>
    public int AtomCount => atomTf.Count;

    /// <summary>Mueve un átomo. Los enlaces se rehacen solos.</summary>
    public void SetAtomPosition(int index, Vector3 localPos)
    {
        if (index < 0 || index >= atomTf.Count || !atomTf[index]) return;
        atomTf[index].localPosition = localPos;
        RebuildBondTransforms();
    }

    public Vector3 GetAtomPosition(int index)
        => (index >= 0 && index < atomTf.Count && atomTf[index]) ? atomTf[index].localPosition : Vector3.zero;

    /// <summary>Recoloca los cilindros según dónde estén AHORA los átomos.
    ///
    /// Se destruyen y se vuelven a crear en vez de moverlos: un enlace doble son dos
    /// cilindros separados por una perpendicular que cambia al girar el enlace, y
    /// recalcularla a mano para cada uno sería reescribir SpawnBond peor.</summary>
    void RebuildBondTransforms()
    {
        foreach (var r in bondRend) if (r) Destroy(r.gameObject);
        bondRend.Clear();
        bondKind.Clear();

        foreach (var rec in bondRecs)
        {
            if (rec.a < 0 || rec.a >= atomTf.Count || rec.b < 0 || rec.b >= atomTf.Count) continue;
            SpawnBond(atomTf[rec.a].localPosition, atomTf[rec.b].localPosition, rec.order, rec.kind);
        }
        ApplyLayers();
    }

    /// <summary>Cambia un enlace: lo crea, le cambia el orden, o lo quita con order 0.</summary>
    public void SetBond(int a, int b, int order, string kind)
    {
        int at = bondRecs.FindIndex(r => (r.a == a && r.b == b) || (r.a == b && r.b == a));

        // Al cambiar de orden se conserva el tipo que ya tenía si no llega uno nuevo: un
        // paso que solo dice "ahora es doble" no debería despintar un enlace polar.
        string keep = (at >= 0 && string.IsNullOrEmpty(kind)) ? bondRecs[at].kind : (kind ?? "");

        if (order <= 0) { if (at >= 0) bondRecs.RemoveAt(at); }
        else if (at >= 0) bondRecs[at] = new BondRec { a = a, b = b, order = order, kind = keep };
        else              bondRecs.Add(new BondRec { a = a, b = b, order = order, kind = keep });

        RebuildBondTransforms();
    }

    /// <summary>El electrón que viaja de un átomo a otro. Es lo que hace visible la
    /// palabra "entrega" del guion: el sodio ENTREGA su electrón al cloro.</summary>
    public Transform SpawnElectron(Vector3 at)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Electron";
        var c = go.GetComponent<Collider>(); if (c) Destroy(c);
        go.transform.SetParent(stageRoot, false);
        go.layer = stageRoot.gameObject.layer;
        go.transform.localPosition = at;
        go.transform.localScale = Vector3.one * 0.26f;

        if (atomMaterial)
        {
            var mat = new Material(atomMaterial);
            mat.SetColor(BaseColorId, new Color(0.55f, 0.85f, 1f));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor(Shader.PropertyToID("_EmissionColor"), new Color(0.35f, 0.75f, 1f) * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        effectTf.Add(go.transform);
        return go.transform;
    }

    /// <summary>La línea punteada de una atracción. NO es un enlace, y esa distinción es
    /// justo lo que la lección enseña en el puente de hidrógeno: se dibuja como una fila
    /// de puntos, no como un cilindro.</summary>
    public void SpawnAttraction(int a, int b)
    {
        if (a < 0 || a >= atomTf.Count || b < 0 || b >= atomTf.Count) return;
        Vector3 pa = atomTf[a].localPosition, pb = atomTf[b].localPosition;

        float len = Vector3.Distance(pa, pb);
        int dots = Mathf.Clamp(Mathf.RoundToInt(len / 0.22f), 3, 14);

        for (int i = 1; i < dots; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "AttractDot";
            var c = go.GetComponent<Collider>(); if (c) Destroy(c);
            go.transform.SetParent(stageRoot, false);
            go.layer = stageRoot.gameObject.layer;
            go.transform.localPosition = Vector3.Lerp(pa, pb, i / (float)dots);
            go.transform.localScale = Vector3.one * 0.07f;

            if (bondMaterial)
            {
                var mat = new Material(bondMaterial);
                mat.SetColor(BaseColorId, new Color(0.95f, 0.85f, 0.45f));
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            effectTf.Add(go.transform);
        }
    }

    /// <summary>Quita electrones y líneas de atracción, dejando la molécula. Lo usa el
    /// reproductor al rebobinar: los efectos son de un paso, no del estado.</summary>
    public void ClearEffects()
    {
        foreach (var t in effectTf) if (t) Destroy(t.gameObject);
        effectTf.Clear();
    }

    // ── Capas ─────────────────────────────────────────────────────────────────

    public void SetLayers(bool symbols, bool electronegativity, bool bondTypes)
    {
        ShowSymbols           = symbols;
        ShowElectronegativity = electronegativity;
        ShowBondTypes         = bondTypes;
        ApplyLayers();
    }

    void ApplyLayers()
    {
        foreach (var t in labelTf) if (t) t.gameObject.SetActive(ShowSymbols);
        foreach (var p in enLabels)    if (p.tf) p.tf.gameObject.SetActive(ShowElectronegativity);
        foreach (var p in deltaLabels) if (p.tf) p.tf.gameObject.SetActive(ShowBondTypes);

        for (int i = 0; i < bondRend.Count; i++)
        {
            if (!bondRend[i] || bondRend[i].sharedMaterial == null) continue;
            Color c = Color.white;
            if (ShowBondTypes)
            {
                if      (bondKind[i] == "polar") c = POLAR_COLOR;
                else if (bondKind[i] == "ionic") c = IONIC_COLOR;
            }
            bondRend[i].sharedMaterial.SetColor(BaseColorId, c);
        }
    }

    /// <summary>¿Hay datos para encender cada capa? Lo consulta la pantalla para no
    /// ofrecer un interruptor que no va a hacer nada: desde el diario no llega
    /// electronegatividad, y una molécula sin enlaces polares no tiene δ que mostrar.</summary>
    public bool HasElectronegativity => enLabels.Count > 0;
    public bool HasBondTypes         => deltaLabels.Count > 0;

    public void Clear()
    {
        if (stageRoot == null) return;
        for (int i = stageRoot.childCount - 1; i >= 0; i--)
            Destroy(stageRoot.GetChild(i).gameObject);
        atomTf.Clear(); labelTf.Clear(); atomRad.Clear();
        enLabels.Clear(); deltaLabels.Clear(); bondRend.Clear(); bondKind.Clear();
        bondRecs.Clear(); effectTf.Clear();
        if (stageRoot) stageRoot.localRotation = Quaternion.identity;
    }

    // ── Runtime ──────────────────────────────────────────────────────────────────
    void LateUpdate()
    {
        if (cam == null || stageRoot == null) return;

        if (!dragging && idleSpin != 0f)
            stageRoot.Rotate(cam.transform.up, idleSpin * Time.deltaTime, Space.World);

        // Etiquetas: al frente de la esfera, mirando a la cámara (billboard como ZonaJuego)
        Vector3 camPos = cam.transform.position;
        Vector3 camFwd = cam.transform.forward;
        Vector3 camUp  = cam.transform.up;

        for (int i = 0; i < labelTf.Count && i < atomTf.Count; i++)
        {
            if (!labelTf[i] || !atomTf[i]) continue;
            labelTf[i].position = atomTf[i].position - camFwd * (atomRad[i] + 0.06f);
            labelTf[i].rotation = Quaternion.LookRotation(labelTf[i].position - camPos);
        }

        // Las de las capas van pegadas a su átomo pero desplazadas EN PANTALLA, no en el
        // mundo: así el símbolo, su electronegatividad y su δ nunca se tapan entre ellos
        // por mucho que se gire la molécula.
        PlaceStuck(enLabels,    camPos, camFwd, camUp);
        PlaceStuck(deltaLabels, camPos, camFwd, camUp);
    }

    void PlaceStuck(List<Pegada> list, Vector3 camPos, Vector3 camFwd, Vector3 camUp)
    {
        foreach (var p in list)
        {
            if (!p.tf || p.atom < 0 || p.atom >= atomTf.Count || !atomTf[p.atom]) continue;
            p.tf.position = atomTf[p.atom].position
                          - camFwd * (atomRad[p.atom] + 0.06f)
                          + camUp  * p.up;
            p.tf.rotation = Quaternion.LookRotation(p.tf.position - camPos);
        }
    }

    public void OnDrag(PointerEventData e)
    {
        if (stageRoot == null || cam == null) return;
        stageRoot.Rotate(cam.transform.up,    -e.delta.x * rotateSpeed, Space.World);
        stageRoot.Rotate(cam.transform.right,  e.delta.y * rotateSpeed, Space.World);
    }

    // La cámara del visor solo renderiza mientras el visor se ve. En el diario da igual
    // —es la única cámara de esa escena— pero en la pizarra está abierta una clase
    // detrás, y un segundo render a textura cada frame para mostrar un escenario vacío
    // es coste puro.
    void OnEnable()  { dragging = false; if (cam) cam.enabled = true; }
    void OnDisable() { if (cam) cam.enabled = false; }
    void Update()    { dragging = Input.GetMouseButton(0) && (EventSystem.current && EventSystem.current.IsPointerOverGameObject()); }

    // ── Helpers ──────────────────────────────────────────────────────────────────
    void EnsureRenderTexture()
    {
        if (!target) target = GetComponent<RawImage>();
        if (rt != null) return;
        rt = new RenderTexture(rtSize, rtSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        rt.Create();
        if (cam) cam.targetTexture = rt;
        if (target) target.texture = rt;
    }

    float VisibleRadius()
    {
        if (cam == null || stageRoot == null) return 2f;
        float dist = Vector3.Distance(cam.transform.position, stageRoot.position);
        return dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
    }

    void OnDestroy()
    {
        if (cam) cam.targetTexture = null;
        if (rt) { rt.Release(); Destroy(rt); }
    }
}
