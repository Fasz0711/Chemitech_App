using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reproduce el guion de una molécula sobre el visor 3D.
///
/// LA REGLA QUE LO ORDENA TODO: cada paso se aplica SOBRE EL ANTERIOR, desde el estado
/// base. Así "ir al paso 3" es aplicar 1, 2 y 3 sin animar, y no hay que guardar un
/// estado por paso ni poder deshacer nada. Es la misma regla que la Fase 5 acordó para
/// la escena de clase, por el mismo motivo: quien llega a mitad dibuja el estado directo.
///
/// Los índices de las primitivas son posiciones en animation.atoms, nunca ids. Se
/// comprueban contra lo que el visor tiene dibujado: un guion con un índice fuera de
/// rango se salta esa primitiva en vez de reventar a media explicación delante de la
/// clase.
/// </summary>
public class AnimationPlayer
{
    readonly MoleculeViewer3D viewer;
    readonly MonoBehaviour    host;      // quien presta las corrutinas

    MoleculeAnimation script;
    Coroutine playing;

    /// <summary>Paso actual, base incluida: 0 = nada aplicado.</summary>
    public int  Step  { get; private set; }
    public int  Total => script?.steps?.Length ?? 0;
    public bool IsPlaying => playing != null;

    /// <summary>Lo que se explica ahora mismo, para ponerlo en pantalla.</summary>
    public string Caption { get; private set; } = "";

    public AnimationPlayer(MoleculeViewer3D viewer, MonoBehaviour host)
    {
        this.viewer = viewer;
        this.host   = host;
    }

    /// <summary>Guarda el guion y dice si sirve. NO TOCA LO QUE SE VE.
    ///
    /// Esto importa: la escena del guion PUEDE SER OTRA COSA que la molécula. En la sal
    /// son dos iones contra los 27 de la red, porque lo que se explica es el gesto y con
    /// la red entera el electrón se perdería entre las aristas. Si cargar dibujara, abrir
    /// la sal convertiría la red en dos átomos sin que nadie pulsara nada.
    ///
    /// La escena del guion se dibuja al REPRODUCIR, que es cuando el usuario lo pidió.</summary>
    public bool Load(MoleculeAnimation animation)
    {
        Stop();
        script = (animation != null && animation.Has) ? animation : null;
        return script != null;
    }

    /// <summary>Dibuja los átomos del GUION, que pueden ser más que los de la molécula:
    /// el puente de hidrógeno trae dos aguas aunque el alumno tocara una.</summary>
    void ShowBase()
    {
        Step = 0;
        Caption = "";
        pairPos.Clear();
        if (viewer == null || script == null) return;

        var st = new JournalStructure { atoms = script.atoms, bonds = script.bonds };
        viewer.Show(st);
    }

    /// <summary>Reproduce desde el principio.</summary>
    public void Play(System.Action onFinished = null)
    {
        Stop();
        if (script == null || host == null) return;
        ShowBase();
        playing = host.StartCoroutine(Run(onFinished));
    }

    public void Stop()
    {
        if (playing != null && host != null) host.StopCoroutine(playing);
        playing = null;
    }

    /// <summary>Deja la escena como al final del paso indicado, SIN animar. Lo usa
    /// adelantar y rebobinar.</summary>
    public void GoTo(int step)
    {
        Stop();
        if (script == null) return;

        ShowBase();
        pairPos.Clear();
        int target = Mathf.Clamp(step, 0, Total);
        for (int i = 0; i < target; i++) ApplyInstant(script.steps[i]);
        Step = target;
        Caption = target > 0 ? (script.steps[target - 1].caption ?? "") : "";
    }

    IEnumerator Run(System.Action onFinished)
    {
        for (int i = 0; i < Total; i++)
        {
            var step = script.steps[i];
            Caption = step.caption ?? "";
            Step    = i + 1;

            yield return AnimateStep(step);

            // Respiro entre pasos: sin él, el texto de uno se lee encima del siguiente.
            float pausa = Mathf.Max(0.35f, step.durationMs / 1000f * 0.25f);
            yield return new WaitForSecondsRealtime(pausa);
        }

        playing = null;
        onFinished?.Invoke();
    }

