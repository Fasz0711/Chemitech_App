using System.Collections.Generic;
using UnityEngine;
using TMPro;

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

    // Fuente de las etiquetas de elemento. Solo la usa la ruta de SOLO LECTURA: cuando hay
    // sink, los átomos los crea el sistema de colocación y ya vienen etiquetados. Sin
    // ella el alumno veía esferas sin nombre mientras el docente sí los veía.
    readonly TMP_FontAsset labelFont;

    // Mitad del lado de la plataforma, en unidades de mundo. 0 = no recolocar nada.
    readonly float layoutHalf;

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

    // "m1" -> la molécula entera tal como la mandó el servidor. Se guarda porque el
    // estado YA TRAE la química que hace falta para explicarla —en, charge, kind y
    // negativeEnd viajan en cada sondeo desde la Fase 2— y tirarla al dibujar obligaría
    // a volver a pedírsela al servidor para algo que acaba de llegar.
    readonly Dictionary<string, SceneMoleculeDTO> moleculeById = new Dictionary<string, SceneMoleculeDTO>();

    string isolated = "";

    // Los resaltados tienen DOS fuentes que no se pisan: los que manda el docente y la
    // molécula que el alumno acaba de tocar. Se guardan los del servidor porque hay que
    // volver a aplicarlos cada vez que cambia lo local, y al revés.
    HighlightDTO[] serverHighlights;
    string tappedMolecule = "";

    readonly BondRenderer bondRenderer;   // null en modo editable: los pinta BondManager

    static readonly Color FALLBACK_COLOR = new Color(0.72f, 0.75f, 0.82f);

    public ClassSceneRenderer(Transform root, Material atomMaterial, Material bondMaterial,
                              float worldScale, float atomSize, float bondThickness, float bondSpacing,
                              IAtomSink sink = null, TMP_FontAsset labelFont = null,
                              float layoutHalf = 0f)
    {
        this.labelFont  = labelFont;
        this.layoutHalf = layoutHalf;
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

        // Dónde va cada molécula. Normalmente en el sitio que mandó el servidor; solo se
        // recolocan si la escena no cabe en la plataforma.
        Vector3[] shift = LayoutShifts(molecules);
        float lift = FloorLift(molecules);

        // Ids globales: los del JSON son locales a cada molécula (0..n-1), y con varias
        // moléculas en escena se pisarían entre ellas.
        int nextInternalId = 0;

        int mi = -1;
        foreach (var m in molecules)
        {
            mi++;
            if (m == null || m.atoms == null) continue;

            Vector3 offset = m.offset != null ? m.offset.ToVector3() : Vector3.zero;

            // 'scale' es el tamaño del fragmento en las unidades de la escena. Conserva la
            // proporción entre moléculas: sin él, un cristal de sal se vería igual de
            // grande que una molécula de agua, porque cada una llega normalizada a [0,1]
            // por separado.
            //
            // OJO CON LAS UNIDADES: lo que viene del CATÁLOGO llega en ångströms reales
            // (un O–H mide 0.96), mientras que lo construido a mano vuelve en las
            // unidades en que se mandó. Nunca conviven en la misma escena —setAtoms
            // reemplaza todo— pero sí hacen que una misma molécula se vea de distinto
            // tamaño según de dónde venga.
            //
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
                Vector3 scene = (local - Vector3.one * 0.5f) * mScale + offset;
                Vector3 world = scene * worldScale + Vector3.up * lift + shift[mi];

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

            if (!string.IsNullOrEmpty(m.id))
            {
                moleculeAtomIds[m.id] = ids;
                moleculeById[m.id]    = m;
            }

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

    /// <summary>Cuánto hay que mover CADA MOLÉCULA para que la escena quepa, o nada si ya
    /// cabe.
    ///
    /// EL SERVIDOR LAS REPARTE EN FILA. Está bien para dos o tres, pero con la red de
    /// cloruro de sodio —que sola mide 9 unidades— la fila se sale de la plataforma y las
    /// moléculas acaban flotando fuera, que delante de una clase parece un fallo.
    ///
    /// SOLO SE TOCA SI NO CABE, y eso lo hace seguro: lo que el docente coloca a mano se
    /// recorta al colocarlo, así que nunca se sale y nunca entra aquí. Su disposición —dos
    /// aguas juntas para enseñar el puente, por ejemplo— se respeta siempre. Recolocar es
    /// el último recurso frente a dejarlo fuera de la mesa.
    ///
    /// Se colocan en estantes: se ordenan de mayor a menor y se van poniendo en filas,
    /// saltando de fila cuando no cabe. Con tamaños tan distintos —un agua al lado de una
    /// red— una cuadrícula uniforme desperdiciaría casi todo el sitio.</summary>
    Vector3[] LayoutShifts(SceneMoleculeDTO[] molecules)
    {
        var shift = new Vector3[molecules.Length];
        if (layoutHalf <= 0f || molecules.Length == 0) return shift;

        // Tamaño y centro de cada una, ya en unidades de mundo.
        var size   = new float[molecules.Length];
        var center = new Vector3[molecules.Length];
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;

        for (int i = 0; i < molecules.Length; i++)
        {
            var m = molecules[i];
            if (m == null || m.atoms == null || m.atoms.Length == 0) continue;

            Vector3 off = (m.offset != null ? m.offset.ToVector3() : Vector3.zero) * worldScale;
            float   d   = Mathf.Max(0.5f, (m.scale > 0f ? m.scale : 1f) * worldScale);

            size[i]   = d;
            center[i] = off;

            minX = Mathf.Min(minX, off.x - d * 0.5f); maxX = Mathf.Max(maxX, off.x + d * 0.5f);
            minZ = Mathf.Min(minZ, off.z - d * 0.5f); maxZ = Mathf.Max(maxZ, off.z + d * 0.5f);
        }

        if (minX > maxX) return shift;   // nada que medir

        // ¿Cabe tal como viene? Entonces no se toca: las posiciones son del servidor y,
        // si son de algo construido a mano, son intencionadas.
        bool cabe = minX >= -layoutHalf && maxX <= layoutHalf
                 && minZ >= -layoutHalf && maxZ <= layoutHalf;
        if (cabe) return shift;

        // De mayor a menor: las grandes primero aprovechan mejor la fila.
        var orden = new List<int>();
        for (int i = 0; i < molecules.Length; i++) if (size[i] > 0f) orden.Add(i);
        orden.Sort((a, b) => size[b].CompareTo(size[a]));

        const float HUECO = 1.2f;
        float span = layoutHalf * 2f;

        var destino = new Vector3[molecules.Length];
        float x = 0f, z = 0f, altoFila = 0f, anchoMax = 0f;

        foreach (int i in orden)
        {
            if (x > 0f && x + size[i] > span)     // no cabe: siguiente fila
            {
                anchoMax = Mathf.Max(anchoMax, x - HUECO);
                x = 0f;
                z += altoFila + HUECO;
                altoFila = 0f;
            }

            destino[i] = new Vector3(x + size[i] * 0.5f, 0f, z + size[i] * 0.5f);
            x += size[i] + HUECO;
            altoFila = Mathf.Max(altoFila, size[i]);
        }
        anchoMax = Mathf.Max(anchoMax, x - HUECO);
        float altoTotal = z + altoFila;

        // El bloque entero se centra en la plataforma.
        Vector3 centrado = new Vector3(anchoMax * 0.5f, 0f, altoTotal * 0.5f);
        foreach (int i in orden)
            shift[i] = (destino[i] - centrado) - new Vector3(center[i].x, 0f, center[i].z);

        return shift;
    }

    /// <summary>Cuánto hay que subir la escena ENTERA para que ningún átomo atraviese la
    /// plataforma.
    ///
    /// El servidor centra cada molécula en su offset, así que la mitad inferior queda con
    /// Y negativa: un agua del catálogo llega con el oxígeno justo en y=0 y se ve medio
    /// enterrada en el suelo. Lo construido a mano no tiene el problema, porque al
    /// colocarlo ya se apoya encima; de ahí que solo pase "a veces".
    ///
    /// Se eleva TODO con el mismo desplazamiento, nunca átomo por átomo ni molécula por
    /// molécula: recortar cada uno por su cuenta deformaría la molécula, y subir cada
    /// fragmento por separado destruiría la disposición que el docente montó, que es
    /// justo lo que el contrato se molesta en conservar.
    ///
    /// Mismo criterio que "copiar a un universo", que ya resolvía esto al otro lado.</summary>
    float FloorLift(SceneMoleculeDTO[] molecules)
    {
        float minY = float.MaxValue;

        foreach (var m in molecules)
        {
            if (m == null || m.atoms == null) continue;
            Vector3 offset = m.offset != null ? m.offset.ToVector3() : Vector3.zero;
            float   mScale = m.scale > 0f ? m.scale : 1f;

            foreach (var a in m.atoms)
            {
                if (a == null) continue;
                Vector3 local = a.position != null ? a.position.ToVector3() : Vector3.zero;
                float y = ((local - Vector3.one * 0.5f) * mScale + offset).y * worldScale;
                if (y < minY) minY = y;
            }
        }

        if (minY == float.MaxValue) return 0f;

        // El átomo más bajo tiene que apoyarse sobre el suelo, no atravesarlo: su centro
        // va a la altura de su propio radio.
        float floor = atomSize * 0.5f;

        // Solo se sube, nunca se baja: lo que el docente colocó en alto se queda en alto.
        return Mathf.Max(0f, floor - minY);
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
        serverHighlights = highlights;
        RefreshSelection();
    }

    /// <summary>Resalta la molécula ENTERA que el alumno tocó. Pasar "" lo apaga.
    ///
    /// Es LOCAL: no viaja al servidor ni lo ven los demás, igual que el aislamiento. Usa
    /// el mismo resaltado que produce el botón del docente a propósito — que el gesto del
    /// alumno y el del docente se vean igual es lo que permite decir "lo que tú tocaste
    /// es lo que yo estoy marcando".</summary>
    public void SetTappedMolecule(string moleculeId)
    {
        tappedMolecule = moleculeId ?? "";
        RefreshSelection();
    }

    public string TappedMolecule => tappedMolecule;

    /// <summary>Apaga todo y vuelve a encender desde las DOS fuentes.
    ///
    /// Recalcular desde cero es siempre correcto —el servidor manda el conjunto completo
    /// de resaltados— y es lo que evita que aplicar una fuente borre la otra: antes, cada
    /// estado que llegaba del docente apagaba lo que el alumno acababa de tocar.</summary>
    void RefreshSelection()
    {
        foreach (var atom in byId.Values) if (atom) atom.SetSelected(false);

        if (serverHighlights != null)
            foreach (var h in serverHighlights)
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

        // Lo tocado va DESPUÉS: si el docente resaltó solo los oxígenos y el alumno toca
        // la molécula, se ve la molécula entera, que es lo que acaba de pedir.
        if (!string.IsNullOrEmpty(tappedMolecule)
            && moleculeAtomIds.TryGetValue(tappedMolecule, out var todos))
            foreach (int id in todos)
                if (id >= 0 && byId.TryGetValue(id, out var atom) && atom)
                    atom.SetSelected(true);
    }

    /// <summary>Molécula a la que pertenece un átomo, o "" si no se conoce.</summary>
    public string MoleculeOf(int atomId)
        => atomMolecule.TryGetValue(atomId, out var id) ? id : "";

    /// <summary>La molécula completa, con su nombre, su fórmula, su SMILES y la química
    /// de cada átomo y cada enlace. Null si ese id no está en la escena.</summary>
    public SceneMoleculeDTO MoleculeInfo(string moleculeId)
        => !string.IsNullOrEmpty(moleculeId) && moleculeById.TryGetValue(moleculeId, out var m) ? m : null;

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
        moleculeById.Clear();
        atomMolecule.Clear();
        isolated = "";
        // Al redibujar, los ids de antes pueden no existir: lo tocado se suelta.
        tappedMolecule   = "";
        serverHighlights = null;

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

        AddLabel(go.transform, element, color);
    }

    /// <summary>El símbolo sobre el átomo, mirando siempre a la cámara. Es la misma
    /// etiqueta que pone el sistema de colocación en la pizarra del docente; aquí hacía
    /// falta porque el alumno no pasa por ese camino.</summary>
    void AddLabel(Transform parent, string element, Color atomColor)
    {
        if (!labelFont || string.IsNullOrEmpty(element)) return;

        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * (atomSize * 0.5f + 0.46f);
        go.transform.localScale    = Vector3.one * 0.20f;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = element;
        tmp.font = labelFont;
        tmp.fontSize = element.Length > 1 ? 10f : 12f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.GetComponent<RectTransform>().sizeDelta = new Vector2(4f, 2f);

        go.AddComponent<Billboard>();
    }

    /// <summary>El átomo más cercano al toque en PANTALLA, o null si ninguno cae dentro
    /// del radio. Es el repuesto de un rayo exacto.
    ///
    /// Hace falta porque EL DEDO TAPA JUSTO LO QUE QUIERE TOCAR: el alumno apunta a una
    /// esfera, la yema cubre medio centímetro de pantalla y el punto que el sistema
    /// reporta cae a un lado. Con un rayo infinitamente fino eso es un fallo; con un
    /// radio de dedo, es el átomo que el alumno estaba mirando.
    ///
    /// Se mide en pantalla y no en el mundo a propósito: "mi dedo cubre esto" es una
    /// distancia en píxeles, y en píxeles un átomo lejano ocupa menos, que es justo el
    /// comportamiento que se quiere.
    ///
    /// La ruta del docente hace esto mismo en AtomPlacementController.</summary>
    public Atom3D NearestAtomOnScreen(Camera camera, Vector2 screenPosition, float maxPixels)
    {
        if (!camera) return null;

        Atom3D best = null;
        float  bestD = maxPixels;

        foreach (var atom in byId.Values)
        {
            if (!atom) continue;
            Vector3 sp = camera.WorldToScreenPoint(atom.transform.position);
            if (sp.z <= 0f) continue;   // detrás de la cámara: se proyecta al revés
            float d = Vector2.Distance(screenPosition, new Vector2(sp.x, sp.y));
            if (d < bestD) { bestD = d; best = atom; }
        }
        return best;
    }
}
