using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;

/// <summary>
/// Edición de un universo existente (cargado desde UniverseEditContext.Current).
/// Permite cambiar nombre/ícono/color (Guardar = Update) y eliminarlo (con modal
/// de confirmación). Edit y delete son locales (UniverseStore).
/// </summary>
public class EditarUniversoManager : MonoBehaviour
{
    const int MAX_CHARS = 25;

    [Header("Input nombre")]
    [SerializeField] private TMP_InputField inputName;
    [SerializeField] private TextMeshProUGUI counterLabel;
    [SerializeField] private TextMeshProUGUI validationLabel;

    [Header("Vista previa")]
    [SerializeField] private Image           previewCircle;
    [SerializeField] private Image           previewIcon;
    [SerializeField] private TextMeshProUGUI previewNameLabel;

    [Header("Íconos (8)")]
    [SerializeField] private Button[]  iconButtons;
    [SerializeField] private Image[]   iconBorders;

    [Header("Colores (6)")]
    [SerializeField] private Button[]          colorButtons;
    [SerializeField] private TextMeshProUGUI[] colorChecks;

    [Header("Botones")]
    [SerializeField] private Button btnAtras;
    [SerializeField] private Button btnCancelar;
    [SerializeField] private Button btnGuardar;
    [SerializeField] private Button btnEliminar;

    [Header("Modal eliminar")]
    [SerializeField] private GameObject deleteModal;
    [SerializeField] private Button     btnModalCancelar;
    [SerializeField] private Button     btnModalConfirmar;

    [Header("Escenas")]
    [SerializeField] private string escenaAtras = "MisUniversosScene";

    static readonly Color COL_ICON_SEL   = new Color(0.30f, 0.85f, 0.91f, 1f);
    static readonly Color COL_ICON_UNSEL = new Color(0.12f, 0.14f, 0.28f, 0.85f);

    static readonly Color[] THEME_COLORS =
    {
        new Color(0.30f, 0.85f, 0.91f, 1f), // teal
        new Color(0.90f, 0.46f, 0.71f, 1f), // pink
        new Color(0.61f, 0.35f, 0.71f, 1f), // purple
        new Color(0.95f, 0.77f, 0.06f, 1f), // yellow
        new Color(0.18f, 0.80f, 0.44f, 1f), // green
        new Color(0.91f, 0.30f, 0.24f, 1f), // red
    };

    UniverseData _editing;
    int selIcon;
    int selColor;

    void Start()
    {
        _editing = UniverseEditContext.Current;
        if (_editing == null)
        {
            Debug.LogWarning("[EditarUniverso] No hay universo en contexto; vuelvo a la lista.");
            SceneManager.LoadScene(escenaAtras);
            return;
        }

        if (inputName)
        {
            inputName.characterLimit = MAX_CHARS;
            inputName.onValueChanged.AddListener(OnNameChanged);
        }

        if (btnAtras)    btnAtras.onClick.AddListener(GoBack);
        if (btnCancelar) btnCancelar.onClick.AddListener(GoBack);
        if (btnGuardar)  btnGuardar.onClick.AddListener(OnGuardar);
        if (btnEliminar) btnEliminar.onClick.AddListener(ShowDeleteModal);

        if (btnModalCancelar)  btnModalCancelar.onClick.AddListener(HideDeleteModal);
        if (btnModalConfirmar) btnModalConfirmar.onClick.AddListener(OnConfirmDelete);

        for (int i = 0; i < iconButtons.Length; i++)
        {
            int idx = i;
            if (iconButtons[i]) iconButtons[i].onClick.AddListener(() => SelectIcon(idx));
        }
        for (int i = 0; i < colorButtons.Length; i++)
        {
            int idx = i;
            if (colorButtons[i]) colorButtons[i].onClick.AddListener(() => SelectColor(idx));
        }

        // Cargar datos del universo a editar
        selIcon  = Mathf.Clamp(_editing.iconIndex,  0, Mathf.Max(0, iconButtons.Length  - 1));
        selColor = Mathf.Clamp(_editing.colorIndex, 0, Mathf.Max(0, colorButtons.Length - 1));
        if (inputName) inputName.text = _editing.name;

        SelectIcon(selIcon);
        SelectColor(selColor);
        OnNameChanged(inputName ? inputName.text : "");
        HideDeleteModal();
    }

