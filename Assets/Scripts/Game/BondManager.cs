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

    [Header("Ajustes")]
    [SerializeField] private float clusterDistance = 2.0f;  // agrupa candidatos (no decide enlaces)
    [SerializeField] private float bondThickness   = 0.09f;
    [SerializeField] private float bondSpacing     = 0.20f; // separación entre líneas paralelas (orden 2/3)
    [SerializeField] private float detectDelay     = 0.45f; // debounce tras el último cambio

    static readonly Color C_DETECT = new Color(0.10f, 0.65f, 0.81f, 1f);
    static readonly Color C_OK     = new Color(0.18f, 0.80f, 0.44f, 1f);

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
    readonly List<(int a, int b, int order)> batchBonds = new List<(int, int, int)>();

    void Awake()
    {
        bondsRoot = new GameObject("Bonds").transform;
        cam = Camera.main;
    }

    void Start() { if (bannerRoot) bannerRoot.SetActive(false); }

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

        if (hash != sentHash && Time.time - lastChangeTime >= detectDelay)
        {
            sentHash = hash;
            StartDetection();
        }
    }

    void StartDetection()
    {
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
                onSuccess: resp => OnClusterResult(batch, resp, capturedMap),
                onError:   (code, detail) => { Debug.LogWarning($"[Detect] Error {code}: {detail}"); OnClusterResult(batch, null, capturedMap); });
        }
    }

    void OnClusterResult(int batch, ApiManager.DetectResponse resp, Atom3D[] map)
    {
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
        }

        pendingRequests--;
        if (pendingRequests <= 0)
        {
            detecting = false;
            if (anyValid) ShowBanner("¡Molécula formada!", C_OK);
            else          HideBanner();
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
            for (int i = 0; i < n; i++)
            {
                var c = bv.cyls[i]; if (!c) continue;
                c.SetActive(true);
                float off = (n == 1) ? 0f : (i - (n - 1) * 0.5f) * bondSpacing;
                Vector3 a2 = pa + perp * off, b2 = pb + perp * off;
                c.transform.position = (a2 + b2) * 0.5f;
                c.transform.up = dirN;
                c.transform.localScale = new Vector3(bondThickness, len * 0.5f, bondThickness);
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
        if (bannerRoot) bannerRoot.SetActive(true);
        if (bannerBg)   bannerBg.color = col;
        if (bannerText) bannerText.text = msg;
    }

    void HideBanner() { if (bannerRoot) bannerRoot.SetActive(false); }
}
