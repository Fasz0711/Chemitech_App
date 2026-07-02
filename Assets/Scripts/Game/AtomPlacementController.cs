using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Colocación, selección, movimiento y borrado de átomos en la zona 3D.
///
///  • Arrastrar sobre espacio vacío → rota la cámara.
///  • Tocar un slot del hotbar → aparece una PREVISUALIZACIÓN (fantasma) del átomo
///    en el centro de la vista, se oculta el cursor y sale el botón "Cancelar".
///  • Las flechas mueven la previsualización (modo exclusivo).
///  • "Presiona para colocar átomo" → coloca un átomo real en la posición de la
///    previsualización; la previsualización se queda (colocar varios).
///  • "Cancelar" → quita la previsualización y vuelve el cursor.
///  • Sin previsualización: tocar un átomo colocado lo selecciona (mover/borrar).
/// </summary>
public class AtomPlacementController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera               cam;
    [SerializeField] private OrbitCameraController orbit;
    [SerializeField] private Material             atomBaseMaterial;
    [SerializeField] private Material             previewMaterial;  // translúcido (preview)
    [SerializeField] private GameObject           reticleRoot;      // cursor central (guía)
    [SerializeField] private Image                reticleDot;
    [SerializeField] private Button               btnPlace;         // "Presiona para colocar átomo"
    [SerializeField] private GameObject           cancelRoot;       // botón "Cancelar" (preview)
    [SerializeField] private Button               btnCancel;
    [SerializeField] private GameObject           btnDeleteRoot;
    [SerializeField] private Button               btnDelete;
    [SerializeField] private TMP_FontAsset        labelFont;

    [Header("Colisión")]
    [SerializeField] private GameObject collisionModal;     // "¡ Colisión Detectada !"
    [SerializeField] private Button     btnCollisionOk;     // "Entendido"

    [Header("Ajustes")]
    [SerializeField] private float atomScale         = 1.1f;
    [SerializeField] private float moveSpeed         = 5f;
    [SerializeField] private float rotateThreshold   = 7f;   // px para considerar arrastre
    [SerializeField] private float platformHalf      = 11.5f;
    [SerializeField] private float maxHeight         = 12f;
    [SerializeField] private float minSeparationFrac = 0.9f; // colisión si dist < atomScale*esto
    [SerializeField] private bool  showLabels        = true;

    Transform atomsRoot;
    int     armedAtom = -1;
    Atom3D  selected;
    int     nextId;

    // previsualización
    GameObject previewGhost;
    Material   previewMat;
    AtomSelectorController selector;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// <summary>Contenedor de los átomos colocados (lo lee BondManager).</summary>
    public Transform AtomsRoot => atomsRoot;

    /// <summary>Hay cambios sin guardar (átomos colocados/movidos/borrados).</summary>
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;

    // input
    Vector2 pointerDown, lastPointer;
    bool    dragging;
    int     activePointerId = -1;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        atomsRoot = new GameObject("Atoms").transform;
    }

    void Start()
    {
        selector = FindObjectOfType<AtomSelectorController>();
        if (btnDelete)      btnDelete.onClick.AddListener(DeleteSelected);
        if (btnPlace)       btnPlace.onClick.AddListener(PlaceActiveAtom);
        if (btnCancel)      btnCancel.onClick.AddListener(CancelPreview);
        if (btnCollisionOk) btnCollisionOk.onClick.AddListener(HideCollision);
        ShowDelete(false);
        ShowCancel(false);
        if (reticleRoot)    reticleRoot.SetActive(true);
        if (collisionModal) collisionModal.SetActive(false);
    }

    /// <summary>Tocar un slot del hotbar: arma el átomo y muestra su previsualización.</summary>
    public void ArmForPlacement(int atomIndex)
    {
        armedAtom = atomIndex;
        Deselect();      // modo exclusivo: salir de edición de átomos colocados
        ShowPreview();
    }

    void Update()
    {
        HandlePointer();
        UpdateReticle();
    }

    // ── Previsualización ──────────────────────────────────────────────────────
    void ShowPreview()
    {
        if (armedAtom < 0) return;

        if (previewGhost == null)
        {
            previewGhost = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            previewGhost.name = "PlacementPreview";
            var col = previewGhost.GetComponent<Collider>(); if (col) Destroy(col);
            previewGhost.transform.SetParent(atomsRoot, false); // sin Atom3D → BondManager lo ignora
            previewGhost.transform.localScale = Vector3.one * atomScale;
            if (previewMaterial)
            {
                previewMat = new Material(previewMaterial);
                previewGhost.GetComponent<Renderer>().sharedMaterial = previewMat;
            }
            previewGhost.transform.position = CenterGroundPos();
        }

        if (previewMat)
        {
            Color c = AtomCatalog.All[armedAtom].color;
            previewMat.SetColor(BaseColorId, new Color(c.r, c.g, c.b, 0.45f));
        }
        previewGhost.SetActive(true);
        ShowCancel(true);
    }

    public void CancelPreview()
    {
        if (previewGhost) Destroy(previewGhost);
        previewGhost = null;
        armedAtom = -1;
        ShowCancel(false);
        if (selector) selector.ClearHighlight();  // quita el resaltado cyan del slot
    }

    Vector3 CenterGroundPos()
    {
        if (!cam) return new Vector3(0f, atomScale * 0.5f, 0f);
        Ray cray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        float planeY = orbit ? Mathf.Max(0f, orbit.FocusHeight) : 0f;
        var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (plane.Raycast(cray, out float e))
        {
            Vector3 raw = cray.GetPoint(e);
            return new Vector3(
                Mathf.Clamp(raw.x, -platformHalf, platformHalf),
                Mathf.Clamp(raw.y, atomScale * 0.5f, maxHeight),
                Mathf.Clamp(raw.z, -platformHalf, platformHalf));
        }
        return new Vector3(0f, atomScale * 0.5f, 0f);
    }

    /// <summary>Coloca un átomo real en la posición de la previsualización (esta se queda).</summary>
    public void PlaceActiveAtom()
    {
        if (armedAtom < 0 || previewGhost == null) return;
        Vector3 pos = previewGhost.transform.position;
        if (Overlaps(pos)) { ShowCollision(); return; }
        PlaceAtom(armedAtom, pos);
        Dirty = true;
    }

    // ── Retícula: cursor central, visible solo cuando NO hay previsualización ──
    void UpdateReticle()
    {
        if (!reticleRoot) return;
        bool show = (previewGhost == null);
        if (reticleRoot.activeSelf != show) reticleRoot.SetActive(show);
        if (show && reticleDot) reticleDot.color = Color.white;
    }

    /// <summary>¿La posición se solaparía con un átomo ya colocado?</summary>
    bool Overlaps(Vector3 pos)
    {
        if (!atomsRoot) return false;
        float minDist = atomScale * minSeparationFrac;
        foreach (Transform child in atomsRoot)
            if (child.GetComponent<Atom3D>() && Vector3.Distance(child.position, pos) < minDist)
                return true;
        return false;
    }

    void ShowCollision() { if (collisionModal) collisionModal.SetActive(true); }
    void HideCollision() { if (collisionModal) collisionModal.SetActive(false); }

    // ── Input unificado (touch nativo en móvil, mouse en desktop) ─────────────
    void HandlePointer()
    {
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            switch (t.phase)
            {
                case TouchPhase.Began:                       BeginPointer(t.position, t.fingerId); break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:                  MovePointer(t.position);              break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:                    EndPointer(t.position);               break;
            }
            return;
        }

        if (Input.GetMouseButtonDown(0)) BeginPointer(Input.mousePosition, -1);
        else if (Input.GetMouseButton(0)) MovePointer(Input.mousePosition);
        else if (Input.GetMouseButtonUp(0)) EndPointer(Input.mousePosition);
    }

    void BeginPointer(Vector2 pos, int pointerId)
    {
        pointerDown = lastPointer = pos;
        dragging = false;
        activePointerId = pointerId;
    }

    void MovePointer(Vector2 pos)
    {
        if (!dragging && !IsOverUI(activePointerId) && Vector2.Distance(pos, pointerDown) > DragThreshold())
            dragging = true;
        if (dragging && orbit) orbit.Rotate(pos - lastPointer);
        lastPointer = pos;
    }

    void EndPointer(Vector2 pos)
    {
        if (!dragging && !IsOverUI(activePointerId)) HandleTap(pos);
        dragging = false;
    }

    float DragThreshold()
    {
        float dpiBased  = (Screen.dpi > 1f) ? Screen.dpi * 0.12f : 0f;
        float sizeBased = Screen.height * 0.022f;
        return Mathf.Max(rotateThreshold, dpiBased, sizeBased);
    }

    static bool IsOverUI(int pointerId)
    {
        return EventSystem.current && EventSystem.current.IsPointerOverGameObject(pointerId);
    }

    void HandleTap(Vector3 screenPos)
    {
        if (!cam || previewGhost != null) return; // en modo preview no se seleccionan átomos

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            var atom = hit.collider.GetComponentInParent<Atom3D>();
            if (atom) { Select(atom); return; }
        }

        var near = NearestAtomOnScreen(screenPos, TapTolerancePx());
        if (near) { Select(near); return; }

        Deselect();
    }

    float TapTolerancePx() => Mathf.Max(45f, Screen.height * 0.06f);

    Atom3D NearestAtomOnScreen(Vector2 screenPos, float maxPx)
    {
        Atom3D best = null;
        float  bestD = maxPx;
        if (!atomsRoot) return null;
        foreach (Transform child in atomsRoot)
        {
            var a = child.GetComponent<Atom3D>();
            if (!a) continue;
            Vector3 sp = cam.WorldToScreenPoint(a.transform.position);
            if (sp.z <= 0f) continue;
            float d = Vector2.Distance(screenPos, new Vector2(sp.x, sp.y));
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    // ── Guardar / restaurar estado (lo usa el modal de pausa) ─────────────────
    /// <summary>Átomos colocados en orden de atomsRoot (para mapear índices ↔ ids).</summary>
    public List<Atom3D> GetOrderedAtoms()
    {
        var list = new List<Atom3D>();
        if (!atomsRoot) return list;
        foreach (Transform c in atomsRoot)
        {
            var a = c.GetComponent<Atom3D>();
            if (a) list.Add(a);
        }
        return list;
    }

    public List<AtomSave> ExportAtoms()
    {
        var list = new List<AtomSave>();
        foreach (var a in GetOrderedAtoms())
        {
            var p = a.transform.position;
            list.Add(new AtomSave { element = a.element, x = p.x, y = p.y, z = p.z });
        }
        return list;
    }

    public void ImportAtoms(List<AtomSave> saved)
    {
        if (atomsRoot)
            for (int i = atomsRoot.childCount - 1; i >= 0; i--)
            {
                var a = atomsRoot.GetChild(i).GetComponent<Atom3D>();
                if (a) Destroy(a.gameObject);
            }
        if (saved != null)
            foreach (var s in saved)
            {
                int idx = AtomCatalog.IndexOf(s.element);
                if (idx >= 0) PlaceAtom(idx, new Vector3(s.x, s.y, s.z));
            }
        Dirty = false; // estado recién cargado = sin cambios
    }

    // ── Colocar / mover / borrar ──────────────────────────────────────────────
    void PlaceAtom(int index, Vector3 worldPos)
    {
        var info = AtomCatalog.All[index];
        float x = Mathf.Clamp(worldPos.x, -platformHalf, platformHalf);
        float z = Mathf.Clamp(worldPos.z, -platformHalf, platformHalf);
        float y = Mathf.Clamp(worldPos.y, atomScale * 0.5f, maxHeight);

        var root = new GameObject($"Atom_{info.symbol}_{nextId}");
        root.transform.SetParent(atomsRoot, false);
        root.transform.position = new Vector3(x, y, z);

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Sphere";
        sphere.transform.SetParent(root.transform, false);
        sphere.transform.localScale = Vector3.one * atomScale;

        var a = root.AddComponent<Atom3D>();
        a.Init(index, info.symbol, nextId++, atomBaseMaterial, info.color, sphere.GetComponent<Renderer>());

        if (showLabels && labelFont) AddLabel(root.transform, info.symbol);
    }

    void AddLabel(Transform parent, string symbol)
    {
        var lblGo = new GameObject("Label");
        lblGo.transform.SetParent(parent, false);
        lblGo.transform.localPosition = Vector3.up * (atomScale * 0.5f + 0.45f);
        lblGo.transform.localScale = Vector3.one * 0.14f;
        var tmp = lblGo.AddComponent<TextMeshPro>();
        tmp.text = symbol; tmp.font = labelFont; tmp.fontSize = 10f;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        var rt = tmp.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(4f, 2f);
        lblGo.AddComponent<Billboard>();
    }

    /// <summary>d-pad: mueve la previsualización; si no, el átomo seleccionado; si no, pan de cámara.</summary>
    public void MoveOrPan(Vector2 dir)
    {
        Transform target = previewGhost ? previewGhost.transform : (selected ? selected.transform : null);
        if (target)
        {
            Vector3 right = cam.transform.right;
            Vector3 fwd   = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 mv    = (right * dir.x + fwd * dir.y) * moveSpeed * Time.deltaTime;
            var p = target.position + mv;
            p.x = Mathf.Clamp(p.x, -platformHalf, platformHalf);
            p.z = Mathf.Clamp(p.z, -platformHalf, platformHalf);
            target.position = p;
            if (!previewGhost && selected) Dirty = true; // mover un átomo colocado es un cambio
        }
        else if (orbit) orbit.PanScreen(dir);
    }

    /// <summary>Flechas verticales: sube/baja la previsualización o el átomo seleccionado, o la cámara.</summary>
    public void VerticalOrCam(float sign)
    {
        Transform target = previewGhost ? previewGhost.transform : (selected ? selected.transform : null);
        if (target)
        {
            var p = target.position;
            p.y = Mathf.Clamp(p.y + sign * moveSpeed * Time.deltaTime, atomScale * 0.5f, maxHeight);
            target.position = p;
            if (!previewGhost && selected) Dirty = true;
        }
        else if (orbit) orbit.MoveVertical(sign);
    }

    void Select(Atom3D a)
    {
        if (selected && selected != a) selected.SetSelected(false);
        selected = a;
        if (selected) selected.SetSelected(true);
        ShowDelete(selected != null);
    }

    void Deselect()
    {
        if (selected) selected.SetSelected(false);
        selected = null;
        ShowDelete(false);
    }

    void DeleteSelected()
    {
        if (!selected) return;
        Destroy(selected.gameObject);
        selected = null;
        ShowDelete(false);
        Dirty = true;
    }

    void ShowDelete(bool show) { if (btnDeleteRoot) btnDeleteRoot.SetActive(show); }
    void ShowCancel(bool show) { if (cancelRoot)    cancelRoot.SetActive(show); }
}
