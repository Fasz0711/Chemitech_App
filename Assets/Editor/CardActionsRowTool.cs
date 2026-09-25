using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta ADITIVA: junta el lado derecho de la plantilla de tarjeta de MisClasesScene
/// en una sola fila que se alinea sola.
///
/// QUÉ ARREGLA. StatusBadge y los botones tenían cada uno su posición absoluta, puesta a
/// mano para UNA combinación. Pero la tarjeta tiene cuatro: alumno (Entrar), docente sin
/// iniciar (Conducir + Iniciar), docente en curso (Conducir + Terminar) y terminada
/// (ninguno). En las que no se ajustaron quedaban huecos, y el badge acababa a 150 px del
/// borde mientras los botones estaban a 20, así que ni siquiera formaban una columna.
///
/// POR QUÉ UN LAYOUT GROUP. Un HorizontalLayoutGroup ignora a los hijos inactivos, que es
/// exactamente lo que el manager hace con los botones según rol y estado. Así las cuatro
/// combinaciones se recolocan solas y nadie vuelve a calcular una X a mano.
///
/// El manager NO se toca: busca los botones por nombre con FindDeep, que es recursivo, así
/// que reparentarlos le da igual.
///
/// IDEMPOTENTE: si la fila ya existe se reutiliza, y se respetan su posición y su
/// separación. De aquí en adelante los ajustes a mano se hacen sobre la fila, no botón por
/// botón: mover la fila mueve el conjunto y el reparto interno se recalcula solo.
///
/// Menú: ChemiTech → Add → Card Actions Row
/// </summary>
public static class CardActionsRowTool
{
    const string ROW_NAME = "ActionsRow";

    // De izquierda a derecha. Los tres últimos son excluyentes entre sí (el manager
    // enciende uno como mucho), así que se turnan el extremo derecho.
    static readonly string[] ORDER     = { "StatusBadge", "BtnConducir", "BtnEnter", "BtnStart", "BtnStop" };
    static readonly string[] EXCLUSIVE = { "BtnEnter", "BtnStart", "BtnStop" };

    [MenuItem("ChemiTech/Add/Card Actions Row")]
    public static void Add()
    {
        var scene  = EditorSceneManager.GetActiveScene();
        var canvas = FindRoot(scene, "Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Card Actions Row",
                "Abre primero MisClasesScene (no encontré el Canvas).", "OK");
            return;
        }

        var card = FindDeep(canvas.transform, "CardTemplate");
        if (card == null)
        {
            EditorUtility.DisplayDialog("Card Actions Row",
                "No encontré CardTemplate. ¿Es esta MisClasesScene?", "OK");
            return;
        }

        // Todo se mide ANTES de tocar nada: en cuanto un hijo entra en el layout group, su
        // posición y su tamaño pasan a estar calculados y ya no dicen lo que puso el autor.
        var found = new Dictionary<string, RectTransform>();
        var size  = new Dictionary<string, Vector2>();
        foreach (var n in ORDER)
        {
            var t = FindDeep(card, n) as RectTransform;
            if (t == null) continue;
            found[n] = t;
            size[n]  = PreferredSize(t);
        }

        if (found.Count == 0)
        {
            EditorUtility.DisplayDialog("Card Actions Row",
                "La tarjeta no tiene ni el badge ni los botones; no hay nada que ordenar.", "OK");
            return;
        }

        var existing = FindDeep(card, ROW_NAME) as RectTransform;
        RectTransform row;
        float spacing;

        if (existing != null)
        {
            // Segunda pasada: manda la fila, incluidos los ajustes que se le hayan hecho.
            row = existing;
            var old = row.GetComponent<HorizontalLayoutGroup>();
            spacing = old ? old.spacing : DeduceSpacing(found);
        }
        else
        {
            spacing = DeduceSpacing(found);

            var go = new GameObject(ROW_NAME, typeof(RectTransform));
            go.transform.SetParent(card, false);
            row = go.GetComponent<RectTransform>();

            // Anclada al centro del borde derecho: así la fila entera queda centrada en
            // vertical, y el margen derecho es el que ya tenían los botones.
            row.anchorMin = new Vector2(1f, 0.5f);
            row.anchorMax = new Vector2(1f, 0.5f);
            row.pivot     = new Vector2(1f, 0.5f);
            row.anchoredPosition = new Vector2(-DeduceRightMargin(found), 0f);
            row.sizeDelta        = new Vector2(RowWidth(size, spacing), RowHeight(size));
        }

        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        if (!hlg) hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = spacing;
        hlg.childAlignment         = TextAnchor.MiddleRight;   // el contenido se pega al borde
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;                    // cada uno con su tamaño
        hlg.childForceExpandHeight = false;
        hlg.padding                = new RectOffset(0, 0, 0, 0);

