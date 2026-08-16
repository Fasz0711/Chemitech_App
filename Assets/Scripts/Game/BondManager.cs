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
    class BondView { public int a, b, order; public GameObject[] cyls; }
    readonly List<BondView> bondViews = new List<BondView>();

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
            HideBanner();
            return;
        }

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
        if (batch != currentBatch) return; // batch viejo (la estructura ya cambió)

        // Conexión con el servicio de IA: error de red/timeout/5xx → offline; cualquier
        // respuesta del servidor (válida o no) → online.
        SetOnline(!connectionError);

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
        foreach (var (a, b, order) in batchBonds)
        {
            int n = Mathf.Clamp(order, 1, 3);
            var bv = new BondView { a = a, b = b, order = n, cyls = new GameObject[n] };
            for (int i = 0; i < n; i++) bv.cyls[i] = CreateCyl();
            bondViews.Add(bv);
        }
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

    GameObject CreateCyl()
    {
        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = "Bond";
        var col = cyl.GetComponent<Collider>(); if (col) Destroy(col);
        cyl.transform.SetParent(bondsRoot, false);
        if (bondMaterial) cyl.GetComponent<Renderer>().sharedMaterial = bondMaterial;
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

            int n = bv.cyls.Length;
            // Enlaces múltiples: líneas un poco más finas y con separación proporcional
            // al grosor, para que doble/triple siempre se vean como líneas distintas.
            float t = (n == 1) ? bondThickness : bondThickness * 0.72f;
            float spacing = Mathf.Max(bondSpacing, t * 2.6f);
            for (int i = 0; i < n; i++)
            {
                var c = bv.cyls[i]; if (!c) continue;
                c.SetActive(true);
                float off = (n == 1) ? 0f : (i - (n - 1) * 0.5f) * spacing;
                Vector3 a2 = pa + perp * off, b2 = pb + perp * off;
                c.transform.position = (a2 + b2) * 0.5f;
                c.transform.up = dirN;
                c.transform.localScale = new Vector3(t, len * 0.5f, t);
            }
        }
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
        if (online == value) return;
        online = value;
        if (detectionOfflineIndicator) detectionOfflineIndicator.SetActive(!online);
        Debug.Log($"[Detect] Servicio de detección: {(online ? "DISPONIBLE" : "NO DISPONIBLE")}");
    }
}
