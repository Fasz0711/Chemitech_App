using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arma la animación de CÓMO SE FORMA una molécula, deduciéndola de su propia química.
/// Sin guion escrito a mano y sin pedirle nada al servidor.
///
/// POR QUÉ SE PUEDE: lo que decide la historia ya llega con cada molécula —la
/// electronegatividad de cada átomo, el tipo de cada enlace y qué extremo lleva el δ−—.
/// Con eso la narración sale sola, y sale igual para las 24 elementos y para cualquier
/// compuesto, incluido lo que un docente construya a mano en la pizarra.
///
/// LO QUE NO SE PUEDE DEDUCIR, y por eso sigue habiendo guiones escritos: dónde colocar
/// una SEGUNDA molécula para que el puente de hidrógeno se vea bien. Esa geometría no
/// sale de ninguna fórmula.
///
/// LA DISTINCIÓN QUE ORDENA TODO, y que es el corazón de la lección:
///     no polar  el par se comparte por igual y se queda en medio
///     polar     el par se comparte pero SE DESPLAZA; ahí nacen δ− y δ+
///     iónico    el electrón SE ENTREGA y se va del todo; quedan iones
/// Mostrar un enlace polar como una entrega enseñaría lo contrario de lo que se quiere
/// medir en el postest, así que la diferencia no es un matiz: es el contenido.
/// </summary>
public static class GenericAnimation
{
    // Cuánto se separan los átomos al empezar. Con el encuadre hecho sobre las posiciones
    // FINALES, 1.45 es lo que cabe sin salirse: el visor deja un 30% de margen.
    const float SEPARACION = 1.45f;

    // Cuánto se desplaza el par en un enlace polar. No es 1 a propósito: sigue COMPARTIDO,
    // solo que más cerca de uno. Si llegara al átomo sería un enlace iónico.
    const float DESPLAZAMIENTO_POLAR = 0.34f;

    const int MS_ACERCARSE = 1600;
    const int MS_COMPARTIR  = 1000;
    const int MS_DESPLAZAR  = 1400;

