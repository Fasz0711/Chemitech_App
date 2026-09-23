/// <summary>
/// Clase con la que se entró a una escena de clase. Es el equivalente de PlayContext
/// para universos: lo llena MisClasesScene antes de cargar la escena siguiente, y lo
/// leen ClaseEsperaScene y las escenas de clase.
///
/// No se persiste: si la app se cierra a mitad de una clase, el alumno vuelve a entrar
/// desde la lista. El estado real de la clase vive en el servidor, no aquí.
/// </summary>
public static class ClassContext
{
    public static string ClassId   = "";
    public static string ClassName = "";

    public static bool HasClass => !string.IsNullOrEmpty(ClassId);

    public static void Set(string id, string name)
    {
        ClassId   = id   ?? "";
        ClassName = name ?? "";
    }

    public static void Clear()
    {
        ClassId   = "";
        ClassName = "";
    }
}
