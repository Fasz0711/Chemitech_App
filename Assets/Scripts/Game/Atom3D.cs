using UnityEngine;

/// <summary>Átomo colocado en la zona 3D. Esfera coloreada con resaltado de selección.</summary>
public class Atom3D : MonoBehaviour
{
    public int    atomIndex;   // índice en AtomCatalog.All
    public string element;
    public int    id;

    Renderer rend;
    Material mat;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId  = Shader.PropertyToID("_EmissionColor");
    static readonly Color SEL_EMISSION = new Color(0.20f, 0.85f, 1f) * 1.6f;

    public void Init(int index, string elem, int id, Material baseMat, Color color, Renderer renderer)
    {
        atomIndex = index; element = elem; this.id = id; rend = renderer;
        mat = new Material(baseMat);
        mat.SetColor(BaseColorId, color);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor(EmissionId, Color.black);
        if (rend) rend.sharedMaterial = mat;
        SetSelected(false);
    }

    public void SetSelected(bool sel)
    {
        if (mat) mat.SetColor(EmissionId, sel ? SEL_EMISSION : Color.black);
    }
}