    // ── Aplicar un paso ───────────────────────────────────────────────────────

    /// <summary>Todas las primitivas de un paso corren A LA VEZ, no una tras otra: un
    /// paso es un momento, no una lista de tareas. "El hidrógeno se acerca mientras la
    /// carga se desplaza" es un paso, y contarlo en serie lo rompería.</summary>
    IEnumerator AnimateStep(AnimationStep step)
    {
        if (step?.primitives == null) yield break;

        var pendientes = new List<IEnumerator>();
        foreach (var p in step.primitives)
        {
            var rutina = Animate(p);
            if (rutina != null) pendientes.Add(rutina);
        }

        bool vivo = true;
        while (vivo)
        {
            vivo = false;
            foreach (var r in pendientes) if (r.MoveNext()) vivo = true;
            yield return null;
        }
    }

    IEnumerator Animate(AnimationPrimitive p)
    {
        if (p == null || viewer == null) return null;
        if (!Valid(p)) return null;

        switch (p.kind)
        {
            case "move":     return MoveAtom(p);
            case "transfer": return Transfer(p);
            case "share":    return Share(p);
            case "bond":     return BondNow(p);
            case "attract":  return AttractNow(p);
            default:         return null;
        }
    }

    /// <summary>Un índice fuera de rango se salta. El servidor valida los guiones antes
    /// de guardarlos, pero un guion viejo en una base vieja no debería tumbar la
    /// explicación entera: perder una primitiva es mejor que perder la pantalla.</summary>
    bool Valid(AnimationPrimitive p)
    {
        int n = viewer.AtomCount;
        bool from = p.fromAtom >= 0 && p.fromAtom < n;
        bool to   = p.toAtom   >= 0 && p.toAtom   < n;

        switch (p.kind)
        {
            case "move":     return from;
            case "transfer":
            case "share":
            case "bond":
            case "attract":  return from && to;
            default:         return false;
        }
    }

