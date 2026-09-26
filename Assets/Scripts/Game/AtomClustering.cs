using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Agrupa átomos por CERCANÍA en componentes conexas. NO decide enlaces: solo separa
/// candidatos para mandarlos por separado al detector, y cuenta fragmentos para el
/// aviso de topes de la pizarra.
///
/// Vive aparte porque lo usan dos sitios con motivos distintos (BondManager para
/// detectar, la pizarra para avisar), y tener dos copias de un union-find es la clase
/// de duplicado que se desincroniza sin que nadie lo note.
///
/// OJO: este corte es el del CLIENTE y no es el mismo que usa el servidor al partir la
/// escena en moléculas. Sirve para aproximar, no para afirmar.
/// </summary>
public static class AtomClustering
{
    public static List<List<Atom3D>> Group(IList<Atom3D> atoms, float maxDistance)
    {
        var groups = new List<List<Atom3D>>();
        if (atoms == null || atoms.Count == 0) return groups;

        var parent = new Dictionary<int, int>();
        foreach (var a in atoms) if (a) parent[a.id] = a.id;

        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int x, int y) { parent[Find(x)] = Find(y); }

        float d2 = maxDistance * maxDistance;
        for (int i = 0; i < atoms.Count; i++)
        {
            if (!atoms[i]) continue;
            for (int j = i + 1; j < atoms.Count; j++)
            {
                if (!atoms[j]) continue;
                if ((atoms[i].transform.position - atoms[j].transform.position).sqrMagnitude <= d2)
                    Union(atoms[i].id, atoms[j].id);
            }
        }

        var byRoot = new Dictionary<int, List<Atom3D>>();
        foreach (var a in atoms)
        {
            if (!a) continue;
            int r = Find(a.id);
            if (!byRoot.TryGetValue(r, out var g)) { g = new List<Atom3D>(); byRoot[r] = g; }
            g.Add(a);
        }

        groups.AddRange(byRoot.Values);
        return groups;
    }

    /// <summary>Cuántos fragmentos conexos hay. Es lo que el servidor contará como
    /// "moléculas" al publicar, aproximado con el corte del cliente.</summary>
    public static int CountFragments(IList<Atom3D> atoms, float maxDistance)
        => Group(atoms, maxDistance).Count;
}
