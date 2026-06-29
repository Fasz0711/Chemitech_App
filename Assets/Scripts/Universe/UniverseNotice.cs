/// <summary>
/// Avisos pendientes entre escenas relacionados con universos. Por ejemplo, si
/// CrearUniverso no pudo guardar (disco lleno), se marca para que MisUniversos
/// muestre el modal correspondiente al volver.
/// </summary>
public static class UniverseNotice
{
    public static bool PendingStorageFull;
}
