using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Detección de moléculas: cuando la estructura de átomos se estabiliza, agrupa
/// los átomos por CERCANÍA (solo para separar candidatos; NO decide enlaces),
/// envía cada grupo al backend con bonds vacío, y DIBUJA los enlaces reales que
/// devuelve el backend (con su orden: simple/doble/triple).
/// Banner: azul "Detectando…" → verde "¡Molécula formada!".
/// </summary>
public class BondManager : MonoBehaviour
{
    [SerializeField] private AtomPlacementController placement;
    [SerializeField] private Material        bondMaterial;
    [SerializeField] private GameObject      bannerRoot;
    [SerializeField] private Image           bannerBg;
    [SerializeField] private TextMeshProUGUI bannerText;
    [SerializeField] private GameObject      detectionOfflineIndicator; // aviso discreto "detección no disponible"

    [Header("Ajustes")]
    [SerializeField] private float clusterDistance = 2.0f;  // agrupa candidatos (no decide enlaces)
    [SerializeField] private float bondThickness   = 0.09f;
    [SerializeField] private float bondSpacing     = 0.20f; // separación entre líneas paralelas (orden 2/3)
    [SerializeField] private float detectDelay     = 0.45f; // debounce tras el último cambio
    [SerializeField] private float offlineRetryInterval = 5f; // reintento de detección mientras el servicio está caído
    [SerializeField] private float offlineIndicatorDuration = 3f; // tiempo que el aviso offline permanece visible (segundos)
    [SerializeField] private float formedBannerSeconds  = 2f; // el banner "¡Molécula formada!" se auto-oculta tras esto

    static readonly Color C_DETECT = new Color(0.10f, 0.65f, 0.81f, 1f);
    static readonly Color C_OK     = new Color(0.18f, 0.80f, 0.44f, 1f);

    /// <summary>Se dispara al detectar una molécula por PRIMERA vez para el usuario
    /// (isNewDiscovery del backend). Args: (fórmula, nombre). Lo usa el modal de
    /// "¡Nuevo descubrimiento!" en ZonaJuegoManager.</summary>
    public event System.Action<string, string> OnNewDiscovery;

    Transform bondsRoot;
    Camera    cam;

    readonly List<Atom3D>            atomsBuf = new List<Atom3D>();
    readonly Dictionary<int, Atom3D> byId     = new Dictionary<int, Atom3D>();

    // Enlaces actualmente dibujados (los devueltos por el backend).
    //
    // 'order' es cuántas LÍNEAS paralelas tiene el enlace (simple/doble/triple).
    // 'cyls' tiene una pieza por línea, o dos si el enlace es bicolor: cada mitad
    // lleva el color de su átomo, como en cualquier visor molecular. Con bicolor
    // el índice de la línea i son cyls[i*2] (lado A) y cyls[i*2+1] (lado B).
    class BondView { public int a, b, order; public GameObject[] cyls; public bool bicolor; }
    readonly List<BondView> bondViews = new List<BondView>();

    // Un material por elemento, reutilizado entre todos sus enlaces: crear uno
    // por mitad dispararía el número de materiales en moléculas grandes.
    readonly Dictionary<int, Material> halfMats = new Dictionary<int, Material>();
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // Estado de detección
    string lastHash = "", sentHash = "";
    float  lastChangeTime;
    bool   detecting;
    int    pendingRequests, currentBatch;
    bool   anyValid;

    // Firma de los enlaces ya dibujados al iniciar un batch: sirve para saber si la
    // detección REALMENTE formó/cambió una molécula o solo reconfirmó una existente.
    string    prevBondsSig = "";
    Coroutine bannerHideCo;

    // Un descubrimiento nuevo ya suena con su propia fanfarria; sin esta marca el
    // batch encadenaría también el chime de "molécula formada" encima.
    bool newDiscoveryThisBatch;

    // Estado de conexión con el servicio de IA
    bool  online = true;
    float lastAttemptTime;
    float offlineIndicatorShowTime; // cuándo se mostró el indicador (para auto-ocultarlo después de offlineIndicatorDuration)

