public static class RegistrationData
{
    public static string Email    { get; set; } = "";
    public static string Password { get; set; } = "";
    public static string Username { get; set; } = "";

    public static void Clear()
    {
        Email = Password = Username = "";
    }
}
