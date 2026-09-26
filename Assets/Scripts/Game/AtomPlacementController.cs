using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Colocación, selección, movimiento y borrado de átomos en la zona 3D.
///
/// EL MODELO: los mandos mueven LA CÁMARA, siempre. Lo que estés colocando o editando
/// viaja con ella, así que la cruz del centro marca en todo momento dónde va a caer.
/// Antes las flechas movían el átomo y la cámara según el caso, y el mismo control
/// hacía dos cosas distintas sin avisar.
///
///  • Arrastrar sobre espacio vacío → rota la cámara (libre, también por debajo).
///  • D-pad y flechas verticales → desplazan la cámara. SIEMPRE.
///  • Tocar un slot del hotbar → aparece una PREVISUALIZACIÓN (fantasma) bajo la cruz,
///    que sigue a la cámara. "Presiona para colocar átomo" la deja ahí; el fantasma se
///    queda, para colocar varios.
///  • Tocar un átomo colocado lo selecciona (mover/borrar).
///  • Con uno seleccionado, ese mismo botón dice "Mover átomo": al pulsarlo el átomo
///    pasa a viajar con la cámara, y se suelta con "Soltar aquí".
///  • "Cancelar" quita la previsualización, o devuelve el átomo a donde estaba.
/// </summary>
public class AtomPlacementController : MonoBehaviour, ClassSceneRenderer.IAtomSink
{
    [Header("Refs")]
    [SerializeField] private Camera               cam;
    [SerializeField] private OrbitCameraController orbit;
    [SerializeField] private Material             atomBaseMaterial;
    [SerializeField] private Material             previewMaterial;  // translúcido (preview)
    [SerializeField] private GameObject           reticleRoot;      // cursor central (guía)
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
    [SerializeField] private float rotateThreshold   = 7f;   // px para considerar arrastre
    [SerializeField] private float platformHalf      = 11.5f;
    [SerializeField] private float maxHeight         = 12f;
    [SerializeField] private float minSeparationFrac = 0.9f; // colisión si dist < atomScale*esto
    [SerializeField] private bool  showLabels        = true;

    Transform atomsRoot;
    int     armedAtom = -1;
    Atom3D  selected;
    int     nextId;

    // Reposicionando un átomo ya colocado: viaja con la cámara hasta que se suelta.
    bool    editing;
    Vector3 moveOrigin;      // para poder devolverlo si se cancela

    // Dónde miraba la cámara el frame anterior. Lo que se está colocando se mueve ESA
    // MISMA diferencia, en vez de saltar al centro: así al empezar a mover un átomo no
    // se teletransporta a donde apunta la cruz.
    Vector3 lastFocus;
    bool    hasLastFocus;

    // La etiqueta del botón de colocar. Se busca sola en el hijo del botón para no tener
    // que cablear una referencia más en cada escena que use este HUD.
    TMP_Text placeLabel;

    // Marca de suelo + línea de altura + anillo de selección (ver PlacementGuides).
    PlacementGuides guides;

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

