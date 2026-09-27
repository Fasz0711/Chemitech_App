using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// La fórmula estructural PLANA, dibujada dentro de un rectángulo de interfaz.
///
/// Es la mitad del ítem 2 del plan de validación: la notación del libro al lado de la
/// forma espacial, para que el alumno las relacione. Se dibuja con coordenadas y no con
/// una imagen a propósito —fue la recomendación que el backend aceptó— porque las
/// coordenadas permiten SEÑALAR: Highlight(i) enciende el mismo átomo aquí y en el 3D,
/// y ese gesto ES la comparación de representaciones. Con un PNG sería un dibujo al lado.
///
/// LA CORRESPONDENCIA ES POR ÍNDICE: el punto i de esta tarjeta es el átomo i del modelo
/// 3D, hidrógenos incluidos. El servidor lo garantiza calculando las dos vistas del mismo
/// objeto de RDKit.
/// </summary>
public class Structure2DCard : MonoBehaviour
{
    [SerializeField] private RectTransform  canvasArea;   // dónde se dibuja
    [SerializeField] private TMP_FontAsset  labelFont;

    [Header("Ajustes")]
    [SerializeField] private float atomSize     = 44f;
    [SerializeField] private float bondWidth    = 6f;
    [SerializeField] private float bondGap      = 7f;    // separación de dobles y triples
    [SerializeField] private float margin       = 0.12f; // del lado corto, para que respire

    static readonly Color BOND_COLOR = new Color(0.80f, 0.84f, 0.95f);

    readonly List<Image> atomDots = new List<Image>();
    readonly List<Color> baseColors = new List<Color>();

    /// <summary>Dibuja la tarjeta y dice si pudo. Sin coordenadas no hay nada que dibujar:
    /// le pasa a la sal, cuya red de 27 iones no tiene fórmula plana, y a lo que el
    /// servidor no reconoce.
    ///
    /// NO TOCA SU PROPIO GameObject. Este componente vive en la tarjeta, así que apagarse
    /// a sí mismo apagaba el marco entero y dejaba los interruptores de al lado flotando
    /// sin nada que los alineara. Quién se ve y quién no lo decide la pantalla; esto solo
    /// dibuja o informa de que no puede.</summary>
    public bool Show(IList<Vector2> flat2D, IList<ExplanationContext.Atom> atoms,
                     IList<ExplanationContext.Bond> bonds)
    {
        Clear();

        if (!canvasArea || flat2D == null || atoms == null ||
            flat2D.Count == 0 || flat2D.Count != atoms.Count)
            return false;

        // Las coordenadas llegan normalizadas pero no necesariamente llenando el cuadro,
        // así que se reencuadran: se busca su caja y se estira hasta el área disponible
        // CONSERVANDO LA PROPORCIÓN, o una molécula alargada saldría deformada.
        Vector2 min = flat2D[0], max = flat2D[0];
        foreach (var p in flat2D)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        Vector2 size = canvasArea.rect.size;
        float pad = Mathf.Min(size.x, size.y) * margin;
        Vector2 usable = new Vector2(Mathf.Max(1f, size.x - pad * 2f),
                                     Mathf.Max(1f, size.y - pad * 2f));

        Vector2 span = max - min;
        // Una molécula plana perfectamente vertical u horizontal tiene un lado en cero:
        // sin este mínimo, la escala saldría infinita.
        float scale = Mathf.Min(span.x > 0.001f ? usable.x / span.x : float.MaxValue,
                                span.y > 0.001f ? usable.y / span.y : float.MaxValue);
        if (float.IsInfinity(scale) || scale <= 0f) scale = 1f;

        Vector2 center = (min + max) * 0.5f;
        var pts = new Vector2[flat2D.Count];
        for (int i = 0; i < flat2D.Count; i++) pts[i] = (flat2D[i] - center) * scale;

        // Los enlaces primero: así los círculos de los átomos quedan por encima y tapan
        // el nacimiento de la línea, que es como se dibuja en un libro.
        if (bonds != null)
            foreach (var b in bonds)
            {
                if (b.beginAtomId < 0 || b.beginAtomId >= pts.Length) continue;
                if (b.endAtomId   < 0 || b.endAtomId   >= pts.Length) continue;
                DrawBond(pts[b.beginAtomId], pts[b.endAtomId], Mathf.Clamp(b.order, 1, 3));
            }

        for (int i = 0; i < pts.Length; i++) DrawAtom(pts[i], atoms[i].element);

        return true;
    }

    void DrawBond(Vector2 a, Vector2 b, int order)
    {
        Vector2 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.01f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector2 perp = new Vector2(-dir.y, dir.x).normalized;

        for (int i = 0; i < order; i++)
        {
            float off = (order == 1) ? 0f : (i - (order - 1) * 0.5f) * bondGap;
            var go = new GameObject("Bond", typeof(RectTransform));
            go.transform.SetParent(canvasArea, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (a + b) * 0.5f + perp * off;
            rt.sizeDelta = new Vector2(len, bondWidth);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);

            var img = go.AddComponent<Image>();
            img.color = BOND_COLOR;
            img.raycastTarget = false;
        }
    }

    void DrawAtom(Vector2 pos, string element)
    {
        int idx = AtomCatalog.IndexOf(element);
        Color col = idx >= 0 ? AtomCatalog.All[idx].color : new Color(0.55f, 0.58f, 0.65f);

        var go = new GameObject("Atom_" + element, typeof(RectTransform));
        go.transform.SetParent(canvasArea, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = Vector2.one * atomSize;

        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;

        atomDots.Add(img);
        baseColors.Add(col);

        var lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(go.transform, false);
        var lrt = lblGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

        var tmp = lblGo.AddComponent<TextMeshProUGUI>();
        tmp.text = element;
        tmp.font = labelFont;
        tmp.fontSize = atomSize * 0.52f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        // Sobre un átomo claro, texto oscuro: el blanco sobre el hidrógeno no se lee.
        float lum = 0.299f * col.r + 0.587f * col.g + 0.114f * col.b;
        tmp.color = lum > 0.65f ? new Color(0.10f, 0.10f, 0.18f) : Color.white;
    }

    /// <summary>Enciende un átomo de la tarjeta. Pasar -1 los apaga todos.
    ///
    /// Es la mitad de "señalar el mismo átomo en las dos vistas"; la otra mitad la pone
    /// quien llame a esto con el mismo índice en el modelo 3D.</summary>
    public void Highlight(int atomIndex)
    {
        for (int i = 0; i < atomDots.Count; i++)
        {
            if (!atomDots[i]) continue;
            atomDots[i].color = (i == atomIndex) ? Color.white : baseColors[i];
        }
    }

    public void Clear()
    {
        if (!canvasArea) return;
        for (int i = canvasArea.childCount - 1; i >= 0; i--)
            Destroy(canvasArea.GetChild(i).gameObject);
        atomDots.Clear();
        baseColors.Clear();
    }
}
