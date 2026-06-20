using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Selector de átomos (modal) + hotbar de 6 slots.
///
/// Flujos de interacción:
///  • Botón "Selector de átomos" → abre modal sin slot objetivo. Tocas un átomo
///    (queda armado + popup), luego tocas un slot → se coloca.
///  • Tocar un slot LLENO → abre modal apuntando a ese slot. Tocar el MISMO átomo
///    lo quita; tocar OTRO lo reemplaza.
///  • Tocar un slot VACÍO → abre modal apuntando a ese slot; el próximo átomo lo llena.
/// </summary>
public class AtomSelectorController : MonoBehaviour
{
    const int SLOTS = 6;
    const float POPUP_SECONDS = 2.5f;

    [Header("Modal")]
    [SerializeField] private GameObject       modalRoot;
    [SerializeField] private Button           btnOpen;     // "Selector de átomos"
    [SerializeField] private Button           btnClose;    // X
    [SerializeField] private TMP_InputField   searchInput;

    [Header("Filtros (5: Todos, Metales, Gases, No metales, Metaloides)")]
    [SerializeField] private Button[]          filterButtons;
    [SerializeField] private Image[]           filterBgs;
    [SerializeField] private TextMeshProUGUI[] filterLabels;

    [Header("Grid")]
    [SerializeField] private RectTransform gridContent;
    [SerializeField] private GameObject    atomTemplate;   // inactivo; se clona por átomo