        int idx = 0;
        foreach (var n in ORDER)
        {
            RectTransform rt;
            if (!found.TryGetValue(n, out rt)) continue;

            rt.SetParent(row, false);

            // El layout group reparte por el preferred, no por el sizeDelta: sin esto los
            // botones saldrían del tamaño mínimo que calcule su texto.
            var le = rt.GetComponent<LayoutElement>();
            if (!le) le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth  = size[n].x;
            le.preferredHeight = size[n].y;
            le.flexibleWidth   = 0f;
            le.flexibleHeight  = 0f;

            rt.SetSiblingIndex(idx++);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = row.gameObject;

        Debug.Log("[CardActionsRowTool] ✓ " + idx + " elementos en la fila. Separación " +
                  spacing.ToString("0.#") + ", margen derecho " +
                  (-row.anchoredPosition.x).ToString("0.#") + ".");
        EditorUtility.DisplayDialog("¡Listo!",
            "El badge y los botones ahora son una fila que se realinea sola.\n\n" +
            "CardTemplate está desactivada, así que en la vista de escena no se recoloca " +
            "hasta que la actives: márcala un momento para verla y vuelve a apagarla.\n\n" +
            "Para moverlo todo, mueve ActionsRow (ya está seleccionada), no los botones.", "OK");
    }

    // ── Medidas tomadas de la tarjeta, no inventadas ──────────────────────────

    /// <summary>El tamaño bueno es el del LayoutElement si ya lo hay (segunda pasada), y el
    /// sizeDelta solo la primera vez, que es cuando todavía significa algo.</summary>
    static Vector2 PreferredSize(RectTransform rt)
    {
        var le = rt.GetComponent<LayoutElement>();
        if (le && le.preferredWidth > 0f && le.preferredHeight > 0f)
            return new Vector2(le.preferredWidth, le.preferredHeight);
        return rt.sizeDelta;
    }

    /// <summary>La separación sale del hueco REAL entre Conducir y el botón del extremo: si
    /// la tarjeta se ajustó a mano, la fila hereda ese ritmo en vez de imponer otro.</summary>
    static float DeduceSpacing(Dictionary<string, RectTransform> found)
    {
        RectTransform conducir;
        if (!found.TryGetValue("BtnConducir", out conducir)) return 20f;

        var action = Rightmost(found);
        if (action == null) return 20f;

        float gap = LeftEdge(action) - RightEdge(conducir);
        return gap > 0f ? gap : 20f;
    }

    static float DeduceRightMargin(Dictionary<string, RectTransform> found)
    {
        var action = Rightmost(found);
        if (action == null) return 20f;
        return Mathf.Max(0f, -RightEdge(action));
    }

    static RectTransform Rightmost(Dictionary<string, RectTransform> found)
    {
        RectTransform best = null;
        float bestX = float.NegativeInfinity;
        foreach (var n in EXCLUSIVE)
        {
            RectTransform rt;
            if (!found.TryGetValue(n, out rt)) continue;
            float r = RightEdge(rt);
            if (r > bestX) { bestX = r; best = rt; }
        }
        return best;
    }

    /// <summary>Ancho de la fila = el PEOR caso, no la suma de todo: Entrar, Iniciar y
    /// Terminar se turnan el mismo sitio, así que solo cuenta el más ancho de los tres.</summary>
    static float RowWidth(Dictionary<string, Vector2> size, float spacing)
    {
        float total  = 0f;
        int   pieces = 0;

        foreach (var n in new[] { "StatusBadge", "BtnConducir" })
            if (size.ContainsKey(n)) { total += size[n].x; pieces++; }

        float widest = 0f;
        foreach (var n in EXCLUSIVE)
            if (size.ContainsKey(n)) widest = Mathf.Max(widest, size[n].x);
        if (widest > 0f) { total += widest; pieces++; }

        if (pieces > 1) total += spacing * (pieces - 1);
        return total;
    }

    static float RowHeight(Dictionary<string, Vector2> size)
    {
        float h = 0f;
        foreach (var kv in size) h = Mathf.Max(h, kv.Value.y);
        return h;
    }

    // Bordes relativos al ancla derecha de la tarjeta; válidos solo antes de reparentar.
    static float RightEdge(RectTransform rt) => rt.anchoredPosition.x + rt.sizeDelta.x * (1f - rt.pivot.x);
    static float LeftEdge (RectTransform rt) => rt.anchoredPosition.x - rt.sizeDelta.x * rt.pivot.x;

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform c in parent)
        {
            if (c.name == name) return c;
            var r = FindDeep(c, name);
            if (r) return r;
        }
        return null;
    }
}
