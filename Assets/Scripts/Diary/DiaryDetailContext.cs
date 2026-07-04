/// <summary>
/// Molécula seleccionada en el diario (en memoria) para una futura pantalla de
/// detalle. La setea DiaryManager al tocar una tarjeta y la leería esa pantalla.
/// Paralelo a PlayContext / UniverseEditContext.
/// </summary>
public static class DiaryDetailContext
{
    public static JournalEntry Current;
}
