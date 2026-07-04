using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pantalla "Diario de Moléculas".
///  • Invitado   → vista bloqueada (iniciar sesión / crear cuenta).
///  • Con sesión → grid de moléculas descubiertas (GET /journal/{userPublicId}),
///    con estado vacío si aún no descubrió ninguna. Cada tarjeta muestra el ícono
///    2D de la molécula, su fórmula y su nombre; al tocarla guarda la molécula en
///    DiaryDetailContext para una futura pantalla de detalle.
/// </summary>
public class DiaryManager : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private Button btnBack;
    [SerializeField] private GameObject guestBadge;       // "Modo invitado"
    [SerializeField] private GameObject molCountBadge;    // "X Moléculas Descubiertas"
    [SerializeField] private TextMeshProUGUI molCountLabel;

    [Header("Vistas")]
    [SerializeField] private GameObject guestView;        // bloqueado
    [SerializeField] private GameObject loggedInView;     // diario

    [Header("Diario (con sesión)")]
    [SerializeField] private GameObject    emptyState;    // sin moléculas
    [SerializeField] private GameObject    gridGroup;     // scroll con el grid
    [SerializeField] private RectTransform gridContent;   // contenedor de tarjetas
    [SerializeField] private GameObject    cardTemplate;  // tarjeta plantilla (inactiva)
    [SerializeField] private Sprite        atomCircle;    // sprite del círculo de átomo
    [SerializeField] private TMP_FontAsset diaryFont;     // fuente para los símbolos

    [Header("Botones invitado")]
    [SerializeField] private Button btnIniciarSesion;
    [SerializeField] private Button btnCrearCuenta;

    [Header("Botón estado vacío")]
    [SerializeField] private Button btnEmpezarJugar;

    [Header("Escenas")]
    [SerializeField] private string escenaMenu     = "SampleScene";
    [SerializeField] private string escenaLogin    = "LoginScene";
    [SerializeField] private string escenaRegistro = "RegisterEmailScene";
    [SerializeField] private string escenaJugar    = "MisUniversosScene";
    [SerializeField] private string escenaDetalle  = "MoleculeDetailScene";

    private void Start()
    {
        if (btnBack)          btnBack.onClick.AddListener(() => SceneManager.LoadScene(escenaMenu));
        if (btnIniciarSesion) btnIniciarSesion.onClick.AddListener(() => SceneManager.LoadScene(escenaLogin));
        if (btnCrearCuenta)   btnCrearCuenta.onClick.AddListener(() => SceneManager.LoadScene(escenaRegistro));
        if (btnEmpezarJugar)  btnEmpezarJugar.onClick.AddListener(() => SceneManager.LoadScene(escenaJugar));

        if (cardTemplate) cardTemplate.SetActive(false);

        bool logged = SessionData.IsLoggedIn;
        if (guestView)     guestView.SetActive(!logged);
        if (loggedInView)  loggedInView.SetActive(logged);
        if (guestBadge)    guestBadge.SetActive(!logged);
        if (molCountBadge) molCountBadge.SetActive(logged);

        if (logged) LoadJournal();
    }

    // ── Carga del diario ────────────────────────────────────────────────────────
    private void LoadJournal()
    {
        if (emptyState) emptyState.SetActive(false);
        if (gridGroup)  gridGroup.SetActive(false);
        if (molCountLabel) molCountLabel.text = "0 Moléculas Descubiertas";

        string uid = SessionData.UserId;
        if (string.IsNullOrEmpty(uid)) { ShowEmpty(); return; }

        ApiManager.Instance.GetJournal(uid,
            onSuccess: entries =>
            {
                if (this == null) return;
                int n = entries != null ? entries.Length : 0;
                if (molCountLabel) molCountLabel.text = $"{n} Moléculas Descubiertas";
                if (n == 0) { ShowEmpty(); return; }
                ShowGrid(entries);
            },
            onError: (code, detail) =>
            {
                Debug.LogWarning($"[Diary] No se pudo cargar el diario · code={code} · {detail}");
                if (this == null) return;
                ShowEmpty();
            });
    }

    private void ShowEmpty()
    {
        if (emptyState) emptyState.SetActive(true);
        if (gridGroup)  gridGroup.SetActive(false);
    }

    private void ShowGrid(JournalEntry[] entries)
    {
        if (emptyState) emptyState.SetActive(false);
        if (gridGroup)  gridGroup.SetActive(true);
        PopulateGrid(entries);
    }

    private void PopulateGrid(JournalEntry[] entries)
    {
        if (!gridContent || !cardTemplate) return;

        // Limpia clones previos (conserva la plantilla)
        for (int i = gridContent.childCount - 1; i >= 0; i--)
        {
            var child = gridContent.GetChild(i).gameObject;
            if (child != cardTemplate) Destroy(child);
        }

        foreach (var e in entries) BuildCard(e);
    }

    private void BuildCard(JournalEntry e)
    {
        if (e == null) return;

        var card = Instantiate(cardTemplate, gridContent);
        card.name = $"Card_{e.molecularFormula}";
        card.SetActive(true);

        var formula = card.transform.Find("Body/Formula")?.GetComponent<TextMeshProUGUI>();
        if (formula) formula.text = FormatFormula(e.molecularFormula);

        // Nombre: solo si la molécula lo trae (las exóticas se quedan solo con fórmula)
        var nameLbl = card.transform.Find("Body/Name")?.GetComponent<TextMeshProUGUI>();
        string molName = e.molecule != null ? e.molecule.name : null;
        if (nameLbl)
        {
            bool hasName = !string.IsNullOrEmpty(molName);
            nameLbl.gameObject.SetActive(hasName);
            if (hasName) nameLbl.text = molName;
        }

        // Ícono 2D dibujado desde la estructura real
        var iconArea = card.transform.Find("Body/IconArea") as RectTransform;
        var structure = e.molecule != null ? e.molecule.structure : null;
        if (iconArea) MoleculeIcon.Render(iconArea, structure, atomCircle, diaryFont);

        // Tarjeta cableada hacia un futuro detalle
        var btn = card.GetComponent<Button>();
        if (btn)
        {
            var captured = e;
            btn.onClick.AddListener(() =>
            {
                DiaryDetailContext.Current = captured;
                SceneManager.LoadScene(escenaDetalle);
            });
        }
    }

    /// <summary>"H2O" → "H&lt;sub&gt;2&lt;/sub&gt;O" (subíndices como en el mockup).</summary>
    private static string FormatFormula(string f)
    {
        if (string.IsNullOrEmpty(f)) return "";
        return System.Text.RegularExpressions.Regex.Replace(f, "([0-9]+)", "<sub>$1</sub>");
    }
}
