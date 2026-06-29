using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MisUniversosManager : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private Button          btnAtras;
    [SerializeField] private TextMeshProUGUI countBadge;
    [SerializeField] private GameObject      countBadgeGroup;

    [Header("Estados")]
    [SerializeField] private GameObject emptyStateGroup;
    [SerializeField] private GameObject listGroup;

    [Header("Lista")]
    [SerializeField] private RectTransform listContent;   // contenido del ScrollRect
    [SerializeField] private GameObject    cardTemplate;  // tarjeta plantilla (inactiva)
    [SerializeField] private Sprite[]      iconSprites;   // 8 íconos, mismo orden que UniverseTheme

    [Header("Botones crear")]
    [SerializeField] private Button btnCrearEmpty;
    [SerializeField] private Button btnCrearList;

    [Header("Escenas")]
    [SerializeField] private string escenaAtras = "SampleScene";
    [SerializeField] private string escenaCrear = "CrearUniversoScene";

    [Header("Modal: sin espacio (crear)")]
    [SerializeField] private GameObject      storageModal;
    [SerializeField] private Button          btnStorageVolver;
    [SerializeField] private Image           storageBarFill;     // Image type Filled (horizontal)
    [SerializeField] private TextMeshProUGUI storageValueLabel;  // "14.8 GB / 15 GB"

    [Header("Modal: universo dañado (abrir)")]
    [SerializeField] private GameObject corruptModal;
    [SerializeField] private Button     btnCorruptVolver;

    private void Start()
    {
        if (btnAtras)      btnAtras.onClick.AddListener(() => SceneManager.LoadScene(escenaAtras));
        if (btnCrearEmpty) btnCrearEmpty.onClick.AddListener(() => SceneManager.LoadScene(escenaCrear));
        if (btnCrearList)  btnCrearList.onClick.AddListener(() => SceneManager.LoadScene(escenaCrear));

        if (btnStorageVolver) btnStorageVolver.onClick.AddListener(HideStorageModal);
        if (btnCorruptVolver) btnCorruptVolver.onClick.AddListener(HideCorruptModal);
        HideStorageModal();
        HideCorruptModal();

        if (cardTemplate) cardTemplate.SetActive(false);

        Refresh();

        // Aviso pendiente desde CrearUniverso (no se pudo guardar por espacio).
        if (UniverseNotice.PendingStorageFull)
        {
            UniverseNotice.PendingStorageFull = false;
            ShowStorageModal();
        }
    }

    // ── Modales de error ──────────────────────────────────────────────────────
    private void ShowStorageModal()
    {
        if (storageModal) storageModal.SetActive(true);

        var (used, total) = DeviceStorage.Get();
        if (total > 0)
        {
            if (storageBarFill)    storageBarFill.fillAmount = Mathf.Clamp01((float)((double)used / total));
            if (storageValueLabel) storageValueLabel.text = $"{DeviceStorage.ToGB(used):0.0} GB / {DeviceStorage.ToGB(total):0} GB";
        }
        else
        {
            if (storageBarFill)    storageBarFill.fillAmount = 0.95f;
            if (storageValueLabel) storageValueLabel.text = "";
        }
    }

    private void ShowCorruptModal() { if (corruptModal) corruptModal.SetActive(true); }
    private void HideStorageModal() { if (storageModal) storageModal.SetActive(false); }
    private void HideCorruptModal() { if (corruptModal) corruptModal.SetActive(false); }

    private void Refresh()
    {
        var col = UniverseStore.Load();
        int  n  = col.universes.Count;
        bool any = n > 0;

        if (countBadge)      countBadge.text = n.ToString();
        if (countBadgeGroup) countBadgeGroup.SetActive(any);

        if (emptyStateGroup) emptyStateGroup.SetActive(!any);
        if (listGroup)       listGroup.SetActive(any);

        if (any) PopulateList(col);
    }

    private void PopulateList(UniverseCollection col)
    {
        if (!listContent || !cardTemplate) return;

        // Limpia clones previos (conserva la plantilla)
        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            var child = listContent.GetChild(i).gameObject;
            if (child != cardTemplate) Destroy(child);
        }

        foreach (var u in col.universes)
            BuildCard(u);
    }

    private void BuildCard(UniverseData u)
    {
        var card = Instantiate(cardTemplate, listContent);
        card.name = $"Card_{u.name}";
        card.SetActive(true);

        var frame = card.transform.Find("IconFrame")?.GetComponent<Image>();
        if (frame) frame.color = UniverseTheme.ColorAt(u.colorIndex);

        var icon = card.transform.Find("IconFrame/Icon")?.GetComponent<Image>();
        if (icon && iconSprites != null && u.iconIndex >= 0 && u.iconIndex < iconSprites.Length)
            icon.sprite = iconSprites[u.iconIndex];

        var nameLbl = card.transform.Find("TextColumn/NameLabel")?.GetComponent<TextMeshProUGUI>();
        if (nameLbl) nameLbl.text = u.name;

        var timeLbl = card.transform.Find("TextColumn/TimeLabel")?.GetComponent<TextMeshProUGUI>();
        if (timeLbl) timeLbl.text = UniverseTheme.TimeAgo(u.createdAtTicks);

        var btnJugar = card.transform.Find("BtnJugar")?.GetComponent<Button>();
        if (btnJugar)
        {
            var captured = u;
            btnJugar.onClick.AddListener(() =>
            {
                PlayContext.Current = captured;
                SceneManager.LoadScene("ZonaJuegoScene");
            });
        }

        var btnEditar = card.transform.Find("BtnEditar")?.GetComponent<Button>();
        if (btnEditar)
        {
            var captured = u;
            btnEditar.onClick.AddListener(() =>
            {
                UniverseEditContext.Current = captured;
                SceneManager.LoadScene("EditarUniversoScene");
            });
        }
    }
}