        guides = new GameObject("PlacementGuides").AddComponent<PlacementGuides>();
        guides.Setup(atomScale);
    }

    void Start()
    {
        selector = FindObjectOfType<AtomSelectorController>();
        if (btnDelete)      btnDelete.onClick.AddListener(DeleteSelected);
        if (btnPlace)     { btnPlace.onClick.AddListener(OnPlaceButton);
                            placeLabel = btnPlace.GetComponentInChildren<TMP_Text>(true); }
        if (btnCancel)      btnCancel.onClick.AddListener(CancelCurrent);
        if (btnCollisionOk) btnCollisionOk.onClick.AddListener(HideCollision);
        ShowDelete(false);
        ShowCancel(false);
        if (collisionModal) collisionModal.SetActive(false);
        RefreshPlaceLabel();
    }

    /// <summary>Con esto apagado se puede rotar la cámara pero no tocar los átomos.
    /// Es lo que permite que la pizarra y el universo compartan el mismo espacio 3D sin
    /// que el docente mueva algo sin querer mientras conduce la clase.</summary>
    public bool Interactive { get; private set; } = true;

    public void SetInteractive(bool value)
    {
        Interactive = value;
        if (!value) { CancelPreview(); Deselect(); }
    }

    /// <summary>Tocar un slot del hotbar: arma el átomo y muestra su previsualización.</summary>
    public void ArmForPlacement(int atomIndex)
    {
        if (!Interactive) return;
        armedAtom = atomIndex;
        Deselect();      // modo exclusivo: salir de edición de átomos colocados
        ShowPreview();
    }

    void Update()
    {
        HandlePointer();
        FollowCamera();
        UpdateReticle();
        UpdateGuides();
    }

    /// <summary>Arrastra lo que se está colocando la misma distancia que se movió la
    /// cámara. Se usa la DIFERENCIA y no la posición absoluta para que empezar a mover
    /// un átomo no lo arranque de donde está.</summary>
    void FollowCamera()
    {
        if (!orbit) return;

        Vector3 focus = orbit.FocusPoint;
        if (!hasLastFocus) { lastFocus = focus; hasLastFocus = true; return; }

        Vector3 delta = focus - lastFocus;
        lastFocus = focus;
        if (delta.sqrMagnitude < 1e-10f) return;

        Transform target = ActiveTransform;
        if (!target) return;

        target.position = Clamp(target.position + delta);
    }

    /// <summary>Lo que ahora mismo viaja con la cámara: la previsualización si la hay, y
    /// si no el átomo que se esté reposicionando. Nada más se mueve solo.</summary>
    Transform ActiveTransform
        => previewGhost ? previewGhost.transform : (editing && selected ? selected.transform : null);

    Vector3 Clamp(Vector3 p) => new Vector3(
        Mathf.Clamp(p.x, -platformHalf, platformHalf),
        Mathf.Clamp(p.y, atomScale * 0.5f, maxHeight),
        Mathf.Clamp(p.z, -platformHalf, platformHalf));

    /// <summary>
    /// Las guías siguen al átomo ACTIVO: la previsualización tiene prioridad
    /// porque, mientras colocas, es lo que estás moviendo.
    /// </summary>
    void UpdateGuides()
    {
        if (!guides) return;

        if (previewGhost)   guides.Track(previewGhost.transform, false);
        else if (selected)  guides.Track(selected.transform, true);
        else                guides.Hide();
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
            previewGhost.transform.position = CrosshairPoint();
        }

        if (previewMat)
        {
            Color c = AtomCatalog.All[armedAtom].color;
            previewMat.SetColor(BaseColorId, new Color(c.r, c.g, c.b, 0.45f));
        }
        previewGhost.SetActive(true);
        ShowCancel(true);
        RefreshPlaceLabel();
    }

    public void CancelPreview()
    {
        if (previewGhost) Destroy(previewGhost);
        previewGhost = null;
        armedAtom = -1;
        ShowCancel(false);
        if (selector) selector.ClearHighlight();  // quita el resaltado cyan del slot
        RefreshPlaceLabel();
    }

    /// <summary>El punto al que mira la cámara: exactamente lo que hay bajo la cruz.
    ///
    /// Antes se lanzaba un rayo contra un plano horizontal, pero con la rotación ya libre
    /// ese rayo puede no cortar el plano —o cortarlo a la espalda— cuando se mira desde
    /// abajo. El punto de enfoque siempre existe y siempre está en el centro de pantalla.</summary>
    Vector3 CrosshairPoint()
    {
        if (orbit) return Clamp(orbit.FocusPoint);
        return new Vector3(0f, atomScale * 0.5f, 0f);
    }

    // ── El botón de colocar, según lo que haya ────────────────────────────────

    void OnPlaceButton()
    {
        if (!Interactive) return;
        if (editing)            ConfirmMove();
        else if (previewGhost)  PlaceActiveAtom();
        else if (selected)      BeginMove();
    }

    /// <summary>"Cancelar": quita la previsualización, o deja el átomo donde estaba.</summary>
    void CancelCurrent()
    {
        if (editing) { EndMove(keep: false); return; }
        CancelPreview();
    }

    /// <summary>El átomo seleccionado pasa a viajar con la cámara.</summary>
    void BeginMove()
    {
        if (!selected) return;
        editing    = true;
        moveOrigin = selected.transform.position;
        ShowCancel(true);
        RefreshPlaceLabel();
    }

    void ConfirmMove()
    {
        if (!selected) { EndMove(keep: false); return; }

        // Soltarlo encima de otro átomo formaría enlaces que no son: se avisa y se sigue
        // moviendo, en vez de aceptar una posición que el detector va a malinterpretar.
        if (Overlaps(selected.transform.position, ignore: selected)) { ShowCollision(); return; }
        EndMove(keep: true);
    }

    void EndMove(bool keep)
    {
        if (!editing) return;
        editing = false;
        ShowCancel(false);

        if (selected)
        {
            // Aunque se acepte, no se deja encima de otro: eso solo puede pasar por una
            // salida implícita (elegir otro elemento del hotbar a media edición).
            if (!keep || Overlaps(selected.transform.position, ignore: selected))
                selected.transform.position = moveOrigin;
            else if (selected.transform.position != moveOrigin)
                Dirty = true;
        }
        RefreshPlaceLabel();
    }

    void RefreshPlaceLabel()
    {
        if (!placeLabel) return;
        placeLabel.text = editing       ? "Soltar aquí"
                        : previewGhost  ? "Presiona para colocar átomo"
                        : selected      ? "Mover átomo"
                                        : "Presiona para colocar átomo";
    }

    /// <summary>Coloca un átomo real en la posición de la previsualización (esta se queda).</summary>
    public void PlaceActiveAtom()
    {
        if (!Interactive) return;
        if (armedAtom < 0 || previewGhost == null) return;
        Vector3 pos = previewGhost.transform.position;
        if (Overlaps(pos)) { ShowCollision(); return; }
        // animate: solo al colocar el jugador. Al restaurar un save entrarían
        // decenas de átomos a la vez y el efecto sería un estallido sin sentido.
        PlaceAtom(armedAtom, pos, animate: true);
        AudioManager.Instance.PlayPlaceAtom();
        Dirty = true;
        RefreshPlaceLabel();
    }

    // ── La cruz central: marca dónde cae lo que se coloca ─────────────────────
    // Está visible siempre que se pueda construir, también con la previsualización
    // puesta: ahora es la mira, y esconderla justo al apuntar sería quitarla cuando más
    // sirve. En la pizarra, conduciendo, no aparece.
    void UpdateReticle()
    {
        if (!reticleRoot) return;
        if (reticleRoot.activeSelf != Interactive) reticleRoot.SetActive(Interactive);
    }

    /// <summary>¿La posición se solaparía con un átomo ya colocado? 'ignore' es el que se
    /// está moviendo: si no se excluyera, chocaría consigo mismo.</summary>
    bool Overlaps(Vector3 pos, Atom3D ignore = null)
    {
        if (!atomsRoot) return false;
        float minDist = atomScale * minSeparationFrac;
        foreach (Transform child in atomsRoot)
        {
            var a = child.GetComponent<Atom3D>();
            if (!a || a == ignore) continue;
            if (Vector3.Distance(child.position, pos) < minDist) return true;
        }
        return false;
    }

    void ShowCollision()
    {
        AudioManager.Instance.PlayCollision();
        if (collisionModal) collisionModal.SetActive(true);
    }
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
        if (!Interactive) return;
        // Ni colocando ni moviendo se cambia de átomo: el toque es para soltar, no para
        // saltar a otro y dejarse el anterior a medio mover.
        if (!cam || previewGhost != null || editing) return;

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

    // ── Escena que llega del servidor (pizarra del docente) ───────────────────
    //
    // El docente y el alumno ven UN SOLO universo. Cuando el servidor devuelve el
    // estado, sus átomos se reconstruyen AQUÍ y no como esferas aparte, para que el
    // docente pueda moverlos y borrarlos igual que los que colocó a mano. Si no, lo
    // traído del catálogo sería intocable y lo construido sí, que es incoherente.

    /// <summary>Vacía el universo. No marca cambios: lo usan tanto "Limpiar" (que ya
    /// publica por su cuenta) como el repintado de un estado del servidor.</summary>
    public void ClearAtoms()
    {
        Deselect();
        CancelPreview();
        if (!atomsRoot) return;
        for (int i = atomsRoot.childCount - 1; i >= 0; i--)
        {
            var a = atomsRoot.GetChild(i).GetComponent<Atom3D>();
            if (a) Discard(a.gameObject);
        }
    }

    // Destroy() no borra hasta el final del frame. Quien recorra atomsRoot antes de eso
    // —BondManager lo hace cada frame— vería los átomos viejos junto a los nuevos y
    // mandaría a detectar una estructura que no existe. Desengancharlos primero los saca
    // del recorrido en el acto.
    static void Discard(GameObject go)
    {
        go.transform.SetParent(null, false);
        Destroy(go);
    }

    /// <summary>Crea un átomo editable en una posición que decide el servidor. Sin
    /// animación (llegan decenas a la vez) y sin recorte (la geometría es suya).</summary>
    public Atom3D SpawnFromScene(string element, Vector3 worldPos)
    {
        int index = AtomCatalog.IndexOf(element);
        if (index < 0) return null;
        return PlaceAtom(index, worldPos, animate: false, clamp: false);
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
                if (a) Discard(a.gameObject);
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
    /// <param name="clamp">Recorta a la plataforma. Se apaga al reconstruir una escena
    /// que viene del servidor: ahí la geometría es autoritativa y recortarla movería
    /// átomos que el docente colocó a propósito lejos.</param>
    Atom3D PlaceAtom(int index, Vector3 worldPos, bool animate = false, bool clamp = true)
    {
        var info = AtomCatalog.All[index];
        float x = worldPos.x, y = worldPos.y, z = worldPos.z;
        if (clamp)
        {
            x = Mathf.Clamp(x, -platformHalf, platformHalf);
            z = Mathf.Clamp(z, -platformHalf, platformHalf);
            y = Mathf.Clamp(y, atomScale * 0.5f, maxHeight);
        }

        var root = new GameObject($"Atom_{info.symbol}_{nextId}");
        root.transform.SetParent(atomsRoot, false);
        root.transform.position = new Vector3(x, y, z);

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Sphere";
        sphere.transform.SetParent(root.transform, false);
        sphere.transform.localScale = Vector3.one * atomScale;

        var a = root.AddComponent<Atom3D>();
        a.Init(index, info.symbol, nextId++, atomBaseMaterial, info.color, sphere.GetComponent<Renderer>());

        // El "plop" va en la esfera hija, no en el raíz: BondManager lee la
        // posición del raíz para detectar la estructura y animarlo provocaría
        // llamadas al backend mientras dura el efecto.
        if (animate && GraphicsManager.Instance.Effects != GraphicsLevel.Bajo)
            sphere.AddComponent<SpawnPop>().Play(Vector3.one * atomScale);

        if (showLabels && labelFont) AddLabel(root.transform, info.symbol);
        return a;
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

    /// <summary>d-pad: desplaza LA CÁMARA. Lo que se esté colocando la sigue (FollowCamera),
    /// así que este mismo control coloca sin dejar de hacer siempre lo mismo.</summary>
    public void MoveOrPan(Vector2 dir)
    {
        if (orbit) orbit.PanScreen(dir);
    }

    /// <summary>Flechas verticales: suben y bajan LA CÁMARA, con la misma regla.</summary>
    public void VerticalOrCam(float sign)
    {
        if (orbit) orbit.MoveVertical(sign);
    }

    void Select(Atom3D a)
    {
        if (editing) EndMove(keep: true);
        if (selected && selected != a) selected.SetSelected(false);
        selected = a;
        if (selected) selected.SetSelected(true);
        ShowDelete(selected != null);
        RefreshPlaceLabel();
    }

    void Deselect()
    {
        if (editing) EndMove(keep: true);
        if (selected) selected.SetSelected(false);
        selected = null;
        ShowDelete(false);
        RefreshPlaceLabel();
    }

    void DeleteSelected()
    {
        if (!selected) return;
        editing = false;
        ShowCancel(false);
        Discard(selected.gameObject);
        AudioManager.Instance.PlayDeleteAtom();
        selected = null;
        ShowDelete(false);
        Dirty = true;
        RefreshPlaceLabel();
    }

    void ShowDelete(bool show) { if (btnDeleteRoot) btnDeleteRoot.SetActive(show); }
    void ShowCancel(bool show) { if (cancelRoot)    cancelRoot.SetActive(show); }
}