    // ── Edición ─────────────────────────────────────────────────────────────────
    void OnNameChanged(string value)
    {
        if (counterLabel) counterLabel.text = $"{value.Length} / {MAX_CHARS}";

        bool hasInvalid = Regex.IsMatch(value, "[<>:\"|?]");
        bool showError  = string.IsNullOrEmpty(value.Trim()) || hasInvalid;
        if (validationLabel) validationLabel.gameObject.SetActive(showError);

        UpdatePreview();
    }

    void SelectIcon(int idx)
    {
        selIcon = idx;
        for (int i = 0; i < iconBorders.Length; i++)
            if (iconBorders[i]) iconBorders[i].color = (i == idx) ? COL_ICON_SEL : COL_ICON_UNSEL;

        if (previewIcon && iconButtons != null && idx < iconButtons.Length && iconButtons[idx])
        {
            var t = iconButtons[idx].transform;
            var child = t.childCount > 0 ? t.GetChild(0).GetComponent<Image>() : null;
            if (child) previewIcon.sprite = child.sprite;
        }
    }

    void SelectColor(int idx)
    {
        selColor = idx;
        for (int i = 0; i < colorChecks.Length; i++)
            if (colorChecks[i]) colorChecks[i].gameObject.SetActive(i == idx);
        UpdatePreview();
    }

    void UpdatePreview()
    {
        if (previewCircle) previewCircle.color = THEME_COLORS[Mathf.Clamp(selColor, 0, THEME_COLORS.Length - 1)];
        string name = inputName ? inputName.text.Trim() : "";
        if (previewNameLabel) previewNameLabel.text = string.IsNullOrEmpty(name) ? "Universo" : name;
    }

    void OnGuardar()
    {
        string name = inputName ? inputName.text.Trim() : "";
        bool hasInvalid = Regex.IsMatch(name, "[<>:\"|?]");
        if (string.IsNullOrEmpty(name) || hasInvalid)
        {
            if (validationLabel) validationLabel.gameObject.SetActive(true);
            return;
        }

        var updated = new UniverseData
        {
            id             = _editing.id,
            name           = name,
            iconIndex      = ResolveCanonicalIconIndex(),
            colorIndex     = selColor,
            createdAtTicks = _editing.createdAtTicks,
        };
        UniverseStore.Update(updated);
        Debug.Log($"[EditarUniverso] Guardado: nombre={name} icono={updated.iconIndex} color={selColor} id={updated.id}");
        GoBack();
    }

    // ── Eliminar ────────────────────────────────────────────────────────────────
    void ShowDeleteModal()  { if (deleteModal) deleteModal.SetActive(true); }
    void HideDeleteModal()  { if (deleteModal) deleteModal.SetActive(false); }

    void OnConfirmDelete()
    {
        UniverseStore.Remove(_editing.id);
        Debug.Log($"[EditarUniverso] Eliminado: id={_editing.id} nombre={_editing.name}");

        // Decrementa el contador de universos en el journal (solo con sesión real).
        if (!string.IsNullOrEmpty(SessionData.UserId))
        {
            ApiManager.Instance.DecrementCreatedUniverse(SessionData.UserId,
                onSuccess: stats => Debug.Log($"[EditarUniverso] Journal actualizado · totalCreatedUniverses={stats.totalCreatedUniverses}"),
                onError: (code, detail) => Debug.LogWarning($"[EditarUniverso] No se pudo decrementar el journal · code={code} · {detail}"));
        }

        GoBack();
    }

    // ── Navegación ────────────────────────────────────────────────────────────────
    void GoBack() => SceneManager.LoadScene(escenaAtras);

    // Índice canónico del ícono según el sprite seleccionado (no depende del orden del array).
    int ResolveCanonicalIconIndex()
    {
        if (iconButtons != null && selIcon >= 0 && selIcon < iconButtons.Length && iconButtons[selIcon] != null)
        {
            var t = iconButtons[selIcon].transform;
            var child = t.childCount > 0 ? t.GetChild(0).GetComponent<Image>() : null;
            if (child != null && child.sprite != null)
            {
                int canonical = System.Array.IndexOf(UniverseTheme.IconNames, child.sprite.name);
                if (canonical >= 0) return canonical;
            }
        }
        return selIcon;
    }
}
