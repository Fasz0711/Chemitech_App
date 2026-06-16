using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;

public class CrearUniversoManager : MonoBehaviour
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
    [SerializeField] private Button[]  iconButtons;  // 8
    [SerializeField] private Image[]   iconBorders;  // 8
    [SerializeField] private Sprite[]  iconSprites;  // 8

    [Header("Colores (6)")]
    [SerializeField] private Button[]          colorButtons;
    [SerializeField] private TextMeshProUGUI[] colorChecks;

    [Header("Botones")]
    [SerializeField] private Button btnAtras;
    [SerializeField] private Button btnCancelar;
    [SerializeField] private Button btnCrear;

    [Header("Escenas")]
    [SerializeField] private string escenaAtras = "MisUniversosScene";

    static readonly Color COL_ICON_SEL   = new Color(0.30f, 0.85f, 0.91f, 1f);
    static readonly Color COL_ICON_UNSEL = new Color(0.12f, 0.14f, 0.28f, 0.85f);

    static readonly Color[] THEME_COLORS = new Color[]
    {
        new Color(0.30f, 0.85f, 0.91f, 1f), // teal   #4DD9E8
        new Color(0.90f, 0.46f, 0.71f, 1f), // pink   #E575B5
        new Color(0.61f, 0.35f, 0.71f, 1f), // purple #9B59B6
        new Color(0.95f, 0.77f, 0.06f, 1f), // yellow #F1C40F
        new Color(0.18f, 0.80f, 0.44f, 1f), // green  #2ECC71
        new Color(0.91f, 0.30f, 0.24f, 1f), // red    #E74C3C
    };

    int selIcon  = 1;
    int selColor = 0;

    void Start()
    {
        if (inputName)
        {
            inputName.characterLimit = MAX_CHARS;
            inputName.onValueChanged.AddListener(OnNameChanged);
        }

        if (btnCancelar) btnCancelar.onClick.AddListener(() => SceneManager.LoadScene(escenaAtras));
        if (btnAtras)    btnAtras.onClick.AddListener(() => SceneManager.LoadScene(escenaAtras));
        if (btnCrear)    btnCrear.onClick.AddListener(OnCrear);

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

        SelectIcon(selIcon);
        SelectColor(selColor);
        OnNameChanged(inputName ? inputName.text : "");
    }

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
            // Lee el sprite del hijo "IconImg" del botón, sin necesitar iconSprites[] cableado
            var child = iconButtons[idx].transform.GetChild(0).GetComponent<Image>();
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
        if (previewCircle) previewCircle.color = THEME_COLORS[selColor];
        string name = inputName ? inputName.text.Trim() : "";
        if (previewNameLabel) previewNameLabel.text = string.IsNullOrEmpty(name) ? "Universo 1" : name;
    }

    void OnCrear()
    {
        string name = inputName ? inputName.text.Trim() : "";
        bool hasInvalid = Regex.IsMatch(name, "[<>:\"|?]");
        if (string.IsNullOrEmpty(name) || hasInvalid)
        {
            if (validationLabel) validationLabel.gameObject.SetActive(true);
            return;
        }
        Debug.Log($"[CrearUniverso] nombre={name} icono={selIcon} color={selColor} — pendiente.");
    }
}