    // ¿Hay ahora mismo algo que detectar (un grupo de ≥2 átomos)? El aviso de
    // "servicio no disponible" solo se muestra si lo hay: sin nada que detectar
    // no hemos podido comprobar el servicio, así que afirmar que está caído sería
    // decir lo que no sabemos, y además no le sirve de nada al jugador.
    bool hasDetectableStructure;
    readonly List<(int a, int b, int order)> batchBonds = new List<(int, int, int)>();

    void Awake()
    {
        bondsRoot = new GameObject("Bonds").transform;
        cam = Camera.main;
    }

    void Start()
    {
        if (bannerRoot) bannerRoot.SetActive(false);
        if (detectionOfflineIndicator) detectionOfflineIndicator.SetActive(false);
    }

    void Update()
    {
        GatherAtoms(placement ? placement.AtomsRoot : null);
        DetectionStep();
        UpdateBondVisuals();
    }

    void GatherAtoms(Transform root)
    {
        atomsBuf.Clear();
        byId.Clear();
        if (!root) return;
        foreach (Transform c in root)
        {
            var a = c.GetComponent<Atom3D>();
            if (a) { atomsBuf.Add(a); byId[a.id] = a; }
        }
    }

    // ── Detección ─────────────────────────────────────────────────────────────
    void DetectionStep()
    {
        if (atomsBuf.Count == 0)
        {
            sentHash = ""; lastHash = "";
            ClearBondViews(); batchBonds.Clear();
            SetDetectableStructure(false);   // tablero vacío: no hay nada que comprobar
            if (!detecting) HideBanner();
            return;
        }

        string hash = StructureHash();
        if (hash != lastHash) { lastHash = hash; lastChangeTime = Time.time; }

        // Offline: reintenta el estado ACTUAL cada offlineRetryInterval para
        // auto-recuperarse cuando el servicio de IA vuelva (sin requerir interacción).
        if (!online && !detecting && Time.time - lastAttemptTime >= offlineRetryInterval)
            sentHash = "";

        if (hash != sentHash && Time.time - lastChangeTime >= detectDelay)
        {
            sentHash = hash;
            StartDetection();
        }
    }

    void StartDetection()
    {
        lastAttemptTime = Time.time;   // marca el intento (aunque no haya candidatos)
        prevBondsSig = BondsSignature(ExportBonds()); // enlaces dibujados ANTES de este batch

        var clusters = ClusterAtoms();
        // Solo grupos con ≥2 átomos son candidatos a molécula.
        var candidates = clusters.FindAll(c => c.Count >= 2);
        if (candidates.Count == 0)
        {
            ClearBondViews(); batchBonds.Clear();
            SetDetectableStructure(false);   // átomos sueltos: no hay molécula candidata
            HideBanner();
            return;
        }

        SetDetectableStructure(true);
        currentBatch++;
        int batch = currentBatch;
        pendingRequests = candidates.Count;
        anyValid = false;
        detecting = true;
        newDiscoveryThisBatch = false;
        batchBonds.Clear();
        ShowBanner("Detectando interacción atómica…", C_DETECT);
        Debug.Log($"[Detect] {candidates.Count} candidato(s) · userId='{SessionData.UserId}'");

        foreach (var cluster in candidates)
        {
            // Ids locales 0..n-1 (el backend los referencia así) + mapa a los Atom3D reales.
            var map = new Atom3D[cluster.Count];
            var atomsDTO = new ApiManager.AtomDTO[cluster.Count];
            for (int i = 0; i < cluster.Count; i++)
            {
                map[i] = cluster[i];
                var p = cluster[i].transform.position;
                atomsDTO[i] = new ApiManager.AtomDTO { id = i, element = cluster[i].element, x = p.x, y = p.y, z = p.z };
            }

            var capturedMap = map;
            ApiManager.Instance.DetectMolecule(SessionData.UserId, atomsDTO, new ApiManager.BondDTO[0],
                onSuccess: resp => OnClusterResult(batch, resp, capturedMap, false),
                onError:   (code, detail) =>
                {
                    Debug.LogWarning($"[Detect] Error {code}: {detail}");
                    bool connErr = (code == 0 || code >= 500);  // sin respuesta / timeout / servicio caído
                    OnClusterResult(batch, null, capturedMap, connErr);
                });
        }
    }

