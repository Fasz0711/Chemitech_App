using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pantalla "Detalle de molécula". Lee la molécula seleccionada en el diario
/// (DiaryDetailContext.Current): llena el panel de datos (fórmula, nombre,
/// descripción, composición, fecha, categoría, solubilidad, masa molar) y muestra
/// la estructura 3D en el visor. Campos sin dato → "—".
/// </summary>
public class MoleculeDetailManager : MonoBehaviour
{
    const string DASH = "—";

    [Header("Header")]
    [SerializeField] private Button btnBack;

    [Header("Datos")]
    [SerializeField] private TextMeshProUGUI formulaLabel;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI descriptionLabel;
    [SerializeField] private RectTransform   compositionRow;
    [SerializeField] private TextMeshProUGUI dateValue;
    [SerializeField] private TextMeshProUGUI categoryValue;
    [SerializeField] private TextMeshProUGUI solubilityValue;
    [SerializeField] private TextMeshProUGUI molarMassValue;

    [Header("Composición (chips)")]
    [SerializeField] private Sprite        atomCircle;
    [SerializeField] private TMP_FontAsset chipFont;

    [Header("Visor 3D")]
    [SerializeField] private MoleculeViewer3D viewer;

    [Header("Capas del visor")]
    [SerializeField] private Toggle tglSymbols;
    [SerializeField] private Toggle tglElectronegativity;
    [SerializeField] private Toggle tglBondTypes;

    [Header("Animación")]
    [SerializeField] private Button          btnPlay;
    [SerializeField] private TextMeshProUGUI captionLabel;

    private AnimationPlayer player;

    [Header("Escenas")]
    [SerializeField] private string escenaDiario = "DiaryScene";

    private void Start()
    {
        if (btnBack) btnBack.onClick.AddListener(() => SceneManager.LoadScene(escenaDiario));

        player = new AnimationPlayer(viewer, this);
        if (btnPlay)
        {
            btnPlay.onClick.AddListener(PlayAnimation);
            btnPlay.gameObject.SetActive(false);
        }
        if (captionLabel) captionLabel.text = "";

        if (tglSymbols)           tglSymbols.onValueChanged.AddListener(_ => ApplyLayers());
        if (tglElectronegativity) tglElectronegativity.onValueChanged.AddListener(_ => ApplyLayers());
        if (tglBondTypes)         tglBondTypes.onValueChanged.AddListener(_ => ApplyLayers());

        var entry = DiaryDetailContext.Current;
        if (entry == null) { Debug.LogWarning("[Detail] Sin molécula en contexto."); Fill(null); return; }
        Fill(entry);
    }

    private void Fill(JournalEntry e)
    {
        string formula = e != null ? e.molecularFormula : null;
        var m = e != null ? e.molecule : null;

        if (formulaLabel)  formulaLabel.text  = string.IsNullOrEmpty(formula) ? DASH : FormatFormula(formula);
        if (nameLabel)     nameLabel.text     = TextOr(m != null ? m.name : null);
        if (descriptionLabel) descriptionLabel.text = TextOr(m != null ? m.description : null);
        if (categoryValue) categoryValue.text = TextOr(m != null ? m.category : null);
        if (solubilityValue) solubilityValue.text = TextOr(m != null ? m.aqueousSolubility : null);
        if (molarMassValue) molarMassValue.text = (m != null && m.molarMass > 0f)
            ? m.molarMass.ToString("0.##", CultureInfo.InvariantCulture) : DASH;
        if (dateValue) dateValue.text = FormatDate(e != null ? e.firstDiscoveredAt : null);

        BuildComposition(m != null ? m.composition : null);

        if (viewer) viewer.Show(m != null ? m.structure : null);
        RefreshLayerToggles();
        ResolveAnimation(m != null ? m.canonicalSmiles : null);
    }

    // ── Animación ────────────────────────────────────────────────────────────────

    /// <summary>Busca si esta molécula tiene guion.
    ///
    /// HAY QUE PEDIRLO APARTE: el listado del diario NO trae la animación, a propósito —
    /// incrusta el detalle completo por entrada y cada molécula arrastraría su guion,
    /// multiplicando una respuesta que ya es cara. Solo la puebla /detection/by-smiles.
    ///
    /// Y tiene que estar aquí: el argumento que decidió el diseño del guion fue que el
    /// alumno que repasa SOLO, en el diario, pueda ver el puente de hidrógeno. Sin este
    /// botón esa razón se queda sin cumplir.</summary>
    private void ResolveAnimation(string canonicalSmiles)
    {
        if (btnPlay) btnPlay.gameObject.SetActive(false);
        if (string.IsNullOrEmpty(canonicalSmiles)) return;

        ApiManager.Instance.GetMoleculeBySmiles(canonicalSmiles,
            onSuccess: resp =>
            {
                if (!this || resp?.molecule == null) return;
                if (!resp.molecule.hasAnimation || resp.molecule.animation == null) return;
                if (!resp.molecule.animation.Has) return;

                if (player != null && player.Load(resp.molecule.animation) && btnPlay)
                    btnPlay.gameObject.SetActive(true);
            },
            onError: (code, detail) => { /* sin guion la pantalla sigue sirviendo */ });
    }

