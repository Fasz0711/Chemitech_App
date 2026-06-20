/// <summary>
/// Contexto de juego en memoria: qué universo se está jugando.
/// Se setea al pulsar "Jugar" en MisUniversos y lo lee la zona de juego.
/// </summary>
public static class PlayContext
{
    public static UniverseData Current;

    public static string UniverseName =>
        (Current != null && !string.IsNullOrEmpty(Current.name)) ? Current.name : "Universo";
}
