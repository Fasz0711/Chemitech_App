public static class SessionData
{
    public static string UserId   { get; private set; } = "";
    public static string Username { get; private set; } = "";
    public static string Email    { get; private set; } = "";

    public static bool IsLoggedIn => !string.IsNullOrEmpty(UserId);

    public static void SetSession(string userId, string username, string email)
    {
        UserId   = userId;
        Username = username;
        Email    = email;
    }

    public static void Clear()
    {
        UserId = Username = Email = "";
    }
}
