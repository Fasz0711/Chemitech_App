using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dibuja la escena que manda el docente: esferas por átomo y cilindros por enlace.
///
/// NO detecta nada. La clase recibe la química ya resuelta por el servidor y solo la
/// pinta, que es justo la razón por la que el dibujado de enlaces se extrajo de
/// BondManager a BondRenderer.
///
/// No es MonoBehaviour: lo instancia quien lo usa (el alumno y el docente ven la misma
/// escena) y llama a UpdateVisuals() cada frame. Al terminar, Dispose().
/// </summary>
public class ClassSceneRenderer
{
    readonly Transform root;
    readonly Material  atomMaterial;
    readonly float     worldScale;
    readonly float     atomSize;

    readonly Dictionary<int, Atom3D> byId = new Dictionary<int, Atom3D>();
    readonly List<GameObject>        spawned = new List<GameObject>();
    readonly List<(int a, int b, int order)> bonds = new List<(int, int, int)>();

    readonly BondRenderer bondRenderer;

    static readonly Color FALLBACK_COLOR = new Color(0.72f, 0.75f, 0.82f);

    public ClassSceneRenderer(Transform root, Material atomMaterial, Material bondMaterial,
                              float worldScale, float atomSize, float bondThickness, float bondSpacing)
    {
        this.root         = root;
        this.atomMaterial = atomMaterial;
        this.worldScale   = worldScale;
        this.atomSize     = atomSize;

        bondRenderer = new BondRenderer(root, bondMaterial, byId, bondThickness, bondSpacing);
    }

    /// <summary>Reconstruye la escena entera. Se llama solo cuando el estado CAMBIÓ:
    /// el servidor manda estado completo, así que redibujar es siempre correcto.</summary>
    public void Render(SceneMoleculeDTO[] molecules)
    {
        Clear();
        if (molecules == null) return;

        // Ids globales: los del JSON son locales a cada molécula (0..n-1), y con varias
        // moléculas en escena se pisarían entre ellas.
        int baseId = 0;

        foreach (var m in molecules)
        {
            if (m == null || m.atoms == null) continue;

            Vector3 offset = m.offset != null ? m.offset.ToVector3() : Vector3.zero;

            // 'scale' es el tamaño REAL en ångströms. Conserva la proporción entre
            // moléculas: sin él, un cristal de sal se vería igual de grande que una
            // molécula de agua, porque cada una llega normalizada a [0,1] por separado.
            // El 0 no debería llegar nunca; si llegara, la molécula colapsaría a un punto.
            float mScale = m.scale > 0f ? m.scale : 1f;

            for (int i = 0; i < m.atoms.Length; i++)
            {
                var a = m.atoms[i];
                if (a == null) continue;

                // Fórmula del contrato: la posición normalizada se centra restando 0.5
                // ANTES de escalar, o la molécula quedaría colgada de una esquina del
                // offset en vez de centrada en él.
                Vector3 local = a.position != null ? a.position.ToVector3() : Vector3.zero;
                Vector3 angstroms = (local - Vector3.one * 0.5f) * mScale + offset;

                SpawnAtom(baseId + i, a.type, angstroms * worldScale);
            }

            if (m.bonds != null)
                foreach (var b in m.bonds)
                {
                    if (b == null) continue;
                    if (b.beginAtomId < 0 || b.beginAtomId >= m.atoms.Length) continue;
                    if (b.endAtomId   < 0 || b.endAtomId   >= m.atoms.Length) continue;
                    bonds.Add((baseId + b.beginAtomId, baseId + b.endAtomId, b.order));
                }

            baseId += m.atoms.Length;
        }

        bondRenderer.SetBonds(bonds);
    }

    /// <summary>Orienta los enlaces según la cámara. Llamar cada frame.</summary>
    public void UpdateVisuals() => bondRenderer.UpdateVisuals();

    public void Clear()
    {
        bondRenderer.Clear();
        bonds.Clear();
        byId.Clear();

        foreach (var go in spawned) if (go) Object.Destroy(go);
        spawned.Clear();
    }

    public void Dispose()
    {
        Clear();
        bondRenderer.Dispose();
    }

    void SpawnAtom(int id, string element, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = $"Atom_{element}_{id}";
        var col = go.GetComponent<Collider>(); if (col) Object.Destroy(col);
        go.transform.SetParent(root, false);
        go.transform.localPosition = position;
        go.transform.localScale    = Vector3.one * atomSize;

        int index = AtomCatalog.IndexOf(element);
        Color color = index >= 0 ? AtomCatalog.All[index].color : FALLBACK_COLOR;

        var atom = go.AddComponent<Atom3D>();
        atom.Init(index, element, id, atomMaterial, color, go.GetComponent<Renderer>());

        byId[id] = atom;
        spawned.Add(go);
    }
}
