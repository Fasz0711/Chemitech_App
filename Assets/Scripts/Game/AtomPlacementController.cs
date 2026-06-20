using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Colocación, selección, movimiento y borrado de átomos en la zona 3D.
///
///  • Arrastrar sobre espacio vacío → rota la cámara.
///  • Tocar (sin arrastrar) → coloca el átomo armado, o selecciona un átomo existente.
///  • Con un átomo seleccionado, el d-pad/flechas lo mueven (vía MoveOrPan/VerticalOrCam).
///  • Botón Eliminar borra el átomo seleccionado.
/// El hotbar arma el átomo a colocar con ArmForPlacement().
/// </summary>
public class AtomPlacementController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera               cam;
    [SerializeField] private OrbitCameraController orbit;
    [SerializeField] private Material             atomBaseMaterial;
    [SerializeField] private GameObject           btnDeleteRoot;
    [SerializeField] private Button               btnDelete;
    [SerializeField] private TMP_FontAsset        labelFont;

    [Header("Ajustes")]
    [SerializeField] private float atomScale       = 1.1f;
    [SerializeField] private float moveSpeed       = 5f;
    [SerializeField] private float rotateThreshold = 7f;   // px para considerar arrastre
    [SerializeField] private float platformHalf    = 11.5f;
    [SerializeField] private float maxHeight       = 12f;
    [SerializeField] private bool  showLabels      = true;

    Transform atomsRoot;
    int     armedAtom = -1;
    Atom3D  selected;
    int     nextId;

    // input
    Vector3 pointerDown, lastPointer;
    bool    dragging, pointerOverUI;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        atomsRoot = new GameObject("Atoms").transform;
    }

    void Start()
    {
        if (btnDelete) btnDelete.onClick.AddListener(DeleteSelected);
        ShowDelete(false);
    }

    /// <summary>Arma un átomo del hotbar para colocarlo con el próximo toque.</summary>
    public void ArmForPlacement(int atomIndex)
    {
        armedAtom = atomIndex;
        Deselect();
    }

    void Update() => HandlePointer();

    // ── Input unificado ───────────────────────────────────────────────────────
    void HandlePointer()
    {
        if (Input.GetMouseButtonDown(0))
        {
            pointerDown = lastPointer = Input.mousePosition;
            dragging = false;
            pointerOverUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
        }
        else if (Input.GetMouseButton(0))
        {
            Vector3 cur = Input.mousePosition;
            if (!dragging && !pointerOverUI && Vector3.Distance(cur, pointerDown) > rotateThreshold)
                dragging = true;
            if (dragging && orbit)
                orbit.Rotate((Vector2)(cur - lastPointer));
            lastPointer = cur;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (!dragging && !pointerOverUI) HandleTap(Input.mousePosition);
            dragging = false;
        }
    }

    void HandleTap(Vector3 screenPos)
    {
        if (!cam) return;
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            var atom = hit.collider.GetComponentInParent<Atom3D>();
            if (atom) { Select(atom); return; }
        }

        if (armedAtom >= 0)
        {
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter))
                PlaceAtom(armedAtom, ray.GetPoint(enter));
        }
        else Deselect();
    }

    // ── Colocar / mover / borrar ──────────────────────────────────────────────
    void PlaceAtom(int index, Vector3 groundPos)
    {
        var info = AtomCatalog.All[index];
        float x = Mathf.Clamp(groundPos.x, -platformHalf, platformHalf);
        float z = Mathf.Clamp(groundPos.z, -platformHalf, platformHalf);

        var root = new GameObject($"Atom_{info.symbol}_{nextId}");
        root.transform.SetParent(atomsRoot, false);
        root.transform.position = new Vector3(x, atomScale * 0.5f, z);

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
        armedAtom = -1;             // seleccionar sale del modo colocar
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
