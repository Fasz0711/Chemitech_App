using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// El comando `setAtoms`: publica en la pizarra lo que el docente construyó a mano.
///
/// Contrato: docs/CONTRATO_PIZARRA_UNIVERSO.txt §1. Se manda SOLO la lista plana de
/// átomos. Nada de enlaces —el servidor los recalcula, y así no puede haber dos
/// versiones de la química— y nada de agrupar: el servidor parte la escena en moléculas
/// con un criterio RELATIVO, calibrado para que un enlace quede dentro y un puente de
/// hidrógeno fuera, que es lo que la lección necesita.
///
/// LA ESCALA NO IMPORTA  [contrato del 26/09]. El servidor la estima de la propia
/// escena, así que las coordenadas van en las unidades que tenga el universo y no hay
/// nada que convertir. Antes se exigían ångströms reales y eso era un bug: el universo
/// coloca todos los pares con el mismo espaciado, mientras que las longitudes reales van
/// de 0.97 (O–H) a 2.68 (Na–Cl), así que ningún factor único las deja bien a la vez. Un
/// agua construida a mano llegaba con los átomos demasiado separados para el criterio
/// del servidor y se publicaba como TRES ÁTOMOS SUELTOS SIN ENLACES.
///
/// Es el ESTADO COMPLETO del universo: publicar siempre reemplaza.
/// </summary>
public static class SetAtomsCommand
{
    // ── Topes del servidor (contrato §3) ──────────────────────────────────────
    // Se repiten aquí para poder AVISAR MIENTRAS SE CONSTRUYE. Dejar al docente
    // construir diez minutos y fallar al publicar es lo peor que puede pasar delante
    // de 25 alumnos. El servidor sigue siendo quien decide: esto solo anticipa.
    public const int MAX_TOTAL_ATOMS = 60;   // contando hidrógenos
    public const int MAX_FRAGMENTS   = 12;   // fragmentos conexos = "moléculas"
    public const int MAX_HEAVY_PER_FRAGMENT = 30;

    /// <summary>Cuánto margen queda antes de avisar. A partir de aquí el aviso aparece.</summary>
    public const int WARN_MARGIN = 6;

    /// <summary>Construye el actionsJson.
    ///
    /// 'worldToScene' es el INVERSO del factor con el que se dibuja lo que llega del
    /// servidor. Ya no convierte a ninguna unidad física —al servidor le da igual la
    /// escala— pero tiene que seguir siendo el inverso exacto: es lo que garantiza que
    /// publicar y volver a dibujar devuelva los átomos al mismo sitio en vez de encoger
    /// o agrandar el universo en cada publicación.</summary>
    public static string Build(IList<Atom3D> atoms, float worldToScene)
    {
        var sb = new StringBuilder(64 + (atoms?.Count ?? 0) * 56);
        sb.Append(@"[{""action"":""setAtoms"",""atoms"":[");

        bool first = true;
        if (atoms != null)
            foreach (var a in atoms)
            {
                if (!a || string.IsNullOrEmpty(a.element)) continue;
                if (!first) sb.Append(',');
                first = false;

                Vector3 p = a.transform.position * worldToScene;
                sb.Append(@"{""element"":""").Append(a.element).Append(@""",""x"":").Append(F(p.x))
                  .Append(@",""y"":").Append(F(p.y))
                  .Append(@",""z"":").Append(F(p.z)).Append('}');
            }

        sb.Append("]}]");
        return sb.ToString();
    }

    // Un celular con el idioma en español escribe 0,76 en vez de 0.76, y eso no es un
    // número JSON: el servidor rechazaría el comando entero con ERR_INVALID_ACTION y
    // solo en los dispositivos con esa configuración. De ahí el InvariantCulture.
    // Cuatro decimales sobran de sobra para cualquier escala razonable.
    static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    // ── Cuenta para el aviso ──────────────────────────────────────────────────

    public struct Counts
    {
        public int total;          // átomos, hidrógenos incluidos
        public int fragments;      // aproximado: el corte del cliente no es el del servidor
        public int biggestHeavy;   // átomos pesados del fragmento más grande

        public bool OverLimit => total > MAX_TOTAL_ATOMS
                              || fragments > MAX_FRAGMENTS
                              || biggestHeavy > MAX_HEAVY_PER_FRAGMENT;

        public bool NearLimit => total >= MAX_TOTAL_ATOMS - WARN_MARGIN
                              || fragments >= MAX_FRAGMENTS - 2
                              || biggestHeavy >= MAX_HEAVY_PER_FRAGMENT - WARN_MARGIN;
    }

    public static Counts Count(IList<Atom3D> atoms, float clusterDistance)
    {
        var c = new Counts();
        if (atoms == null || atoms.Count == 0) return c;

        foreach (var a in atoms) if (a) c.total++;

        foreach (var group in AtomClustering.Group(atoms, clusterDistance))
        {
            c.fragments++;
            int heavy = 0;
            foreach (var a in group) if (a && a.element != "H") heavy++;
            if (heavy > c.biggestHeavy) c.biggestHeavy = heavy;
        }
        return c;
    }

    /// <summary>El aviso a mostrar, o "" si no hay nada que avisar. Habla de lo que el
    /// docente puede hacer, no del error que va a recibir.</summary>
    public static string WarningFor(Counts c)
    {
        if (c.total > MAX_TOTAL_ATOMS)
            return $"Te pasaste del máximo: {c.total} átomos de {MAX_TOTAL_ATOMS}. Quita algunos antes de publicar.";
        if (c.fragments > MAX_FRAGMENTS)
            return $"Demasiados grupos sueltos: {c.fragments} de {MAX_FRAGMENTS}. Junta o quita algunos.";
        if (c.biggestHeavy > MAX_HEAVY_PER_FRAGMENT)
            return $"Una molécula es demasiado grande: {c.biggestHeavy} átomos pesados de {MAX_HEAVY_PER_FRAGMENT}.";

        if (c.total >= MAX_TOTAL_ATOMS - WARN_MARGIN)
            return $"Quedan {MAX_TOTAL_ATOMS - c.total} átomos disponibles.";
        if (c.fragments >= MAX_FRAGMENTS - 2)
            return $"Quedan {MAX_FRAGMENTS - c.fragments} grupos disponibles.";
        if (c.biggestHeavy >= MAX_HEAVY_PER_FRAGMENT - WARN_MARGIN)
            return $"Esa molécula ya casi llena el máximo ({c.biggestHeavy} de {MAX_HEAVY_PER_FRAGMENT}).";

        return "";
    }
}