    [Header("Popup info")]
    [SerializeField] private RectTransform  infoPopup;
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("Hotbar")]
    [SerializeField] private Button[]          slotButtons;   // 6
    [SerializeField] private Image[]           slotIcons;     // 6 (círculo de átomo)
    [SerializeField] private TextMeshProUGUI[] slotSymbols;   // 6
    [SerializeField] private GameObject[]      slotBadges;    // 6 (badge numérico)

    [Header("Sprites")]
    [SerializeField] private Sprite atomCircle;

    // Colores de filtro activo/inactivo
    static readonly Color TAB_ON     = new Color(0.10f, 0.65f, 0.81f, 1f);
    static readonly Color TAB_OFF    = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color TAB_TXT_ON = Color.white;
    static readonly Color TAB_TXT_OFF= new Color(1f, 1f, 1f, 0.7f);

    readonly int[]  slotAtom = new int[SLOTS]; // índice en AtomCatalog.All, -1 vacío
    readonly List<GameObject> atomCells = new List<GameObject>();
    readonly List<Image>      atomRings = new List<Image>();

    AtomFilter currentFilter = AtomFilter.Todos;
    int   targetSlot = -1;
    int   armedAtom  = -1;
    Coroutine popupCo;

    void Awake()
    {
        for (int i = 0; i < SLOTS; i++) slotAtom[i] = -1;
    }

    void Start()
    {
        if (btnOpen)  btnOpen.onClick.AddListener(OpenFromButton);
        if (btnClose) btnClose.onClick.AddListener(Close);

        if (filterButtons != null)
            for (int i = 0; i < filterButtons.Length; i++)
            {
                int idx = i;
                if (filterButtons[i]) filterButtons[i].onClick.AddListener(() => SetFilter((AtomFilter)idx));
            }

        if (searchInput) searchInput.onValueChanged.AddListener(_ => RefreshGrid());

        if (slotButtons != null)
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int idx = i;
                if (slotButtons[i]) slotButtons[i].onClick.AddListener(() => OnSlotTapped(idx));
            }

        BuildCells();
        for (int i = 0; i < SLOTS; i++) RefreshSlot(i);

        if (infoPopup) infoPopup.gameObject.SetActive(false);
        if (modalRoot) modalRoot.SetActive(false);
    }

    // ── Construcción del grid (clona el template por cada átomo) ──────────────
    void BuildCells()
    {
        if (!atomTemplate || !gridContent) return;
        atomTemplate.SetActive(false);

        for (int i = 0; i < AtomCatalog.All.Count; i++)
        {
            var atom = AtomCatalog.All[i];
            var cell = Instantiate(atomTemplate, gridContent);
            cell.name = $"Atom_{atom.symbol}";
            cell.SetActive(true);

            var circle = cell.transform.Find("Circle")?.GetComponent<Image>();
            if (circle) { circle.sprite = atomCircle; circle.color = atom.color; }

            var sym = cell.transform.Find("Symbol")?.GetComponent<TextMeshProUGUI>();
            if (sym) sym.text = atom.symbol;

            var ring = cell.transform.Find("Ring")?.GetComponent<Image>();
            if (ring) ring.gameObject.SetActive(false);

            int idx = i;
            var btn = cell.GetComponent<Button>();
            if (btn) btn.onClick.AddListener(() => OnAtomTapped(idx, cell.transform as RectTransform));

            atomCells.Add(cell);
            atomRings.Add(ring);
        }
    }

    // ── Apertura / cierre ─────────────────────────────────────────────────────
    public void OpenFromButton()
    {
        targetSlot = -1;
        armedAtom  = -1;
        Open();
    }

    void OpenFromSlot(int slot)
    {
        targetSlot = slot;          // apunta a ese slot
        armedAtom  = -1;
        Open();
    }

    void Open()
    {
        if (modalRoot) modalRoot.SetActive(true);
        if (searchInput) searchInput.SetTextWithoutNotify("");
        SetFilter(AtomFilter.Todos);
        RefreshRings();
    }

    public void Close()
    {
        HidePopup();
        armedAtom  = -1;
        targetSlot = -1;
        if (modalRoot) modalRoot.SetActive(false);
        RefreshRings();
    }

    // ── Filtros / búsqueda ────────────────────────────────────────────────────
    void SetFilter(AtomFilter f)
    {
        currentFilter = f;
        if (filterBgs != null && filterLabels != null)
            for (int i = 0; i < filterBgs.Length; i++)
            {
                bool on = (i == (int)f);
                if (filterBgs[i])    filterBgs[i].color    = on ? TAB_ON : TAB_OFF;
                if (filterLabels[i]) filterLabels[i].color = on ? TAB_TXT_ON : TAB_TXT_OFF;
            }
        RefreshGrid();
    }

    void RefreshGrid()
    {
        string q = searchInput ? searchInput.text : "";
        for (int i = 0; i < atomCells.Count; i++)
        {
            var atom = AtomCatalog.All[i];
            bool show = atom.Matches(currentFilter) && atom.MatchesSearch(q);
            if (atomCells[i]) atomCells[i].SetActive(show);
        }
    }

    // ── Toque en un átomo del grid ────────────────────────────────────────────
    void OnAtomTapped(int atomIndex, RectTransform cell)
    {
        ShowPopup(atomIndex, cell);

        if (targetSlot >= 0)
        {
            // Modal apuntando a un slot: mismo = quitar, distinto = poner/reemplazar
            if (slotAtom[targetSlot] == atomIndex) SetSlot(targetSlot, -1);
            else                                   SetSlot(targetSlot, atomIndex);
            targetSlot = -1;
        }
        else
        {
            // Sin slot objetivo: armar el átomo, esperar a que toques un slot
            armedAtom = atomIndex;
        }
        RefreshRings();
    }

    // ── Toque en un slot del hotbar ───────────────────────────────────────────
    void OnSlotTapped(int slot)
    {
        bool modalOpen = modalRoot && modalRoot.activeSelf;

        if (!modalOpen)
        {
            OpenFromSlot(slot);
            return;
        }

        if (armedAtom >= 0)
        {
            SetSlot(slot, armedAtom);   // coloca el átomo armado
            armedAtom = -1;
        }
        else
        {
            targetSlot = slot;          // el próximo átomo actuará sobre este slot
        }
        RefreshRings();
    }

    // ── Estado de slots ───────────────────────────────────────────────────────
    void SetSlot(int slot, int atomIndex)
    {
        slotAtom[slot] = atomIndex;
        RefreshSlot(slot);
    }

    void RefreshSlot(int slot)
    {
        int a = slotAtom[slot];
        bool filled = a >= 0;

        if (slotIcons != null && slotIcons[slot])
        {
            slotIcons[slot].gameObject.SetActive(filled);
            if (filled) { slotIcons[slot].sprite = atomCircle; slotIcons[slot].color = AtomCatalog.All[a].color; }
        }
        if (slotSymbols != null && slotSymbols[slot])
        {
            slotSymbols[slot].gameObject.SetActive(filled);
            if (filled) slotSymbols[slot].text = AtomCatalog.All[a].symbol;
        }
        if (slotBadges != null && slotBadges[slot])
            slotBadges[slot].SetActive(filled);
    }

    // ── Anillos de selección (átomos que están en el hotbar) ──────────────────
    void RefreshRings()
    {
        var inHotbar = new HashSet<int>();
        for (int i = 0; i < SLOTS; i++) if (slotAtom[i] >= 0) inHotbar.Add(slotAtom[i]);

        for (int i = 0; i < atomRings.Count; i++)
            if (atomRings[i])
                atomRings[i].gameObject.SetActive(inHotbar.Contains(i) || i == armedAtom);
    }

    // ── Popup de información ───────────────────────────────────────────────────
    void ShowPopup(int atomIndex, RectTransform near)
    {
        if (!infoPopup || !infoText) return;
        infoText.text = AtomCatalog.All[atomIndex].PopupText;
        infoPopup.gameObject.SetActive(true);
        if (near) infoPopup.position = near.position + new Vector3(0f, near.rect.height * 0.5f + 26f, 0f);

        if (popupCo != null) StopCoroutine(popupCo);
        popupCo = StartCoroutine(HidePopupAfter(POPUP_SECONDS));
    }

    IEnumerator HidePopupAfter(float secs)
    {
        yield return new WaitForSeconds(secs);
        HidePopup();
    }

    void HidePopup()
    {
        if (popupCo != null) { StopCoroutine(popupCo); popupCo = null; }
        if (infoPopup) infoPopup.gameObject.SetActive(false);
    }

    /// <summary>Átomos actualmente en el hotbar (para fases siguientes: colocar en 3D).</summary>
    public IReadOnlyList<int> SlotAtoms => slotAtom;
}
