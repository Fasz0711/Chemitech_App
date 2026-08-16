using UnityEngine;

/// <summary>
/// Animación de aparición de un átomo: crece desde cero, se pasa un poco del
/// tamaño final y se asienta. Se autodestruye al terminar.
///
/// Se aplica sobre la ESFERA hija, nunca sobre el objeto raíz del átomo: el raíz
/// es el que lee BondManager para calcular posiciones y detectar la estructura,
/// y animarlo dispararía detecciones contra el backend mientras dura el efecto.
/// </summary>
[DisallowMultipleComponent]
public class SpawnPop : MonoBehaviour
{
    const float DURATION  = 0.22f;
    const float OVERSHOOT = 1.7f;   // cuánto se pasa antes de asentarse

    Vector3 target;
    float   elapsed;

    /// <summary>Arranca el efecto hacia 'finalScale'.</summary>
    public void Play(Vector3 finalScale)
    {
        target  = finalScale;
        elapsed = 0f;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float k = Mathf.Clamp01(elapsed / DURATION);

        transform.localScale = target * EaseOutBack(k);

        if (k >= 1f)
        {
            transform.localScale = target;
            Destroy(this);
        }
    }

    /// <summary>Se pasa del destino y vuelve: da la sensación de "plop" elástico.</summary>
    static float EaseOutBack(float k)
    {
        float c1 = OVERSHOOT;
        float c3 = c1 + 1f;
        float p  = k - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
