using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tutorial informativo de la zona de juego: un modal de bienvenida + N pasos que
/// explican cómo jugar (texto + una pista por paso). Se reproduce automáticamente
/// la PRIMERA vez que se entra a un universo en este dispositivo (flag global en
/// PlayerPrefs, sirve para invitado y logeado) y se puede repetir desde el menú de
/// pausa (ZonaJuegoManager llama a Replay()).
///
/// Es solo informativo: no obliga a hacer la acción; se avanza con Atrás/Siguiente.
/// Todos los objetos visuales los crea (y cablea) TutorialTool de forma aditiva.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        public string title;
        [TextArea(2, 4)] public string body;
        public string hint;   // línea de ejemplo/ilustración (vacío = se oculta)
    }

    [Header("Fondo / overlay")]
    [SerializeField] private GameObject backdrop;

    [Header("Modal de bienvenida")]
    [SerializeField] private GameObject welcomeModal;
    [SerializeField] private Button btnOmitir;
    [SerializeField] private Button btnIniciar;

    [Header("Modal de paso")]
    [SerializeField] private GameObject      stepModal;
    [SerializeField] private TextMeshProUGUI stepTitle;
    [SerializeField] private TextMeshProUGUI stepBody;
    [SerializeField] private GameObject      hintBox;
    [SerializeField] private TextMeshProUGUI stepHint;
    [SerializeField] private Button          btnAtras;
    [SerializeField] private Button          btnSiguiente;
    [SerializeField] private TextMeshProUGUI btnSiguienteLabel;
    [SerializeField] private Button          btnClose;

    [Header("Progreso")]
    [SerializeField] private RectTransform dotsContainer;
    [SerializeField] private Sprite        dotSprite;

    [Header("Contenido")]
    [SerializeField] private Step[] steps;

    const string SEEN_KEY = "chemitech_tutorial_seen";

    static readonly Color DOT_DONE    = new Color(0.25f, 0.88f, 0.82f, 1f);  // cian (pasos vistos)
    static readonly Color DOT_ACTIVE  = new Color(0.95f, 0.77f, 0.13f, 1f);  // ámbar (paso actual)
    static readonly Color DOT_PENDING = new Color(1f, 1f, 1f, 0.18f);        // tenue (por venir)

    readonly List<Image> _dots = new List<Image>();
    int  _current;
    bool _open;

    /// <summary>El tutorial está visible (ZonaJuegoManager pausa el cronómetro).</summary>
    public bool IsOpen => _open;

    void Start()
    {
        if (btnOmitir)    btnOmitir.onClick.AddListener(Close);
        if (btnIniciar)   btnIniciar.onClick.AddListener(StartSteps);
        if (btnAtras)     btnAtras.onClick.AddListener(Prev);
        if (btnSiguiente) btnSiguiente.onClick.AddListener(Next);
        if (btnClose)     btnClose.onClick.AddListener(Close);

        BuildProgress();
        HideAll();

        // Primera vez en este dispositivo → mostrar y marcar como visto.
        if (PlayerPrefs.GetInt(SEEN_KEY, 0) == 0)
        {
            PlayerPrefs.SetInt(SEEN_KEY, 1);
            PlayerPrefs.Save();
            OpenWelcome();
        }
    }

    /// <summary>Reabre el tutorial desde el inicio (botón "Ver tutorial" de la pausa).</summary>
    public void Replay() => OpenWelcome();

    // ── Estados ────────────────────────────────────────────────────────────────
    void OpenWelcome()
    {
        _open = true;
        if (backdrop)     backdrop.SetActive(true);
        if (welcomeModal) welcomeModal.SetActive(true);
        if (stepModal)    stepModal.SetActive(false);
    }

    void StartSteps()
    {
        if (welcomeModal) welcomeModal.SetActive(false);
        _current = 0;
        ShowStep();
    }

    void ShowStep()
    {
        if (steps == null || steps.Length == 0) { Close(); return; }
        _current = Mathf.Clamp(_current, 0, steps.Length - 1);
        var s = steps[_current];

        _open = true;
        if (backdrop)  backdrop.SetActive(true);
        if (stepModal) stepModal.SetActive(true);

        if (stepTitle) stepTitle.text = $"{_current + 1} - {s.title}";
        if (stepBody)  stepBody.text  = s.body;

        bool hasHint = !string.IsNullOrEmpty(s.hint);
        if (hintBox)  hintBox.SetActive(hasHint);
        if (stepHint) stepHint.text = s.hint;

        bool last = _current == steps.Length - 1;
        if (btnSiguienteLabel) btnSiguienteLabel.text = last ? "¡Listo!" : "Siguiente";

        UpdateProgress();
    }

    void Next()
    {
        if (_current >= steps.Length - 1) Close();
        else { _current++; ShowStep(); }
    }

    void Prev()
    {
        if (_current <= 0) OpenWelcome();   // desde el paso 1, "Atrás" vuelve a la bienvenida
        else { _current--; ShowStep(); }
    }

    void Close() => HideAll();

    void HideAll()
    {
        _open = false;
        if (backdrop)     backdrop.SetActive(false);
        if (welcomeModal) welcomeModal.SetActive(false);
        if (stepModal)    stepModal.SetActive(false);
    }

    // ── Barra de progreso (se genera según la cantidad de pasos) ───────────────
    void BuildProgress()
    {
        _dots.Clear();
        if (!dotsContainer || steps == null) return;

        for (int i = dotsContainer.childCount - 1; i >= 0; i--)
            Destroy(dotsContainer.GetChild(i).gameObject);

        const float W = 30f, H = 8f, GAP = 10f;
        float total  = steps.Length * W + (steps.Length - 1) * GAP;
        float startX = -total / 2f + W / 2f;

        for (int i = 0; i < steps.Length; i++)
        {
            var go = new GameObject($"Dot{i}", typeof(RectTransform));
            go.transform.SetParent(dotsContainer, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(startX + i * (W + GAP), 0f);
            rt.sizeDelta = new Vector2(W, H);

            var img = go.AddComponent<Image>();
            img.sprite = dotSprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            img.color = DOT_PENDING;
            _dots.Add(img);
        }
    }

    void UpdateProgress()
    {
        for (int i = 0; i < _dots.Count; i++)
            _dots[i].color = i == _current ? DOT_ACTIVE
                           : i <  _current ? DOT_DONE
                           : DOT_PENDING;
    }
}
