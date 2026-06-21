using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Enlaces por proximidad (líneas simples) entre átomos cercanos, y detección
/// automática de moléculas vía API cuando la estructura se estabiliza.
/// Banner de estado: azul "Detectando…" → verde "¡Molécula formada!".
/// </summary>
public class BondManager : MonoBehaviour
{
    [SerializeField] private AtomPlacementController placement;
    [SerializeField] private Material        bondMaterial;
    [SerializeField] private GameObject      bannerRoot;
    [SerializeField] private Image           bannerBg;
    [SerializeField] private TextMeshProUGUI bannerText;

    [Header("Ajustes")]
    [SerializeField] private float bondDistance  = 1.8f;   // proximidad para enlazar
    [SerializeField] private float bondThickness = 0.12f;
    [SerializeField] private float detectDelay   = 0.45f;  // debounce antes de llamar a la API

    static readonly Color C_DETECT = new Color(0.10f, 0.65f, 0.81f, 1f);
    static readonly Color C_OK     = new Color(0.18f, 0.80f, 0.44f, 1f);

    Transform bondsRoot;
    readonly Dictionary<long, GameObject> bonds = new Dictionary<long, GameObject>();
    readonly List<Atom3D> atomsBuf = new List<Atom3D>();
    readonly List<(Atom3D a, Atom3D b)> pairBuf = new List<(Atom3D, Atom3D)>();

    string lastHash = "";
    string sentHash = "";
    float  lastChangeTime;
    bool   detecting;

    void Awake() { bondsRoot = new GameObject("Bonds").transform; }

    void Start() { if (bannerRoot) bannerRoot.SetActive(false); }

    void Update()
    {
        GatherAtoms(placement ? placement.AtomsRoot : null);
        RecomputeBonds();
        UpdateBondVisuals();
        DetectionStep();
    }

    // ── Átomos / enlaces ──────────────────────────────────────────────────────
    void GatherAtoms(Transform root)
    {
        atomsBuf.Clear();
        if (!root) return;
        foreach (Transform c in root)
        {
            var a = c.GetComponent<Atom3D>();
            if (a) atomsBuf.Add(a);
        }
    }

    void RecomputeBonds()
    {
        pairBuf.Clear();
        float d2 = bondDistance * bondDistance;
        for (int i = 0; i < atomsBuf.Count; i++)
            for (int j = i + 1; j < atomsBuf.Count; j++)
                if ((atomsBuf[i].transform.position - atomsBuf[j].transform.position).sqrMagnitude <= d2)
                    pairBuf.Add((atomsBuf[i], atomsBuf[j]));

        var desired = new HashSet<long>();
        foreach (var p in pairBuf) desired.Add(Key(p.a.id, p.b.id));

        // Quita enlaces que ya no aplican (átomos lejos o borrados)
        var toRemove = new List<long>();
        foreach (var kv in bonds)
            if (!desired.Contains(kv.Key) || !kv.Value) toRemove.Add(kv.Key);
        foreach (var k in toRemove)
        {
            if (bonds[k]) Destroy(bonds[k]);
            bonds.Remove(k);
        }

        // Crea los nuevos
        foreach (var p in pairBuf)
        {
            long k = Key(p.a.id, p.b.id);
            if (!bonds.ContainsKey(k)) bonds[k] = CreateBond();
        }
    }

    GameObject CreateBond()
    {
        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = "Bond";
        var col = cyl.GetComponent<Collider>(); if (col) Destroy(col); // no debe interferir al tocar átomos
        cyl.transform.SetParent(bondsRoot, false);
        if (bondMaterial) cyl.GetComponent<Renderer>().sharedMaterial = bondMaterial;
        return cyl;
    }

    void UpdateBondVisuals()
    {
        foreach (var p in pairBuf)
        {
            if (!bonds.TryGetValue(Key(p.a.id, p.b.id), out var cyl) || !cyl) continue;
            Vector3 pa = p.a.transform.position, pb = p.b.transform.position;
            Vector3 dir = pb - pa; float len = dir.magnitude;
            cyl.transform.position = (pa + pb) * 0.5f;
            if (len > 0.0001f) cyl.transform.up = dir / len;
            cyl.transform.localScale = new Vector3(bondThickness, len * 0.5f, bondThickness);
        }
    }

    // ── Detección (API) ───────────────────────────────────────────────────────
    void DetectionStep()
    {
        string hash = StructureHash();
        if (hash != lastHash) { lastHash = hash; lastChangeTime = Time.time; }

        if (pairBuf.Count == 0)               // sin enlaces no hay molécula que detectar
        {
            sentHash = "";
            if (!detecting) HideBanner();
            return;
        }

        if (hash != sentHash && Time.time - lastChangeTime >= detectDelay)
        {
            sentHash = hash;
            SendDetection();
        }
    }

    void SendDetection()
    {
        var atomsDTO = new ApiManager.AtomDTO[atomsBuf.Count];
        for (int i = 0; i < atomsBuf.Count; i++)
        {
            var p = atomsBuf[i].transform.position;
            atomsDTO[i] = new ApiManager.AtomDTO
            {
                id = atomsBuf[i].id, element = atomsBuf[i].element, x = p.x, y = p.y, z = p.z
            };
        }
        var bondsDTO = new ApiManager.BondDTO[pairBuf.Count];
        for (int i = 0; i < pairBuf.Count; i++)
            bondsDTO[i] = new ApiManager.BondDTO
            {
                beginAtomId = pairBuf[i].a.id, endAtomId = pairBuf[i].b.id, order = 1
            };

        detecting = true;
        ShowBanner("Detectando interacción atómica…", C_DETECT);
        Debug.Log($"[Detect] Enviando {atomsDTO.Length} átomos, {bondsDTO.Length} enlaces · userId='{SessionData.UserId}'");

        ApiManager.Instance.DetectMolecule(SessionData.UserId, atomsDTO, bondsDTO,
            onSuccess: resp => { detecting = false; OnDetect(resp); },
            onError:   (code, detail) => { detecting = false; Debug.LogWarning($"[Detect] Error {code}: {detail}"); HideBanner(); });
    }

    void OnDetect(ApiManager.DetectResponse resp)
    {
        Debug.Log($"[Detect] message={resp?.message} · isValid={resp?.isValid} · reason={resp?.invalidityReason} · " +
                  $"molécula={resp?.molecule?.name} ({resp?.molecule?.molecularFormula})");
        if (resp != null && resp.isValid && resp.molecule != null)
            ShowBanner("¡Molécula formada!", C_OK);
        else
            HideBanner();
    }

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
        sb.Append('|');
        var keys = new List<long>();
        foreach (var p in pairBuf) keys.Add(Key(p.a.id, p.b.id));
        keys.Sort();
        foreach (var k in keys) sb.Append(k).Append(';');
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

    static long Key(int a, int b)
    {
        int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
        return (long)lo * 100000L + hi;
    }
}
