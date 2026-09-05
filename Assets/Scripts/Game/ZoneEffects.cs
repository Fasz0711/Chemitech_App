using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Efectos puntuales de la zona de juego: por ahora, el pulso que marca la
/// formación de un enlace.
///
/// Escala con el nivel de EFECTOS, porque es adorno puro:
///   • Bajo  → nada.
///   • Medio → un anillo que se expande en el punto medio del enlace.
///   • Alto  → además, un destello en cada uno de los dos átomos.
///
/// Se implementa con quads y un anillo dibujado por código en vez de un
/// ParticleSystem: así el color y la opacidad se controlan directamente cada
/// frame, sin depender de que el shader de partículas lea el color por vértice
/// ni de que ese shader quede incluido en la build.
/// </summary>
public class ZoneEffects : MonoBehaviour
{
    public static ZoneEffects Instance { get; private set; }

    const float BOND_DURATION = 0.45f;
    const float BOND_FROM     = 0.30f;
    const float BOND_TO       = 2.40f;

    const float ATOM_DURATION = 0.35f;
    const float ATOM_FROM     = 1.20f;
    const float ATOM_TO       = 2.60f;

    static readonly Color BOND_COLOR = new Color(0.30f, 1.00f, 0.85f);
    static readonly Color ATOM_COLOR = new Color(0.55f, 0.90f, 1.00f);

    class Pulse
    {
        public GameObject go;
        public Material   mat;
        public Vector3    center;
        public Color      color;
        public float      from, to, duration, elapsed;
        public bool       active;
    }

    readonly List<Pulse> pool = new List<Pulse>();
    Texture2D ringTex;
    Transform camT;

    void Awake()
    {
        Instance = this;
        ringTex  = ProceduralTextures.Ring(128, 0.55f, 0.95f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var p in pool) if (p.mat) Destroy(p.mat);
        if (ringTex) Destroy(ringTex);
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>Se formó un enlace entre dos átomos: pulso en el medio (y destellos en Alto).</summary>
    public void BondFormed(Vector3 a, Vector3 b)
    {
        var level = GraphicsManager.Instance.Effects;
        if (level == GraphicsLevel.Bajo) return;

        Emit((a + b) * 0.5f, BOND_COLOR, BOND_FROM, BOND_TO, BOND_DURATION);

        if (level == GraphicsLevel.Alto)
        {
            Emit(a, ATOM_COLOR, ATOM_FROM, ATOM_TO, ATOM_DURATION);
            Emit(b, ATOM_COLOR, ATOM_FROM, ATOM_TO, ATOM_DURATION);
        }
    }

    // ── Animación ─────────────────────────────────────────────────────────────

    void Emit(Vector3 center, Color color, float from, float to, float duration)
    {
        var p = Rent();
        p.center   = center;
        p.color    = color;
        p.from     = from;
        p.to       = to;
        p.duration = duration;
        p.elapsed  = 0f;
        p.active   = true;
        p.go.SetActive(true);
    }

    void LateUpdate()
    {
        if (!camT)
        {
            if (!Camera.main) return;
            camT = Camera.main.transform;
        }

        foreach (var p in pool)
        {
            if (!p.active) continue;

            p.elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(p.elapsed / p.duration);

            if (k >= 1f)
            {
                p.active = false;
                p.go.SetActive(false);
                continue;
            }

            // Crece rápido al principio y se frena; se desvanece de forma lineal.
            float grow = 1f - (1f - k) * (1f - k);
            float size = Mathf.Lerp(p.from, p.to, grow);

            p.go.transform.position   = p.center;
            p.go.transform.localScale = Vector3.one * size;
            p.go.transform.rotation   = Quaternion.LookRotation(p.center - camT.position);

            var c = p.color; c.a = 1f - k;
            p.mat.SetColor("_BaseColor", c);
        }
    }

    // ── Pool ──────────────────────────────────────────────────────────────────

    Pulse Rent()
    {
        foreach (var p in pool) if (!p.active) return p;

        var mat = MakeAdditiveMat(ringTex);

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Pulse";
        var col = go.GetComponent<Collider>(); if (col) Destroy(col);
        go.transform.SetParent(transform, false);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.SetActive(false);

        var pulse = new Pulse { go = go, mat = mat };
        pool.Add(pulse);
        return pulse;
    }

    /// <summary>URP Unlit con mezcla aditiva (ver GuideMaterials).</summary>
    static Material MakeAdditiveMat(Texture2D tex) => GuideMaterials.NewAdditive(tex);
}
