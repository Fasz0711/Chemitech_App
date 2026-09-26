using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dibuja la escena que manda el docente: esferas por átomo y cilindros por enlace.
///
/// NO detecta nada. La clase recibe la química ya resuelta por el servidor y solo la
/// pinta, que es justo la razón por la que el dibujado de enlaces se extrajo de
/// BondManager a BondRenderer.
///
/// Tiene dos modos:
///   • SOLO LECTURA (alumno): crea sus propias esferas y dibuja los enlaces él mismo.
///   • EDITABLE (pizarra del docente): delega la creación de átomos en un IAtomSink —
///     su AtomPlacementController— para que lo que llega del servidor sea el MISMO
///     objeto manipulable que lo que el docente coloca a mano. En ese modo los enlaces
///     los dibuja BondManager, no este renderer, para que no haya dos dueños del mismo
///     cilindro.
///
/// No es MonoBehaviour: lo instancia quien lo usa y llama a UpdateVisuals() cada frame.
/// Al terminar, Dispose().
/// </summary>
public class ClassSceneRenderer
{
    /// <summary>Quien crea los átomos cuando la escena no es de solo lectura.</summary>
    public interface IAtomSink
    {
        void   ClearAtoms();
        Atom3D SpawnFromScene(string element, Vector3 worldPos);
    }

    readonly Transform root;
    readonly Material  atomMaterial;
    readonly float     worldScale;
    readonly float     atomSize;
    readonly IAtomSink sink;   // null = solo lectura (alumno)

    readonly Dictionary<int, Atom3D> byId = new Dictionary<int, Atom3D>();
    readonly List<GameObject>        spawned = new List<GameObject>();
    readonly List<(int a, int b, int order)> bonds = new List<(int, int, int)>();

    // "m1" -> los ids globales de sus átomos, EN EL ORDEN LOCAL del contrato. Los
    // highlights y los enlaces vienen con índices locales a su molécula, así que sin
    // este mapa no se sabe a qué esfera se refieren.
    //
    // Es una tabla y no un desplazamiento base porque con el sink los ids los asigna el
    // controlador de colocación y no tienen por qué ser consecutivos.
    readonly Dictionary<string, int[]> moleculeAtomIds = new Dictionary<string, int[]>();

    // Al revés: id global de átomo -> molécula a la que pertenece. Lo usa el aislamiento,
    // que parte de tocar un átomo y necesita saber qué molécula dejar encendida.
    readonly Dictionary<int, string> atomMolecule = new Dictionary<int, string>();

    string isolated = "";

    readonly BondRenderer bondRenderer;   // null en modo editable: los pinta BondManager

    static readonly Color FALLBACK_COLOR = new Color(0.72f, 0.75f, 0.82f);

    public ClassSceneRenderer(Transform root, Material atomMaterial, Material bondMaterial,
                              float worldScale, float atomSize, float bondThickness, float bondSpacing,
                              IAtomSink sink = null)
    {
        this.root         = root;
        this.atomMaterial = atomMaterial;
        this.worldScale   = worldScale;
        this.atomSize     = atomSize;
        this.sink         = sink;

        if (sink == null)
            bondRenderer = new BondRenderer(root, bondMaterial, byId, bondThickness, bondSpacing);
    }

    /// <summary>Los enlaces del último Render, ya en ids de Atom3D. En modo editable es
    /// lo que se le pasa a BondManager para que los dibuje sin volver a preguntarle al
    /// servidor algo que el servidor acaba de decir.</summary>
    public List<(int a, int b, int order)> Bonds => bonds;

