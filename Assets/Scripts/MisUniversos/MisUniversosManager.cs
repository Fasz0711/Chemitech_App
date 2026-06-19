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

    private void Start()
    {
        if (btnAtras)      btnAtras.onClick.AddListener(() => SceneManager.LoadScene(escenaAtras));
        if (btnCrearEmpty) btnCrearEmpty.onClick.AddListener(() => SceneManager.LoadScene(escenaCrear));
        if (btnCrearList)  btnCrearList.onClick.AddListener(() => SceneManager.LoadScene(escenaCrear));

        if (cardTemplate) cardTemplate.SetActive(false);

        Refresh();
    }

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
                Debug.Log($"[MisUniversos] Jugar '{captured.name}' (id={captured.id}) — pendiente."));
        }

        var btnEditar = card.transform.Find("BtnEditar")?.GetComponent<Button>();
        if (btnEditar)
        {
            var captured = u;
            btnEditar.onClick.AddListener(() =>
                Debug.Log($"[MisUniversos] Editar '{captured.name}' (id={captured.id}) — pendiente."));
        }
    }
}
