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

    /// <summary>Detectar SIN escribir en el diario. La pizarra del docente lo apaga:
    /// está dando clase, no jugando, y su diario no debería llenarse de moléculas de
    /// demostración. Con esto en false, isNewDiscovery llega siempre false y el modal
    /// de descubrimiento no se dispara.</summary>
    public bool RecordDiscoveries { get; set; } = true;

    /// <summary>El corte con el que se agrupan candidatos. Lo lee la pizarra para contar
    /// fragmentos con el mismo criterio con el que se mandan a detectar.</summary>
    public float ClusterDistance => clusterDistance;

    /// <summary>Se dispara al detectar una molécula por PRIMERA vez para el usuario
    /// (isNewDiscovery del backend). Args: (fórmula, nombre). Lo usa el modal de
    /// "¡Nuevo descubrimiento!" en ZonaJuegoManager.</summary>
    public event System.Action<string, string> OnNewDiscovery;

    // Dibuja los enlaces que devuelve el backend. Vive aparte para que la escena
    // de clase pueda pintar enlaces ya resueltos sin arrastrar la detección.
    BondRenderer bondRenderer;

    readonly List<Atom3D>            atomsBuf = new List<Atom3D>();
    readonly Dictionary<int, Atom3D> byId     = new Dictionary<int, Atom3D>();

    // Estado de detección
    string lastHash = "", sentHash = "";
    float  lastChangeTime;
    bool   detecting;
    int    pendingRequests, currentBatch;
    bool   anyValid;     // algún cluster devolvió una molécula COMPLETA
    bool   anyResponse;  // algún cluster recibió respuesta del servidor (no fue todo red caída)

    // Firma de los enlaces de moléculas COMPLETAS del batch anterior: sirve para saber si
    // la detección REALMENTE formó algo o solo reconfirmó lo que ya había. Solo cuenta lo
    // válido: una molécula a medias cambia los enlaces dibujados en cada paso intermedio,
    // y celebrar eso sería un falso positivo.
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
    // batchBonds:      TODO lo que se dibuja, incluidas las moléculas a medias.
    // batchValidBonds: solo lo de moléculas completas → decide el banner y los pulsos.
    readonly List<(int a, int b, int order)> batchBonds      = new List<(int, int, int)>();
    readonly List<(int a, int b, int order)> batchValidBonds = new List<(int, int, int)>();

    void Awake()
    {
        var bondsRoot = new GameObject("Bonds").transform;
        bondRenderer = new BondRenderer(bondsRoot, bondMaterial, byId, bondThickness, bondSpacing);
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
        bondRenderer.UpdateVisuals();
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
            ClearDrawnBonds();
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

        var clusters = ClusterAtoms();
        // Solo grupos con ≥2 átomos son candidatos a molécula.
        var candidates = clusters.FindAll(c => c.Count >= 2);
        if (candidates.Count == 0)
        {
            ClearDrawnBonds();
            SetDetectableStructure(false);   // átomos sueltos: no hay molécula candidata
            HideBanner();
            return;
        }

        SetDetectableStructure(true);
        currentBatch++;
        int batch = currentBatch;
        pendingRequests = candidates.Count;
        anyValid = false;
        anyResponse = false;
        detecting = true;
        newDiscoveryThisBatch = false;
        batchBonds.Clear();
        batchValidBonds.Clear();
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
                record: RecordDiscoveries,
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

        if (resp != null) anyResponse = true;

        // 'bonds' de primer nivel se DIBUJA SIEMPRE, válida o no. El alumno arma la
        // molécula átomo por átomo y casi todos los pasos intermedios son incompletos:
        // sin esto, dos carbonos juntos no mostrarían ningún enlace hasta completar el
        // etano. Mientras está a medias todos vienen simples (el orden real no existe aún).
        if (resp?.bonds != null)
            foreach (var bd in resp.bonds) AddBond(batchBonds, bd, map);

        bool valid = resp != null && resp.isValid && resp.molecule != null;
        if (valid)
        {
            anyValid = true;
            var m = resp.molecule;
            Debug.Log($"[Detect] {m.name} ({m.molecularFormula}) · enlaces={resp.bonds?.Length ?? 0}");

            // Los enlaces de la molécula COMPLETA van aparte: son los que deciden el banner.
            if (m.bonds != null)
                foreach (var bd in m.bonds) AddBond(batchValidBonds, bd, map);

            // Primera vez que se descubre esta molécula → avisar para el modal.
            if (m.isNewDiscovery)
            {
                newDiscoveryThisBatch = true;
                AudioManager.Instance.PlayDiscovery();
                OnNewDiscovery?.Invoke(m.molecularFormula, m.name);
            }
        }
        else if (resp != null)
        {
            Debug.Log($"[Detect] a medias ({resp.invalidityReason}) · enlaces={resp.bonds?.Length ?? 0}");
        }

        // Solo se redibuja si el servidor contestó. Con el servicio caído se mantiene en
        // pantalla lo último dibujado: borrarlo haría desaparecer las moléculas del alumno
        // por un problema de red.
        if (resp != null) bondRenderer.SetBonds(batchBonds);

        pendingRequests--;
        if (pendingRequests <= 0)
        {
            detecting = false;

            // Batch entero sin respuesta (servicio caído): no sabemos nada nuevo, así que
            // no se toca la firma. Resetearla haría que al volver el servicio se celebrara
            // otra vez una molécula que ya estaba dibujada.
            if (!anyResponse) { HideBanner(); return; }

            // Solo celebramos si los enlaces de moléculas COMPLETAS cambiaron. Reconfirmar
            // una que ya existía (al colocar un átomo aparte, o tras recargar el universo)
            // no vuelve a mostrar el banner, y los pasos intermedios tampoco lo disparan.
            string validSig = BondsSignature(batchValidBonds);
            if (anyValid && validSig != prevBondsSig) ShowMoleculeFormed();
            else                                      HideBanner();
            prevBondsSig = validSig;
        }
    }

    // Traduce un enlace del backend a ids de Atom3D reales: begin/endAtomId son índices
    // locales del request (0..n-1), no los ids del mundo.
    static void AddBond(List<(int a, int b, int order)> into, ApiManager.BondDTO bd, Atom3D[] map)
    {
        if (bd.beginAtomId >= 0 && bd.beginAtomId < map.Length &&
            bd.endAtomId   >= 0 && bd.endAtomId   < map.Length)
            into.Add((map[bd.beginAtomId].id, map[bd.endAtomId].id, bd.order));
    }

    // No queda nada dibujado: también se olvida la firma, o al rearmar la misma molécula
    // no saldría el banner.
    void ClearDrawnBonds()
    {
        bondRenderer.Clear();
        batchBonds.Clear();
        batchValidBonds.Clear();
        prevBondsSig = "";
    }

    // ── Agrupamiento por cercanía (componentes por distancia; NO decide enlaces) ─
    List<List<Atom3D>> ClusterAtoms() => AtomClustering.Group(atomsBuf, clusterDistance);

    void OnDestroy() => bondRenderer?.Dispose();

    // ── Guardar / restaurar enlaces (para no re-descubrir al recargar) ─────────
    public List<(int a, int b, int order)> ExportBonds() => bondRenderer.ExportBonds();

    /// <summary>
    /// Dibuja enlaces cargados SIN llamar al backend, y marca la estructura actual
    /// como "ya detectada" para que no se re-descubra la misma molécula al entrar.
    /// </summary>
    public void ImportBonds(List<(int a, int b, int order)> bonds)
    {
        GatherAtoms(placement ? placement.AtomsRoot : null);
        batchBonds.Clear();
        batchValidBonds.Clear();
        if (bonds != null) { batchBonds.AddRange(bonds); batchValidBonds.AddRange(bonds); }
        bondRenderer.SetBonds(batchBonds);

        // Lo guardado ya se descubrió antes: su firma cuenta como "lo que ya había", así
        // que al reabrir el universo no se vuelve a celebrar.
        prevBondsSig = BondsSignature(batchValidBonds);

        string h = StructureHash();
        lastHash = h;
        sentHash = h;   // hash == sentHash → DetectionStep no vuelve a detectar
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

        foreach (var (a, b, _) in batchValidBonds)
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