    /// <summary>Construye el guion, o null si la molécula no da para contar nada (un
    /// átomo suelto, o una estructura sin enlaces).</summary>
    public static MoleculeAnimation Build(IList<ExplanationContext.Atom> atoms,
                                          IList<ExplanationContext.Bond> bonds,
                                          string moleculeName)
    {
        if (atoms == null || bonds == null || atoms.Count < 2 || bonds.Count == 0) return null;

        // Los átomos se dan en su posición FINAL, no en la de partida. El visor encuadra
        // por la extensión de lo que recibe, así que darle la separada encuadraría sobre
        // ella y la molécula acabaría a la mitad de tamaño con las esferas iguales: los
        // enlaces se perdían, y se notaba sobre todo en las moléculas alargadas. El
        // reproductor los separa después, ya encuadrado.
        var partida = new JournalAtom[atoms.Count];
        for (int i = 0; i < atoms.Count; i++)
            partida[i] = new JournalAtom
            {
                type     = atoms[i].element,
                position = Vec(atoms[i].position),
                en       = atoms[i].en,
                charge   = atoms[i].charge,
            };

        var pasos = new List<AnimationStep>();

        // ── Paso 1: se acercan y nacen los enlaces ────────────────────────────
        var acercarse = new List<AnimationPrimitive>();
        for (int i = 0; i < atoms.Count; i++)
            acercarse.Add(new AnimationPrimitive
            {
                kind = "move", fromAtom = i, toAtom = -1,
                toPosition = Vec(atoms[i].position), durationMs = MS_ACERCARSE,
            });

        foreach (var b in bonds)
            acercarse.Add(new AnimationPrimitive
            {
                kind = "bond", fromAtom = b.beginAtomId, toAtom = b.endAtomId,
                order = Mathf.Max(1, b.order), bondKind = b.kind,
                bondNegativeEnd = b.negativeEnd, durationMs = 0,
            });

        pasos.Add(new AnimationStep
        {
            caption    = "Los átomos se acercan y forman enlaces.",
            durationMs = MS_ACERCARSE,
            primitives = acercarse.ToArray(),
        });

        // ── Paso 2: cada enlace estrena su par compartido, en el medio ────────
        //
        // SOLO PARA LOS ENLACES DONDE DE VERDAD SE COMPARTE. En un enlace iónico no hay
        // par compartido en ningún momento: el electrón se entrega y ya.
        //
        // No es un matiz: la frase de este paso —"un par de electrones que los dos átomos
        // comparten"— es casi literal la ALTERNATIVA INCORRECTA del ítem 5 de la prueba
        // ("Porque comparten un par de electrones", forma A; "Un par de electrones que
        // comparten", forma B), y contradice la respuesta correcta del ítem 4. Mostrársela
        // al alumno durante la lección sería enseñarle el distractor con la app que la
        // prueba está midiendo. Con la sal, este paso ahora no existe.
        var compartir = new List<AnimationPrimitive>();
        foreach (var b in bonds)
        {
            if (b.kind == "ionic") continue;
            compartir.Add(new AnimationPrimitive
            {
                kind = "share", fromAtom = b.beginAtomId, toAtom = b.endAtomId,
                amount = 0f, durationMs = MS_COMPARTIR,
            });
        }

        if (compartir.Count > 0)
            pasos.Add(new AnimationStep
            {
                caption    = "Cada enlace es un par de electrones que los dos átomos comparten.",
                durationMs = MS_COMPARTIR,
                primitives = compartir.ToArray(),
            });

        // ── Paso 3: el par se desplaza, o se entrega ──────────────────────────
        // Se separan polares e iónicos EN PASOS DISTINTOS cuando conviven, porque la frase
        // que los explica no es la misma y meterlos juntos obligaría a una genérica.
        var polares = new List<AnimationPrimitive>();
        var ionicos = new List<AnimationPrimitive>();

        foreach (var b in bonds)
        {
            bool ionico = b.kind == "ionic";

            // UN ENLACE IÓNICO LLEGA CON negativeEnd = -1, Y ES CORRECTO QUE ASÍ SEA: δ−
            // es una carga PARCIAL y en la sal las cargas son enteras, así que el servidor
            // manda el centinela y deja esa información en 'charge'. Esta función lo
            // descartaba igual que a un no polar, de modo que los 54 enlaces de la sal se
            // perdían y la molécula terminaba cayendo en el último recurso: "los dos
            // atraen igual", que es lo contrario de lo que la sal enseña y de lo que
            // preguntan los ítems 4 y 5.
            //
            // Quién recibe el electrón se deduce de quién atrae más, que es el dato que
            // define el enlace iónico y que siempre viaja en cada átomo.
            int hacia, desde;

            if (ionico)
            {
                bool finAtraeMas = Atraccion(atoms, b.endAtomId) >= Atraccion(atoms, b.beginAtomId);
                hacia = finAtraeMas ? b.endAtomId : b.beginAtomId;
                desde = finAtraeMas ? b.beginAtomId : b.endAtomId;
            }
            else
            {
                if (b.negativeEnd < 0) continue;   // no polar: el par se queda donde está
                hacia = b.negativeEnd;
                desde = (hacia == b.beginAtomId) ? b.endAtomId : b.beginAtomId;
            }

            // IÓNICO: un electrón que SALE del que entrega y LLEGA al que recibe.
            // No es un par que se desliza, porque en la sal nunca hubo par: deslizar
            // uno contaría la historia del ítem 5 al revés. Es además lo que la matriz
            // de especificaciones le promete al ítem 4, "la animación del electrón que
            // pasa del sodio al cloro".
            if (ionico)
                ionicos.Add(new AnimationPrimitive
                {
                    kind = "transfer", fromAtom = desde, toAtom = hacia,
                    durationMs = MS_DESPLAZAR,
                });

            // POLAR: el par sigue existiendo y solo se corre. La diferencia entre las
            // dos primitivas ES la diferencia que la prueba mide.
            else
                polares.Add(new AnimationPrimitive
                {
                    kind = "share", fromAtom = desde, toAtom = hacia,
                    amount = DESPLAZAMIENTO_POLAR, durationMs = MS_DESPLAZAR,
                });
        }

        if (polares.Count > 0)
            pasos.Add(new AnimationStep
            {
                caption    = "El átomo que atrae más jala el par hacia su lado: "
                           + "sigue compartido, pero ese lado queda δ− y el otro δ+.",
                durationMs = MS_DESPLAZAR,
                primitives = polares.ToArray(),
            });

        if (ionicos.Count > 0)
            pasos.Add(new AnimationStep
            {
                caption    = "Aquí no se comparte: el electrón SE ENTREGA del todo. "
                           + "Los átomos quedan con carga y se atraen como iones.",
                durationMs = MS_DESPLAZAR,
                primitives = ionicos.ToArray(),
            });

        // Ni un polar ni un iónico: todo el reparto es simétrico, y eso también se dice.
        if (polares.Count == 0 && ionicos.Count == 0)
            pasos.Add(new AnimationStep
            {
                caption    = "Los dos atraen igual, así que el par se queda en medio: "
                           + "el enlace no tiene lados.",
                durationMs = MS_COMPARTIR,
                primitives = new AnimationPrimitive[0],
            });

        return new MoleculeAnimation
        {
            id     = "generico",
            title  = string.IsNullOrEmpty(moleculeName) ? "Cómo se forma" : "Cómo se forma " + moleculeName,
            atoms      = partida,
            bonds      = new JournalBond[0],   // nacen en el paso 1
            separation = SEPARACION,
            steps  = pasos.ToArray(),
        };
    }

    /// <summary>Cuánto atrae un átomo. Es su electronegatividad tal cual; existe como
    /// función para que el índice fuera de rango no tumbe la explicación entera.</summary>
    static float Atraccion(IList<ExplanationContext.Atom> atoms, int i)
        => (atoms != null && i >= 0 && i < atoms.Count) ? atoms[i].en : 0f;

    static JournalVec3 Vec(Vector3 v) => new JournalVec3 { x = v.x, y = v.y, z = v.z };
}