    void OnClusterResult(int batch, ApiManager.DetectResponse resp, Atom3D[] map, bool connectionError)
    {
        // Conexión con el servicio de IA: error de red/timeout/5xx → offline; cualquier
        // respuesta del servidor (válida o no) → online.
        //
        // Va ANTES del descarte por batch obsoleto a propósito: que la estructura
        // haya cambiado no invalida lo que acabamos de aprender sobre el servicio,
        // y tirar esa información retrasaba innecesariamente ocultar el aviso.
        SetOnline(!connectionError);

        if (batch != currentBatch) return; // batch viejo (la estructura ya cambió)

        bool valid = resp != null && resp.isValid && resp.molecule != null;
        if (valid)
        {
            anyValid = true;
            var m = resp.molecule;
            Debug.Log($"[Detect] {m.name} ({m.molecularFormula}) · enlaces={m.bonds?.Length ?? 0}");
            if (m.bonds != null)
                foreach (var bd in m.bonds)
                {
                    // bd.begin/endAtomId son índices locales del request → Atom3D real.
                    if (bd.beginAtomId >= 0 && bd.beginAtomId < map.Length &&
                        bd.endAtomId   >= 0 && bd.endAtomId   < map.Length)
                        batchBonds.Add((map[bd.beginAtomId].id, map[bd.endAtomId].id, bd.order));
                }
            RebuildBondViews();

            // Primera vez que se descubre esta molécula → avisar para el modal.
            if (m.isNewDiscovery)
            {
                newDiscoveryThisBatch = true;
                AudioManager.Instance.PlayDiscovery();
                OnNewDiscovery?.Invoke(m.molecularFormula, m.name);
            }
        }

        pendingRequests--;
        if (pendingRequests <= 0)
        {
            detecting = false;
            // Solo celebramos si los enlaces CAMBIARON respecto a lo ya dibujado. Reconfirmar
            // una molécula que ya existía (p. ej. al colocar un átomo suelto aparte, o tras
            // recargar el universo) no vuelve a mostrar el banner.
            if (anyValid && BondsSignature(batchBonds) != prevBondsSig) ShowMoleculeFormed();
            else                                                        HideBanner();
        }
    }

    // ── Agrupamiento por cercanía (componentes por distancia; NO decide enlaces) ─
    List<List<Atom3D>> ClusterAtoms()
    {
        var parent = new Dictionary<int, int>();
        foreach (var a in atomsBuf) parent[a.id] = a.id;

        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int x, int y) { parent[Find(x)] = Find(y); }

        float d2 = clusterDistance * clusterDistance;
        for (int i = 0; i < atomsBuf.Count; i++)
            for (int j = i + 1; j < atomsBuf.Count; j++)
                if ((atomsBuf[i].transform.position - atomsBuf[j].transform.position).sqrMagnitude <= d2)
                    Union(atomsBuf[i].id, atomsBuf[j].id);

