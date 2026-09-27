using System.Collections;
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
    [SerializeField] private TextMeshProUGUI card2DText;   // respaldo: la fórmula molecular
    [SerializeField] private Structure2DCard structure2D;  // la fórmula estructural dibujada

    [Header("Animación")]
    [SerializeField] private Button          btnPlay;
    [SerializeField] private TextMeshProUGUI captionLabel;

    [Header("Salir")]
    [SerializeField] private Button btnClose;

    AnimationPlayer player;

    void Awake()
    {
        if (btnClose) btnClose.onClick.AddListener(Close);

        if (tglSymbols)           tglSymbols.onValueChanged.AddListener(_ => ApplyLayers());
        if (tglElectronegativity) tglElectronegativity.onValueChanged.AddListener(_ => ApplyLayers());
        if (tglBondTypes)         tglBondTypes.onValueChanged.AddListener(_ => ApplyLayers());

        if (btnPlay) btnPlay.onClick.AddListener(PlayAnimation);
        player = new AnimationPlayer(viewer, this);
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

        if (captionLabel) captionLabel.text = "";
        if (btnPlay) btnPlay.gameObject.SetActive(false);
        ResolveAnimation();
    }

    void OnDisable()
    {
        // El visor mantiene esferas, cilindros y una RenderTexture vivos. Con el panel
        // cerrado no se ven, y detrás hay una clase sondeando cada dos segundos.
        player?.Stop();
        if (viewer)      viewer.Clear();
        if (structure2D) structure2D.Clear();
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

    // ── Animación ─────────────────────────────────────────────────────────────

    /// <summary>Consigue el guion, si esta molécula tiene.
    ///
    /// Desde el universo y el diario ya viene en el contexto, porque esos entran por
    /// /detection/by-smiles. DESDE LA PIZARRA NO: allí el contexto se llena del estado de
    /// la clase, que no incluye guiones, así que hay que pedirlo. Se hace en segundo
    /// plano y el botón aparece cuando llega: la pantalla ya es útil sin él.</summary>
    void ResolveAnimation()
    {
        if (ExplanationContext.Animation != null) { OfferPlay(ExplanationContext.Animation); return; }

        string smiles = ExplanationContext.CanonicalSmiles;
        if (string.IsNullOrEmpty(smiles)) return;

        ApiManager.Instance.GetMoleculeBySmiles(smiles,
            onSuccess: resp =>
            {
                // Se pudo haber cerrado mientras llegaba, o haber abierto OTRA molécula.
                if (!gameObject.activeInHierarchy) return;
                if (resp?.molecule == null) return;
                if (resp.molecule.canonicalSmiles != ExplanationContext.CanonicalSmiles) return;

                if (resp.molecule.hasAnimation && resp.molecule.animation != null
                                               && resp.molecule.animation.Has)
                {
                    ExplanationContext.Animation = resp.molecule.animation;
                    OfferPlay(resp.molecule.animation);
                }
            },
            onError: (code, detail) => { /* sin guion la pantalla sigue sirviendo */ });
    }

    void OfferPlay(MoleculeAnimation animation)
    {
        if (!btnPlay || player == null) return;
        if (!player.Load(animation)) return;
        btnPlay.gameObject.SetActive(true);
    }

    void PlayAnimation()
    {
        if (player == null) return;

        // Las capas se apagan al reproducir: durante la animación lo que importa es el
        // movimiento, y tres capas de etiquetas encima lo tapan.
        SetToggle(tglSymbols, true, true);
        SetToggle(tglElectronegativity, false, viewer && viewer.HasElectronegativity);
        SetToggle(tglBondTypes, false, viewer && viewer.HasBondTypes);
        ApplyLayers();

        if (btnPlay) btnPlay.interactable = false;
        player.Play(onFinished: () => { if (btnPlay) btnPlay.interactable = true; });
        StartCoroutine(FollowCaption());
    }

    IEnumerator FollowCaption()
    {
        while (player != null && player.IsPlaying)
        {
            if (captionLabel) captionLabel.text = player.Caption;
            yield return null;
        }
        if (captionLabel) captionLabel.text = player?.Caption ?? "";
    }

    void ApplyLayers()
    {
        if (!viewer) return;
        viewer.SetLayers(
            tglSymbols            && tglSymbols.isOn,
            tglElectronegativity  && tglElectronegativity.isOn,
            tglBondTypes          && tglBondTypes.isOn);
    }

    /// <summary>La tarjeta con la notación del libro, al lado de la forma espacial.
    ///
    /// Se dibuja la fórmula ESTRUCTURAL si el servidor mandó coordenadas. Si no —le pasa a
    /// la sal, cuya red de 27 iones no tiene fórmula plana— se cae a la fórmula
    /// molecular, que sigue siendo media comparación: el alumno ve "NaCl" junto a la red.
    /// Quedarse sin tarjeta no sería mejor, sería menos.</summary>
    void RefreshCard()
    {
        if (!card2D) return;

        bool drawn = structure2D && structure2D.Show(ExplanationContext.Flat2D,
                                                     ExplanationContext.Atoms,
                                                     ExplanationContext.Bonds);

        string formula = ExplanationContext.Formula ?? "";
        if (card2DText)
        {
            card2DText.gameObject.SetActive(!drawn && !string.IsNullOrEmpty(formula));
            card2DText.text = formula;
        }

        card2D.SetActive(drawn || !string.IsNullOrEmpty(formula));
    }

    public void Close() => gameObject.SetActive(false);
}