    /// <summary>Reconstruye la escena entera. Se llama solo cuando el estado CAMBIÓ:
    /// el servidor manda estado completo, así que redibujar es siempre correcto.</summary>
    public void Render(SceneMoleculeDTO[] molecules)
    {
        Clear();
        if (molecules == null) return;

        // Ids globales: los del JSON son locales a cada molécula (0..n-1), y con varias
        // moléculas en escena se pisarían entre ellas.
        int nextInternalId = 0;

        foreach (var m in molecules)
        {
            if (m == null || m.atoms == null) continue;

            Vector3 offset = m.offset != null ? m.offset.ToVector3() : Vector3.zero;

            // 'scale' es el tamaño REAL en ångströms. Conserva la proporción entre
            // moléculas: sin él, un cristal de sal se vería igual de grande que una
            // molécula de agua, porque cada una llega normalizada a [0,1] por separado.
            // El 0 no debería llegar nunca; si llegara, la molécula colapsaría a un punto.
            float mScale = m.scale > 0f ? m.scale : 1f;

            var ids = new int[m.atoms.Length];

            for (int i = 0; i < m.atoms.Length; i++)
            {
                ids[i] = -1;
                var a = m.atoms[i];
                if (a == null) continue;

                // Fórmula del contrato: la posición normalizada se centra restando 0.5
                // ANTES de escalar, o la molécula quedaría colgada de una esquina del
                // offset en vez de centrada en él.
                Vector3 local = a.position != null ? a.position.ToVector3() : Vector3.zero;
                Vector3 angstroms = (local - Vector3.one * 0.5f) * mScale + offset;
                Vector3 world = angstroms * worldScale;

                if (sink != null)
                {
                    // Un elemento fuera del catálogo devuelve null. Se salta el átomo en
                    // vez de abortar la escena: perder uno es mejor que no pintar nada.
                    var atom = sink.SpawnFromScene(a.type, world);
                    if (!atom) continue;
                    ids[i] = atom.id;
                    byId[atom.id] = atom;
                }
                else
                {
                    ids[i] = nextInternalId++;
                    SpawnAtom(ids[i], a.type, world);
                }

                atomMolecule[ids[i]] = m.id;
            }

            if (!string.IsNullOrEmpty(m.id)) moleculeAtomIds[m.id] = ids;

            if (m.bonds != null)
                foreach (var b in m.bonds)
                {
                    if (b == null) continue;
                    if (b.beginAtomId < 0 || b.beginAtomId >= ids.Length) continue;
                    if (b.endAtomId   < 0 || b.endAtomId   >= ids.Length) continue;
                    if (ids[b.beginAtomId] < 0 || ids[b.endAtomId] < 0) continue;  // átomo saltado
                    bonds.Add((ids[b.beginAtomId], ids[b.endAtomId], b.order));
                }
        }

        bondRenderer?.SetBonds(bonds);
    }

    /// <summary>Enciende el resaltado de los átomos que indica el estado.
    ///
    /// Apaga todo y vuelve a encender: el servidor manda el conjunto COMPLETO de
    /// resaltados, así que recalcular desde cero es siempre correcto y evita arrastrar
    /// los de un comando anterior.
    ///
    /// Los bondIds todavía no se dibujan: los botones de la Fase 2 solo generan
    /// selectores por elemento, que producen átomos. Cuando el intérprete (Fase 3)
    /// pueda pedir "los enlaces O-H", hay que resaltarlos aquí también.</summary>
    public void ApplyHighlights(HighlightDTO[] highlights)
    {
        foreach (var atom in byId.Values) if (atom) atom.SetSelected(false);
        if (highlights == null) return;

        foreach (var h in highlights)
        {
            if (h == null || h.atomIds == null || string.IsNullOrEmpty(h.moleculeId)) continue;
            if (!moleculeAtomIds.TryGetValue(h.moleculeId, out var ids)) continue;

            foreach (int localId in h.atomIds)
            {
                if (localId < 0 || localId >= ids.Length || ids[localId] < 0) continue;
                if (byId.TryGetValue(ids[localId], out var atom) && atom)
                    atom.SetSelected(true);
            }
        }
    }

    /// <summary>Molécula a la que pertenece un átomo, o "" si no se conoce.</summary>
    public string MoleculeOf(int atomId)
        => atomMolecule.TryGetValue(atomId, out var id) ? id : "";

    public string IsolatedMolecule => isolated;

    /// <summary>Deja encendida una molécula y atenúa el resto. Pasar "" lo restablece.
    ///
    /// Es LOCAL: no viaja al servidor ni afecta a los demás alumnos. Se pierde al
    /// redibujar, que es lo correcto: si el docente cambió la escena, los ids de antes
    /// puede que ya no existan.</summary>
    public void SetIsolated(string moleculeId)
    {
        isolated = moleculeId ?? "";
        bool isolating = !string.IsNullOrEmpty(isolated);

        foreach (var kv in byId)
        {
            if (!kv.Value) continue;
            bool dim = isolating && MoleculeOf(kv.Key) != isolated;
            kv.Value.SetDimmed(dim);
        }
    }

    /// <summary>Orienta los enlaces según la cámara. Llamar cada frame.</summary>
    public void UpdateVisuals() => bondRenderer?.UpdateVisuals();

    public void Clear()
    {
        bondRenderer?.Clear();
        bonds.Clear();
        byId.Clear();
        moleculeAtomIds.Clear();
        atomMolecule.Clear();
        isolated = "";

        // Con sink, los átomos son suyos y los destruye él. Sin sink son estas esferas.
        if (sink != null) sink.ClearAtoms();
        else foreach (var go in spawned) if (go) Object.Destroy(go);
        spawned.Clear();
    }

    public void Dispose()
    {
        Clear();
        bondRenderer?.Dispose();
    }

    void SpawnAtom(int id, string element, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = $"Atom_{element}_{id}";
        // El collider SE QUEDA: es lo que permite tocar un átomo para aislar su molécula.
        // (Los cilindros de los enlaces sí pierden el suyo, en BondRenderer.)
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
