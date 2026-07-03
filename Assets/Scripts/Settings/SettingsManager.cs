using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pantalla "Ajustes". Solo interfaz por ahora: navega entre pestañas
/// (Gráficos / Audio / Cuenta), resalta los segmentos (Bajo/Medio/Alto) y
/// actualiza el valor de los sliders. La funcionalidad real (aplicar/guardar,
/// acciones de cuenta) se hará luego.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private Button btnBack;
    [SerializeField] private string escenaMenu = "SampleScene";

    [Header("Pestañas")]
    [SerializeField] private Button[]     tabButtons;  // Gráficos, Audio, Cuenta
    [SerializeField] private GameObject[] tabPanels;   // mismo orden

    [Header("Gráficos — segmentos")]
    [SerializeField] private Button[] qualityButtons;  // Bajo, Medio, Alto
    [SerializeField] private Button[] fxButtons;       // Bajo, Medio, Alto

    [Header("Sliders")]
    [SerializeField] private Slider sliderBrillo;  [SerializeField] private TextMeshProUGUI lblBrillo;
    [SerializeField] private Slider sliderMusica;  [SerializeField] private TextMeshProUGUI lblMusica;
    [SerializeField] private Slider sliderEfectos; [SerializeField] private TextMeshProUGUI lblEfectos;

    [Header("Cuenta")]
    [SerializeField] private Button btnCerrarSesion;
    [SerializeField] private Button btnCambiarContrasena;
    [SerializeField] private Button btnEliminarCuenta;

    static readonly Color SEL_BG    = new Color(0.18f, 0.82f, 0.88f, 1f); // cyan
    static readonly Color UNSEL_BG  = new Color(0.18f, 0.20f, 0.44f, 1f);
    static readonly Color SEL_TXT   = new Color(0.04f, 0.18f, 0.27f, 1f);
    static readonly Color UNSEL_TXT = Color.white;

    private void Start()
    {
        if (btnBack) btnBack.onClick.AddListener(() => SceneManager.LoadScene(escenaMenu));

        Wire(tabButtons, SelectTab);
        Wire(qualityButtons, i => Highlight(qualityButtons, i));
        Wire(fxButtons,      i => Highlight(fxButtons, i));

        WireSlider(sliderBrillo,  lblBrillo);
        WireSlider(sliderMusica,  lblMusica);
        WireSlider(sliderEfectos, lblEfectos);

        if (btnCerrarSesion)      btnCerrarSesion.onClick.AddListener(() => Debug.Log("[Settings] Cerrar Sesión (placeholder)."));
        if (btnCambiarContrasena) btnCambiarContrasena.onClick.AddListener(() => Debug.Log("[Settings] Cambiar Contraseña (placeholder)."));
        if (btnEliminarCuenta)    btnEliminarCuenta.onClick.AddListener(() => Debug.Log("[Settings] Eliminar Cuenta (placeholder)."));

        // Estado inicial (según mockups)
        SelectTab(0);
        Highlight(qualityButtons, 2); // Alto
        Highlight(fxButtons, 1);      // Medio
        RefreshLabel(sliderBrillo,  lblBrillo);
        RefreshLabel(sliderMusica,  lblMusica);
        RefreshLabel(sliderEfectos, lblEfectos);
    }

    private void Wire(Button[] btns, System.Action<int> onSelect)
    {
        if (btns == null) return;
        for (int i = 0; i < btns.Length; i++)
        {
            int idx = i;
            if (btns[i]) btns[i].onClick.AddListener(() => onSelect(idx));
        }
    }

    private void WireSlider(Slider s, TextMeshProUGUI lbl)
    {
        if (s != null) s.onValueChanged.AddListener(_ => RefreshLabel(s, lbl));
    }

    private void RefreshLabel(Slider s, TextMeshProUGUI lbl)
    {
        if (s && lbl) lbl.text = Mathf.RoundToInt(s.value).ToString();
    }

    private void SelectTab(int index)
    {
        if (tabPanels != null)
            for (int i = 0; i < tabPanels.Length; i++)
                if (tabPanels[i]) tabPanels[i].SetActive(i == index);
        Highlight(tabButtons, index);
    }

    private void Highlight(Button[] btns, int selected)
    {
        if (btns == null) return;
        for (int i = 0; i < btns.Length; i++)
        {
            if (!btns[i]) continue;
            var img = btns[i].GetComponent<Image>();
            if (img) img.color = (i == selected) ? SEL_BG : UNSEL_BG;
            var lbl = btns[i].GetComponentInChildren<TextMeshProUGUI>();
            if (lbl) lbl.color = (i == selected) ? SEL_TXT : UNSEL_TXT;
        }
    }
}
