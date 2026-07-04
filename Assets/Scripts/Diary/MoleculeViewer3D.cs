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

    RenderTexture rt;
    readonly List<Transform>       atomTf   = new List<Transform>();
    readonly List<Transform>       labelTf  = new List<Transform>();
    readonly List<float>           atomRad  = new List<float>();
    bool dragging;

    void Reset() { target = GetComponent<RawImage>(); }

    public void Show(JournalStructure s)
    {
        EnsureRenderTexture();
        Clear();
        if (s == null || s.atoms == null || s.atoms.Length == 0) return;

        int n = s.atoms.Length;
        var local = new Vector3[n];
        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            var p = s.atoms[i].position;
            local[i] = p != null ? new Vector3(p.x, p.y, p.z) : Vector3.zero;
            centroid += local[i];
        }
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
            SpawnAtom(s.atoms[i].type, local[i] * scale);

        if (s.bonds != null)
            foreach (var b in s.bonds)
            {
                if (b.beginAtomId < 0 || b.beginAtomId >= n || b.endAtomId < 0 || b.endAtomId >= n) continue;
                SpawnBond(atomTf[b.beginAtomId].localPosition, atomTf[b.endAtomId].localPosition, Mathf.Clamp(b.order, 1, 3));
            }
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

    void SpawnBond(Vector3 a, Vector3 b, int order)
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
            if (bondMaterial) cyl.GetComponent<Renderer>().sharedMaterial = bondMaterial;

            Vector3 a2 = a + perp * off, b2 = b + perp * off;
            cyl.transform.localPosition = (a2 + b2) * 0.5f;
            cyl.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dirN);
            cyl.transform.localScale = new Vector3(t, len * 0.5f, t);
        }
    }

    public void Clear()
    {
        if (stageRoot == null) return;
        for (int i = stageRoot.childCount - 1; i >= 0; i--)
            Destroy(stageRoot.GetChild(i).gameObject);
        atomTf.Clear(); labelTf.Clear(); atomRad.Clear();
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
        for (int i = 0; i < labelTf.Count && i < atomTf.Count; i++)
        {
            if (!labelTf[i] || !atomTf[i]) continue;
            labelTf[i].position = atomTf[i].position - camFwd * (atomRad[i] + 0.06f);
            labelTf[i].rotation = Quaternion.LookRotation(labelTf[i].position - camPos);
        }
    }

    public void OnDrag(PointerEventData e)
    {
        if (stageRoot == null || cam == null) return;
        stageRoot.Rotate(cam.transform.up,    -e.delta.x * rotateSpeed, Space.World);
        stageRoot.Rotate(cam.transform.right,  e.delta.y * rotateSpeed, Space.World);
    }

    void OnEnable()  { dragging = false; }
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
