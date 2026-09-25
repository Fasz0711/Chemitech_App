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

    /// <summary>Por qué se salió de la clase, para que la siguiente pantalla lo explique.
    /// Al borrar una clase, la cuenta del alumno desaparece con ella: su sondeo recibe 401
    /// y ApiManager lo manda al login. Es correcto, pero sin esto aparecería ahí sin
    /// ninguna explicación, como si la app hubiera fallado.
    /// Sobrevive al cambio de escena porque es estático; lo consume quien lo muestra.</summary>
    public static string ExitNotice = "";

    public static string TakeExitNotice()
    {
        string n = ExitNotice;
        ExitNotice = "";
        return n;
    }

    public static void Clear()
    {
        ClassId   = "";
        ClassName = "";
    }
}