    private void PlayAnimation()
    {
        if (player == null) return;
        if (btnPlay) btnPlay.interactable = false;
        player.Play(onFinished: () => { if (btnPlay) btnPlay.interactable = true; });
        StartCoroutine(FollowCaption());
    }

    private IEnumerator FollowCaption()
    {
        while (player != null && player.IsPlaying)
        {
            if (captionLabel) captionLabel.text = player.Caption;
            yield return null;
        }
        if (captionLabel) captionLabel.text = player?.Caption ?? "";
    }

    // ── Capas ────────────────────────────────────────────────────────────────────

    /// <summary>Los mismos tres interruptores que la pantalla de explicación, sobre el
    /// visor que esta pantalla ya tenía. No se abre un modal encima: sería tapar una
    /// molécula con la misma molécula.
    ///
    /// Un interruptor sin dato se apaga y se deshabilita en vez de esconderse: que la
    /// capa exista y esté vacía dice algo —"de esta molécula no tenemos eso"— y quitarla
    /// lo convertiría en un misterio.</summary>
    private void RefreshLayerToggles()
    {
        SetToggle(tglSymbols,           true,  true);
        SetToggle(tglElectronegativity, false, viewer && viewer.HasElectronegativity);
        SetToggle(tglBondTypes,         false, viewer && viewer.HasBondTypes);
        ApplyLayers();
    }

    private static void SetToggle(Toggle t, bool on, bool usable)
    {
        if (!t) return;
        t.isOn         = on && usable;
        t.interactable = usable;

        var label = t.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label)
        {
            var c = label.color;
            label.color = new Color(c.r, c.g, c.b, usable ? 1f : 0.35f);
        }
    }

    private void ApplyLayers()
    {
        if (!viewer) return;
        viewer.SetLayers(
            tglSymbols           && tglSymbols.isOn,
            tglElectronegativity && tglElectronegativity.isOn,
            tglBondTypes         && tglBondTypes.isOn);
    }

    // ── Composición ──────────────────────────────────────────────────────────────
    private void BuildComposition(JournalComposition[] comps)
    {
        if (!compositionRow) return;
        for (int i = compositionRow.childCount - 1; i >= 0; i--)
            Destroy(compositionRow.GetChild(i).gameObject);

        if (comps == null || comps.Length == 0)
        {
            MakeText(compositionRow, "Empty", DASH, new Vector2(-278f, 0f), new Vector2(60f, 44f), 24f,
                     new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Left);
            return;
        }

        float cursor = -300f;   // borde izquierdo de la fila (ancho 600, centrada)
        const float circleD = 44f, gap = 5f, countW = 48f, plusW = 30f;

        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            int idx = AtomCatalog.IndexOf(c.element);
            Color col = idx >= 0 ? AtomCatalog.All[idx].color : new Color(0.55f, 0.58f, 0.65f);

            // Círculo con símbolo
            var circle = MakeImage(compositionRow, "Atom_" + c.element, new Vector2(cursor + circleD * 0.5f, 0f),
                                   new Vector2(circleD, circleD), col, atomCircle);
            float lum = 0.299f * col.r + 0.587f * col.g + 0.114f * col.b;
            Color txt = lum > 0.65f ? new Color(0.10f, 0.10f, 0.18f) : Color.white;
            MakeText(circle.transform, "Sym", c.element, Vector2.zero, new Vector2(circleD, circleD),
                     c.element != null && c.element.Length > 1 ? 16f : 20f, txt, TextAlignmentOptions.Center);

            // "×N"
            MakeText(compositionRow, "Count_" + c.element, "×" + c.count,
                     new Vector2(cursor + circleD + gap + countW * 0.5f, 0f), new Vector2(countW, 40f),
                     24f, Color.white, TextAlignmentOptions.Left);
            cursor += circleD + gap + countW;

            if (i < comps.Length - 1)
            {
                MakeText(compositionRow, "Plus", "+", new Vector2(cursor + plusW * 0.5f, 2f),
                         new Vector2(plusW, 40f), 28f, new Color(1f, 0.85f, 0.3f, 1f), TextAlignmentOptions.Center);
                cursor += plusW;
            }
        }
    }

    // ── Formato ──────────────────────────────────────────────────────────────────
    private static string TextOr(string s) => string.IsNullOrWhiteSpace(s) ? DASH : s;

    private static string FormatFormula(string f)
    {
        if (string.IsNullOrEmpty(f)) return DASH;
        return System.Text.RegularExpressions.Regex.Replace(f, "([0-9]+)", "<sub>$1</sub>");
    }

    private static string FormatDate(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return DASH;
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        return DASH;
    }

    // ── UI helpers (para los chips de composición) ────────────────────────────────
    private Image MakeImage(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite spr)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color; if (spr != null) img.sprite = spr;
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size,
                                     float fontSize, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = chipFont; tmp.fontSize = fontSize; tmp.color = color;
        tmp.alignment = align; tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false; tmp.raycastTarget = false;
        return tmp;
    }
}