    IEnumerator MoveAtom(AnimationPrimitive p)
    {
        Vector3 desde = viewer.GetAtomPosition(p.fromAtom);
        Vector3 hasta = Destino(p, desde);

        float dur = Secs(p.durationMs);
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            viewer.SetAtomPosition(p.fromAtom, Vector3.Lerp(desde, hasta, Suave(t / dur)));
            yield return null;
        }
        viewer.SetAtomPosition(p.fromAtom, hasta);
    }

    IEnumerator Transfer(AnimationPrimitive p)
    {
        Vector3 desde = viewer.GetAtomPosition(p.fromAtom);
        Vector3 hasta = viewer.GetAtomPosition(p.toAtom);

        var electron = viewer.SpawnElectron(desde);
        float dur = Secs(p.durationMs);

        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            if (electron) electron.localPosition = Vector3.Lerp(desde, hasta, Suave(t / dur));
            yield return null;
        }

        // El electrón se queda en el destino: es donde acabó, y verlo ahí es la mitad de
        // lo que el paso explica.
        if (electron) electron.localPosition = hasta;
    }

    /// <summary>El par compartido se desliza desde donde esté hasta su nueva proporción.
    ///
    /// Es la primitiva que separa COMPARTIR de ENTREGAR: con amount 0 se queda en medio,
    /// con ~0.34 se acerca sin llegar, con 1 se va del todo. Animarlo en vez de colocarlo
    /// de golpe es justo lo que hace visible que el enlace polar es un punto intermedio
    /// entre los otros dos, y no una categoría suelta.</summary>
    IEnumerator Share(AnimationPrimitive p)
    {
        float desde = LastAmount(p.fromAtom, p.toAtom);
        float hasta = Mathf.Clamp01(p.amount);
        float dur   = Secs(p.durationMs);

        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            viewer.SetSharedPair(p.fromAtom, p.toAtom, Mathf.Lerp(desde, hasta, Suave(t / dur)));
            yield return null;
        }
        viewer.SetSharedPair(p.fromAtom, p.toAtom, hasta);
        Remember(p.fromAtom, p.toAtom, hasta);
    }

    // Dónde está ya el par de cada enlace. Sin esto, un paso que lo mueve arrancaría
    // siempre desde el medio y se vería un salto hacia atrás.
    //
    // Se guarda como POSICIÓN A LO LARGO DEL ENLACE (0 = en el átomo menor, 0.5 = en
    // medio, 1 = en el mayor) y no como 'amount', porque amount es DIRECCIONAL: el mismo
    // par descrito desde el otro extremo tiene el valor contrario. Guardar el sentido
    // dentro del dato es lo que evita que un guion que invierta from/to mande el par al
    // lado equivocado, que se vería como un error de química y no de código.
    readonly Dictionary<long, float> pairPos = new Dictionary<long, float>();

    static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

    float LastAmount(int a, int b)
    {
        if (!pairPos.TryGetValue(Key(a, b), out float t)) return 0f;
        float haciaB = (a < b) ? t : 1f - t;      // posición vista desde a -> b
        return Mathf.Clamp01((haciaB - 0.5f) * 2f);
    }

    void Remember(int a, int b, float amount)
    {
        float haciaB = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(amount));
        pairPos[Key(a, b)] = (a < b) ? haciaB : 1f - haciaB;
    }

    IEnumerator BondNow(AnimationPrimitive p)
    {
        viewer.SetBond(p.fromAtom, p.toAtom, p.order, p.bondKind);
        yield break;
    }

    IEnumerator AttractNow(AnimationPrimitive p)
    {
        viewer.SpawnAttraction(p.fromAtom, p.toAtom);
        yield break;
    }

    // ── Sin animar, para saltar ───────────────────────────────────────────────

    void ApplyInstant(AnimationStep step)
    {
        if (step?.primitives == null) return;

        foreach (var p in step.primitives)
        {
            if (p == null || !Valid(p)) continue;

            switch (p.kind)
            {
                case "move":
                    if (p.toPosition != null)
                        viewer.SetAtomPosition(p.fromAtom,
                            Destino(p, viewer.GetAtomPosition(p.fromAtom)));
                    break;

                case "transfer":
                    viewer.SpawnElectron(viewer.GetAtomPosition(p.toAtom));
                    break;

                case "share":   viewer.SetSharedPair(p.fromAtom, p.toAtom, Mathf.Clamp01(p.amount));
                                Remember(p.fromAtom, p.toAtom, Mathf.Clamp01(p.amount)); break;
                case "bond":    viewer.SetBond(p.fromAtom, p.toAtom, p.order, p.bondKind); break;
                case "attract": viewer.SpawnAttraction(p.fromAtom, p.toAtom);              break;
            }
        }
    }

    /// <summary>A dónde va un 'move', ya en el espacio del visor.
    ///
    /// El guion manda coordenadas NORMALIZADAS, las mismas en que viene la molécula. El
    /// visor las centra y las escala al dibujar, así que un destino crudo apuntaría a un
    /// espacio distinto y el átomo se iría de la pantalla.</summary>
    Vector3 Destino(AnimationPrimitive p, Vector3 porDefecto)
    {
        if (p.toPosition == null) return porDefecto;
        return viewer.SourceToLocal(new Vector3(p.toPosition.x, p.toPosition.y, p.toPosition.z));
    }

    // Duración con suelo: un guion con durationMs 0 quedaría en un salto invisible.
    static float Secs(int ms) => Mathf.Max(0.2f, ms / 1000f);

    // Arranca y frena suave; el movimiento lineal se ve mecánico.
    static float Suave(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
}