        var groups = new Dictionary<int, List<Atom3D>>();
        foreach (var a in atomsBuf)
        {
            int r = Find(a.id);
            if (!groups.TryGetValue(r, out var g)) { g = new List<Atom3D>(); groups[r] = g; }
            g.Add(a);
        }
        return new List<List<Atom3D>>(groups.Values);
    }

    // ── Dibujo de enlaces (según orden, orientados a la cámara) ────────────────
    void RebuildBondViews()
    {
        ClearBondViews();

        // En Calidad = Bajo se mantiene el cilindro gris de una pieza: partir cada
        // enlace duplica los objetos, y un triple pasaría de 3 piezas a 6.
        bool bicolor = GraphicsManager.Instance.Quality != GraphicsLevel.Bajo;

        foreach (var (a, b, order) in batchBonds)
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
                else bv.cyls[i] = CreateCyl(bondMaterial);
            }

            bondViews.Add(bv);
        }
    }

    /// <summary>Material del lado del enlace que toca a 'atom', cacheado por elemento.</summary>
    Material HalfMatFor(Atom3D atom)
    {
        if (atom == null || bondMaterial == null) return bondMaterial;

        if (halfMats.TryGetValue(atom.atomIndex, out var cached) && cached) return cached;

        var m = new Material(bondMaterial);
        // Se aclara hacia blanco: con el color puro del elemento, los oscuros
        // (el carbono es #4A4E5A) darían enlaces casi negros e ilegibles.
        var c = AtomCatalog.All[atom.atomIndex].color;
        m.SetColor(BaseColorId, Color.Lerp(c, Color.white, 0.30f));

        halfMats[atom.atomIndex] = m;
        return m;
    }

    void OnDestroy()
    {
        foreach (var m in halfMats.Values) if (m) Destroy(m);
        halfMats.Clear();
    }

    void ClearBondViews()
    {
        foreach (var bv in bondViews)
            if (bv.cyls != null)
                foreach (var c in bv.cyls) if (c) Destroy(c);
        bondViews.Clear();
    }

    static void HideBond(BondView bv)
    {
        if (bv.cyls == null) return;
        foreach (var c in bv.cyls) if (c) c.SetActive(false);
    }

    // ── Guardar / restaurar enlaces (para no re-descubrir al recargar) ─────────
    public List<(int a, int b, int order)> ExportBonds()
    {
        var list = new List<(int, int, int)>();
        foreach (var bv in bondViews) list.Add((bv.a, bv.b, bv.order));
        return list;
    }

    /// <summary>
    /// Dibuja enlaces cargados SIN llamar al backend, y marca la estructura actual
    /// como "ya detectada" para que no se re-descubra la misma molécula al entrar.
    /// </summary>
    public void ImportBonds(List<(int a, int b, int order)> bonds)
    {
        GatherAtoms(placement ? placement.AtomsRoot : null);
        batchBonds.Clear();
        if (bonds != null) batchBonds.AddRange(bonds);
        RebuildBondViews();
        string h = StructureHash();
        lastHash = h;
        sentHash = h;   // hash == sentHash → DetectionStep no vuelve a detectar
    }

    GameObject CreateCyl(Material mat)
    {
        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = "Bond";
        var col = cyl.GetComponent<Collider>(); if (col) Destroy(col);
        cyl.transform.SetParent(bondsRoot, false);
        if (mat) cyl.GetComponent<Renderer>().sharedMaterial = mat;
        return cyl;
    }

    void UpdateBondVisuals()
    {
        if (!cam) cam = Camera.main;
        foreach (var bv in bondViews)
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
            float t = (lines == 1) ? bondThickness : bondThickness * 0.72f;
            float spacing = Mathf.Max(bondSpacing, t * 2.6f);

            for (int i = 0; i < lines; i++)
            {
                float off = (lines == 1) ? 0f : (i - (lines - 1) * 0.5f) * spacing;
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

    // ── Hash de estructura (solo átomos: los enlaces los da el backend) ────────
    string StructureHash()
    {
        atomsBuf.Sort((x, y) => x.id.CompareTo(y.id));
        var sb = new StringBuilder();
        foreach (var a in atomsBuf)
        {
            var p = a.transform.position;
            sb.Append(a.id).Append(a.element)
              .Append(Mathf.RoundToInt(p.x * 10f)).Append(',')
              .Append(Mathf.RoundToInt(p.y * 10f)).Append(',')
              .Append(Mathf.RoundToInt(p.z * 10f)).Append(';');
        }
        return sb.ToString();
    }

    // ── Banner ────────────────────────────────────────────────────────────────
    void ShowBanner(string msg, Color col)
    {
        CancelBannerHide();
        if (bannerRoot) bannerRoot.SetActive(true);
        if (bannerBg)   bannerBg.color = col;
        if (bannerText) bannerText.text = msg;
    }

    // Banner verde de éxito: se muestra y se auto-oculta tras formedBannerSeconds.
    void ShowMoleculeFormed()
    {
        ShowBanner("¡Molécula formada!", C_OK);
        if (!newDiscoveryThisBatch) AudioManager.Instance.PlayMoleculeFormed();
        EmitBondPulses();
        bannerHideCo = StartCoroutine(HideBannerAfter(formedBannerSeconds));
    }

    // Un pulso por enlace de la molécula recién formada. Solo se llama desde
    // ShowMoleculeFormed, que ya comprobó que los enlaces CAMBIARON: reconfirmar
    // una molécula existente no vuelve a lanzar el efecto.
    void EmitBondPulses()
    {
        var fx = ZoneEffects.Instance;
        if (fx == null) return;

        foreach (var (a, b, _) in batchBonds)
            if (byId.TryGetValue(a, out var A) && byId.TryGetValue(b, out var B))
                fx.BondFormed(A.transform.position, B.transform.position);
    }

    IEnumerator HideBannerAfter(float secs)
    {
        yield return new WaitForSeconds(secs);
        bannerHideCo = null;
        if (bannerRoot) bannerRoot.SetActive(false);
    }

    void HideBanner()
    {
        CancelBannerHide();
        if (bannerRoot) bannerRoot.SetActive(false);
    }

    void CancelBannerHide()
    {
        if (bannerHideCo != null) { StopCoroutine(bannerHideCo); bannerHideCo = null; }
    }

    // Firma normalizada de un conjunto de enlaces (independiente del orden de la lista
    // y de la dirección a↔b): permite comparar si la molécula dibujada cambió.
    static string BondsSignature(List<(int a, int b, int order)> bonds)
    {
        if (bonds == null || bonds.Count == 0) return "";
        var parts = new List<string>(bonds.Count);
        foreach (var (a, b, order) in bonds)
        {
            int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            parts.Add($"{lo}-{hi}:{order}");
        }
        parts.Sort();
        return string.Join(",", parts);
    }

    // ── Conexión con el servicio de detección ──────────────────────────────────
    void SetOnline(bool value)
    {
        if (online != value)
        {
            online = value;
            Debug.Log($"[Detect] Servicio de detección: {(online ? "DISPONIBLE" : "NO DISPONIBLE")}");
        }
        RefreshOfflineIndicator();
    }

    /// <summary>
    /// El aviso se muestra solo si el servicio falló Y hay una estructura que
    /// detectar, pero AUTO-SE OCULTA después de offlineIndicatorDuration segundos
    /// (para no ser demasiado intrusivo). Antes dependía únicamente de 'online', y
    /// como 'online' solo se puede volver a poner en true desde una respuesta del
    /// servidor —que exige un grupo de ≥2 átomos— el aviso se quedaba fijo para
    /// siempre en cuanto el jugador borraba o separaba sus átomos.
    /// </summary>
    void RefreshOfflineIndicator()
    {
        if (!detectionOfflineIndicator) return;

        bool shouldShow = !online && hasDetectableStructure;
        bool isShown = detectionOfflineIndicator.activeSelf;

        // Si debe mostrarse y no está visible → mostrar y registrar el tiempo
        if (shouldShow && !isShown)
        {
            detectionOfflineIndicator.SetActive(true);
            offlineIndicatorShowTime = Time.time;
        }
        // Si está visible pero ya pasó el tiempo o no debe mostrarse → ocultar
        else if (isShown && (!shouldShow || Time.time - offlineIndicatorShowTime >= offlineIndicatorDuration))
        {
            detectionOfflineIndicator.SetActive(false);
        }
    }

    /// <summary>Marca si hay algo que detectar y refresca el aviso en consecuencia.</summary>
    void SetDetectableStructure(bool value)
    {
        if (hasDetectableStructure == value) return;
        hasDetectableStructure = value;
        RefreshOfflineIndicator();
    }
}
