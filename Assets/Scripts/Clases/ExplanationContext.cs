using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// La molécula que se va a explicar, y de dónde salió. Lo llena quien pulsa el botón
/// y lo lee la pantalla de explicación. Paralelo a ClassContext y DiaryDetailContext.
///
/// POR QUÉ UNA FORMA PROPIA Y NO EL DTO DEL SERVIDOR: la misma pantalla se abre desde
/// tres sitios que no hablan el mismo idioma —la pizarra manda SceneMoleculeDTO, el
/// diario manda JournalEntry, y el universo no manda nada porque ahí la molécula la
/// acaba de detectar el cliente—. Traducir en la entrada deja UNA pantalla en vez de
/// tres ramas dentro de ella.
///
/// No se persiste: si la app se cierra, se vuelve a abrir desde donde se estaba.
/// </summary>
public static class ExplanationContext
{
    /// <summary>Un átomo, con lo que hace falta para dibujarlo y para explicarlo.</summary>
    public struct Atom
    {
        public string  element;
        public Vector3 position;   // en unidades de escena, ya reconstruidas
        public float   en;         // electronegatividad de Pauling; 0 = no se sabe
        public int     charge;
    }

    /// <summary>Un enlace. 'negativeEnd' es el ÍNDICE del átomo que lleva el δ−, o -1 si
    /// el enlace no es polar. Nunca null, para que no se confunda con el átomo 0.</summary>
    public struct Bond
    {
        public int    beginAtomId;
        public int    endAtomId;
        public int    order;
        public string kind;          // "nonpolar" | "polar" | "ionic"; "" = no se sabe
        public int    negativeEnd;
    }

    public static string     Name;
    public static string     Formula;          // en ASCII ("H2O"), como en todo el proyecto
    public static string     CanonicalSmiles;  // con esto se pide lo que falte al servidor
    public static List<Atom> Atoms = new List<Atom>();
    public static List<Bond> Bonds = new List<Bond>();

    /// <summary>La fórmula estructural plana, EN EL MISMO ORDEN que Atoms: Flat2D[i] es
    /// el mismo átomo que Atoms[i]. Esa paridad es lo que convierte la tarjeta en un
    /// puente y no en un dibujo al lado.
    ///
    /// Vacía significa que esa molécula no tiene tarjeta. Pasa con la sal: el libro escribe
    /// la unidad fórmula (Na+ Cl-), 2 átomos contra los 27 de la red, y dibujar eso
    /// rompería la correspondencia. Vacía no es un fallo.</summary>
    public static List<Vector2> Flat2D = new List<Vector2>();

    /// <summary>Si la química para las capas vino con la molécula o hay que ir a buscarla.
    ///
    /// Desde la pizarra viene completa: el estado trae en, charge, kind y negativeEnd en
    /// cada sondeo. Desde el diario NO —su estructura solo lleva type y position— así que
    /// la pantalla tiene que pedirla por SMILES antes de poder encender las capas.</summary>
    public static bool HasChemistry;

    public static bool Has => Atoms != null && Atoms.Count > 0;

    /// <summary>Desde la pizarra: la molécula llega entera y no hace falta pedir nada.</summary>
    public static void SetFromScene(SceneMoleculeDTO m)
    {
        Clear();
        if (m == null) return;

        Name            = m.name ?? "";
        Formula         = m.formula ?? "";
        CanonicalSmiles = m.canonicalSmiles ?? "";

        Vector3 offset = m.offset != null ? m.offset.ToVector3() : Vector3.zero;
        float   scale  = m.scale > 0f ? m.scale : 1f;

        if (m.atoms != null)
            foreach (var a in m.atoms)
            {
                if (a == null) continue;
                Vector3 local = a.position != null ? a.position.ToVector3() : Vector3.zero;
                Atoms.Add(new Atom
                {
                    element  = a.type,
                    // Misma fórmula del contrato que usa el renderer: se centra restando
                    // 0.5 ANTES de escalar, o la molécula cuelga de una esquina.
                    position = (local - Vector3.one * 0.5f) * scale + offset,
                    en       = a.en,
                    charge   = a.charge,
                });
            }

        if (m.bonds != null)
            foreach (var b in m.bonds)
            {
                if (b == null) continue;
                Bonds.Add(new Bond
                {
                    beginAtomId = b.beginAtomId,
                    endAtomId   = b.endAtomId,
                    order       = b.order,
                    kind        = b.kind ?? "",
                    negativeEnd = b.negativeEnd,
                });
            }

        HasChemistry = true;
    }

    /// <summary>Desde el diario o desde el universo: el detalle que devuelve el servidor
    /// por SMILES. Trae la geometría IDEALIZADA del catálogo, no la colocación de quien
    /// construyó la molécula.</summary>
    public static void SetFromDetail(JournalMolecule m)
    {
        Clear();
        if (m == null) return;

        Name            = m.name ?? "";
        Formula         = m.molecularFormula ?? "";
        CanonicalSmiles = m.canonicalSmiles ?? "";

        var st = m.structure;
        if (st?.atoms != null)
            foreach (var a in st.atoms)
            {
                if (a == null) continue;
                var p = a.position;
                Atoms.Add(new Atom
                {
                    element  = a.type,
                    position = p != null ? new Vector3(p.x, p.y, p.z) : Vector3.zero,
                    en       = a.en,
                    charge   = a.charge,
                });
            }

        if (st?.bonds != null)
            foreach (var b in st.bonds)
            {
                if (b == null) continue;
                Bonds.Add(new Bond
                {
                    beginAtomId = b.beginAtomId,
                    endAtomId   = b.endAtomId,
                    order       = b.order,
                    kind        = b.kind ?? "",
                    negativeEnd = b.negativeEnd,
                });
            }

        // Solo se acepta la tarjeta si hay UNA coordenada POR ÁTOMO. Si el servidor
        // mandara una lista de otro largo, la correspondencia por índice sería falsa y la
        // tarjeta señalaría el átomo equivocado sin que nada fallara.
        if (st?.atoms2D != null && st.atoms != null && st.atoms2D.Length == st.atoms.Length)
            foreach (var p in st.atoms2D)
                Flat2D.Add(p != null ? new Vector2(p.x, p.y) : Vector2.zero);

        HasChemistry = true;
    }

    public static void Clear()
    {
        Name = Formula = CanonicalSmiles = "";
        Atoms.Clear();
        Bonds.Clear();
        Flat2D.Clear();
        HasChemistry = false;
    }
}
