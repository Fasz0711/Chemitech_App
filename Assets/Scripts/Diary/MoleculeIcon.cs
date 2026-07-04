using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dibuja un mini-diagrama 2D de una molécula (átomos coloreados con su símbolo +
/// enlaces) dentro de un RectTransform, a partir de la estructura del diario.
/// Reutilizable: diario, y una futura pantalla de detalle.
/// Los colores por elemento salen de AtomCatalog; el círculo y la fuente se pasan
/// por parámetro (no se pueden cargar por AssetDatabase en runtime).
/// </summary>
public static class MoleculeIcon
{
    static readonly Color BondColor    = new Color(0.80f, 0.86f, 0.95f, 0.90f);
    static readonly Color DefaultAtom  = new Color(0.55f, 0.58f, 0.65f, 1f);
    static readonly Color DarkText     = new Color(0.10f, 0.10f, 0.18f, 1f);

    public static void Render(RectTransform area, JournalStructure s, Sprite circle, TMP_FontAsset font,
                              float atomSize = 46f, float bondThickness = 6f, float padding = 6f)
    {
        Clear(area);
        if (area == null || s == null || s.atoms == null || s.atoms.Length == 0) return;

        int n = s.atoms.Length;
        var px = new float[n];
        var py = new float[n];
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;

        for (int i = 0; i < n; i++)
        {
            var p = s.atoms[i].position;
            float x = p != null ? p.x : 0f;
            float y = p != null ? p.y : 0f;
            px[i] = x; py[i] = y;
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
        }

        float cx = (minX + maxX) * 0.5f;
        float cy = (minY + maxY) * 0.5f;
        float spanX = Mathf.Max(maxX - minX, 1e-4f);
        float spanY = Mathf.Max(maxY - minY, 1e-4f);

        Vector2 size = area.rect.size;
        float usableW = Mathf.Max(size.x - 2f * padding - atomSize, 1f);
        float usableH = Mathf.Max(size.y - 2f * padding - atomSize, 1f);
        float scale = (n == 1) ? 0f : Mathf.Min(usableW / spanX, usableH / spanY);

        var pos = new Vector2[n];
        for (int i = 0; i < n; i++)
            pos[i] = new Vector2((px[i] - cx) * scale, (py[i] - cy) * scale);

        // Enlaces primero (quedan debajo de los átomos)
        if (s.bonds != null)
        {
            foreach (var b in s.bonds)
            {
                if (b.beginAtomId < 0 || b.beginAtomId >= n || b.endAtomId < 0 || b.endAtomId >= n) continue;
                DrawBond(area, pos[b.beginAtomId], pos[b.endAtomId], Mathf.Max(1, b.order), bondThickness);
            }
        }

        // Átomos encima
        for (int i = 0; i < n; i++)
            DrawAtom(area, s.atoms[i].type, pos[i], atomSize, circle, font);
    }

    public static void Clear(RectTransform area)
    {
        if (area == null) return;
        for (int i = area.childCount - 1; i >= 0; i--)
            Object.Destroy(area.GetChild(i).gameObject);
    }

    // ── Dibujo ──────────────────────────────────────────────────────────────
    static void DrawBond(RectTransform parent, Vector2 a, Vector2 b, int order, float thickness)
    {
        Vector2 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.001f) return;

        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector2 mid = (a + b) * 0.5f;
        Vector2 perp = new Vector2(-dir.y, dir.x).normalized;
        float gap = thickness + 3f;
        float t = order >= 2 ? thickness * 0.7f : thickness;

        for (int i = 0; i < order; i++)
        {
            float off = (i - (order - 1) * 0.5f) * gap;
            var img = NewImage(parent, "Bond", BondColor, null);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(len, t);
            rt.anchoredPosition = mid + perp * off;
            rt.localRotation = Quaternion.Euler(0f, 0f, ang);
        }
    }

    static void DrawAtom(RectTransform parent, string type, Vector2 p, float sz, Sprite circle, TMP_FontAsset font)
    {
        int idx = AtomCatalog.IndexOf(type);
        Color col = idx >= 0 ? AtomCatalog.All[idx].color : DefaultAtom;

        var img = NewImage(parent, "Atom_" + type, col, circle);
        img.rectTransform.sizeDelta = new Vector2(sz, sz);
        img.rectTransform.anchoredPosition = p;
        img.preserveAspect = true;

        // Símbolo con contraste según luminancia del color del átomo
        float lum = 0.299f * col.r + 0.587f * col.g + 0.114f * col.b;
        Color txt = lum > 0.65f ? DarkText : Color.white;

        var go = new GameObject("Sym", typeof(RectTransform));
        go.transform.SetParent(img.transform, false);
        var trt = go.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = type;
        tmp.font = font;
        tmp.color = txt;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        tmp.fontSize = (!string.IsNullOrEmpty(type) && type.Length > 1) ? sz * 0.36f : sz * 0.46f;
    }

    static Image NewImage(RectTransform parent, string name, Color c, Sprite spr)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = c;
        if (spr != null) img.sprite = spr;
        img.raycastTarget = false;
        return img;
    }
}
