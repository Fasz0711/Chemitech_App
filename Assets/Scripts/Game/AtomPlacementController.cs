using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Colocación, selección, movimiento y borrado de átomos en la zona 3D.
///
///  • Arrastrar sobre espacio vacío → rota la cámara.
///  • Tocar un slot del hotbar → marca ese átomo como activo (no coloca).
///  • Botón "Presiona para colocar átomo" → coloca el átomo activo en la retícula
///    (centro de pantalla), uno por pulsación.
///  • Tocar el mundo (sin arrastrar) → selecciona/deselecciona un átomo colocado.
///  • Con un átomo seleccionado: d-pad/flechas lo mueven y aparece el botón Eliminar.
/// </summary>
public class AtomPlacementController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera               cam;
    [SerializeField] private OrbitCameraController orbit;
    [SerializeField] private Material             atomBaseMaterial;
    [SerializeField] private GameObject           reticleRoot;   // cursor central (siempre visible)
    [SerializeField] private Image                reticleDot;    // se tiñe con el átomo activo
    [SerializeField] private Button               btnPlace;      // "Presiona para colocar átomo"
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

    /// <summary>Contenedor de los átomos colocados (lo lee BondManager).</summary>
    public Transform AtomsRoot => atomsRoot;

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
        if (btnDelete)       btnDelete.onClick.AddListener(DeleteSelected);
        if (btnPlace)        btnPlace.onClick.AddListener(PlaceActiveAtom);
        if (btnCollisionOk)  btnCollisionOk.onClick.AddListener(HideCollision);
        ShowDelete(false);
        if (reticleRoot)     reticleRoot.SetActive(true);  // siempre visible
        if (collisionModal)  collisionModal.SetActive(false);
    }

    /// <summary>Marca el átomo activo del hotbar (lo coloca el botón "Presiona para colocar átomo").</summary>
    public void ArmForPlacement(int atomIndex)
    {
        armedAtom = atomIndex;
    }

    void Update()
    {
        HandlePointer();
        UpdateReticle();
    }

    // ── Retícula: cursor central siempre visible; se tiñe con el átomo activo ──
    void UpdateReticle()
    {
        if (!reticleRoot) return;
        if (!reticleRoot.activeSelf) reticleRoot.SetActive(true);
        if (reticleDot)
            reticleDot.color = (armedAtom >= 0) ? AtomCatalog.All[armedAtom].color : Color.white;
    }

    /// <summary>
    /// Coloca el átomo activo en la retícula (centro), a la ALTURA de enfoque de
    /// la cámara: subes/bajas la vista con las flechas verticales y el átomo va ahí.
    /// </summary>
    public void PlaceActiveAtom()
    {
        if (armedAtom < 0 || !cam) return;
        Ray cray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        float planeY = orbit ? Mathf.Max(0f, orbit.FocusHeight) : 0f;
        var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (!plane.Raycast(cray, out float e)) return;

        Vector3 raw = cray.GetPoint(e);
        Vector3 pos = new Vector3(
            Mathf.Clamp(raw.x, -platformHalf, platformHalf),
            Mathf.Clamp(raw.y, atomScale * 0.5f, maxHeight),
            Mathf.Clamp(raw.z, -platformHalf, platformHalf));

        if (Overlaps(pos)) { ShowCollision(); return; }
        PlaceAtom(armedAtom, pos);
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

    // El dedo tiembla al tocar: el umbral para distinguir tap de arrastre debe
    // escalar con la densidad/tamaño de pantalla (en desktop queda pequeño).
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
        if (!cam) return;

        // 1) Rayo directo: ¿acierta el collider de un átomo?
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            var atom = hit.collider.GetComponentInParent<Atom3D>();
            if (atom) { Select(atom); return; }
        }

        // 2) Tolerancia (touch): el átomo más cercano al toque en pantalla.
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
            if (sp.z <= 0f) continue; // detrás de la cámara
            float d = Vector2.Distance(screenPos, new Vector2(sp.x, sp.y));
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
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

    /// <summary>d-pad: mueve el átomo seleccionado sobre el plano, o hace pan de cámara.</summary>
    public void MoveOrPan(Vector2 dir)
    {
        if (selected)
        {
            Vector3 right = cam.transform.right;
            Vector3 fwd   = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 mv    = (right * dir.x + fwd * dir.y) * moveSpeed * Time.deltaTime;
            var p = selected.transform.position + mv;
            p.x = Mathf.Clamp(p.x, -platformHalf, platformHalf);
            p.z = Mathf.Clamp(p.z, -platformHalf, platformHalf);
            selected.transform.position = p;
        }
        else if (orbit) orbit.PanScreen(dir);
    }

    /// <summary>Flechas verticales: sube/baja el átomo seleccionado, o la cámara.</summary>
    public void VerticalOrCam(float sign)
    {
        if (selected)
        {
            var p = selected.transform.position;
            p.y = Mathf.Clamp(p.y + sign * moveSpeed * Time.deltaTime, atomScale * 0.5f, maxHeight);
            selected.transform.position = p;
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
    }

    void ShowDelete(bool show)
    {
        if (btnDeleteRoot) btnDeleteRoot.SetActive(show);
    }
}
