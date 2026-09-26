using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// La pantalla de explicación de una molécula: el modelo 3D con sus capas activables,
/// al lado de la tarjeta con la fórmula.
///
/// Es el ítem 2 del plan de validación: "capas activables sobre el modelo 3D (símbolos,
/// electronegatividad, tipo de enlace con δ+/δ−) junto a una tarjeta con la fórmula
/// estructural en 2D". La tarjeta 2D todavía no se puede dibujar —el endpoint no
/// existe— así que de momento muestra la fórmula molecular y se queda esperando.
///
/// ES UN PANEL, NO UNA ESCENA, a propósito: se abre encima de la pizarra y al cerrarlo
/// el alumno vuelve exactamente donde estaba, con la clase sin interrumpir y sin haber
/// perdido su vista. Cargar una escena para esto obligaría a reconstruir la clase al
/// volver.
///
/// Lee ExplanationContext, que ya viene relleno por quien abrió el panel.
/// </summary>
public class ExplanationPanel : MonoBehaviour
{
    [Header("Identidad")]
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI formulaLabel;

    [Header("Modelo 3D")]
    [SerializeField] private MoleculeViewer3D viewer;

    [Header("Capas")]
    [SerializeField] private Toggle tglSymbols;
    [SerializeField] private Toggle tglElectronegativity;
    [SerializeField] private Toggle tglBondTypes;

    [Header("Tarjeta 2D")]
    [SerializeField] private GameObject      card2D;
    [SerializeField] private TextMeshProUGUI card2DText;

    [Header("Salir")]
    [SerializeField] private Button btnClose;

    void Awake()
    {
        if (btnClose) btnClose.onClick.AddListener(Close);

        if (tglSymbols)           tglSymbols.onValueChanged.AddListener(_ => ApplyLayers());
        if (tglElectronegativity) tglElectronegativity.onValueChanged.AddListener(_ => ApplyLayers());
        if (tglBondTypes)         tglBondTypes.onValueChanged.AddListener(_ => ApplyLayers());
    }

    /// <summary>Se rellena al ENCENDERSE, no al abrirse desde fuera: así quien lo abre
    /// solo tiene que dejar la molécula en el contexto y activar el objeto, sin saber
    /// nada de lo que hay aquí dentro.</summary>
    void OnEnable()
    {
        if (!ExplanationContext.Has) { Close(); return; }

        if (nameLabel)
            nameLabel.text = string.IsNullOrEmpty(ExplanationContext.Name)
                ? "Estructura sin identificar"
                : ExplanationContext.Name;

        if (formulaLabel) formulaLabel.text = ExplanationContext.Formula ?? "";

        if (viewer) viewer.Show(ExplanationContext.Atoms, ExplanationContext.Bonds);

        // Un interruptor que no puede hacer nada se APAGA Y SE DESHABILITA en vez de
        // esconderse: que la capa exista y esté vacía es información —"de esta molécula
        // no tenemos ese dato"— y esconder el interruptor lo convertiría en un misterio.
        SetToggle(tglSymbols,           true,  true);
        SetToggle(tglElectronegativity, false, viewer && viewer.HasElectronegativity);
        SetToggle(tglBondTypes,         false, viewer && viewer.HasBondTypes);

        ApplyLayers();
        RefreshCard();
    }

    void OnDisable()
    {
        // El visor mantiene esferas, cilindros y una RenderTexture vivos. Con el panel
        // cerrado no se ven, y detrás hay una clase sondeando cada dos segundos.
        if (viewer) viewer.Clear();
    }

    /// <summary>Deja un interruptor en su estado inicial y dice si se puede usar.
    ///
    /// 'usable' en false significa que ESA molécula no trae el dato —el diario no manda
    /// electronegatividad, y una molécula sin enlaces polares no tiene δ que enseñar—.
    /// Se apaga y se atenúa en vez de esconderse: que la capa exista y esté vacía ya es
    /// información, y quitar el interruptor lo convertiría en un misterio.</summary>
    static void SetToggle(Toggle t, bool on, bool usable)
    {
        if (!t) return;

        t.isOn         = on && usable;
        t.interactable = usable;

        // El tinte de Unity solo afecta al gráfico del propio Toggle, no a su etiqueta,
        // así que la atenuación hay que hacerla a mano o el texto se queda encendido
        // junto a una casilla apagada.
        var label = t.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label)
        {
            var c = label.color;
            label.color = new Color(c.r, c.g, c.b, usable ? 1f : 0.35f);
        }
    }

    void ApplyLayers()
    {
        if (!viewer) return;
        viewer.SetLayers(
            tglSymbols            && tglSymbols.isOn,
            tglElectronegativity  && tglElectronegativity.isOn,
            tglBondTypes          && tglBondTypes.isOn);
    }

    /// <summary>La tarjeta con la notación del libro. Mientras el servidor no mande las
    /// coordenadas 2D se muestra la fórmula molecular, que ya es media comparación: el
    /// alumno ve "H2O" al lado de la forma doblada. El puente completo —tocar el oxígeno
    /// en la tarjeta y que se encienda en el 3D— necesita las coordenadas.</summary>
    void RefreshCard()
    {
        if (!card2D) return;

        string formula = ExplanationContext.Formula ?? "";
        card2D.SetActive(!string.IsNullOrEmpty(formula));
        if (card2DText) card2DText.text = formula;
    }

    public void Close() => gameObject.SetActive(false);
}
