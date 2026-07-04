/// <summary>
/// Datos en memoria del flujo "Olvidé mi contraseña" (recuperación por correo).
/// Se llenan paso a paso y se limpian al terminar o al volver al login.
///   Paso 1 → Email (se pide el código)
///   Paso 2 → Code  (se verifica el código)
///   Paso 3 → usa Email + Code para cambiar la contraseña.
/// </summary>
public static class PasswordResetData
{
    public static string Email { get; set; } = "";
    public static string Code  { get; set; } = "";

    public static void Clear()
    {
        Email = Code = "";
    }
}
