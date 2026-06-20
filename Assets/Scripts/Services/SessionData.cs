public static class SessionData
{
    public static string UserId   { get; private set; } = "";
    public static string Username { get; private set; } = "";
    public static string Email    { get; private set; } = "";

    public static string AccessToken  { get; private set; } = "";
    public static string RefreshToken { get; private set; } = "";
    public static string TokenType    { get; private set; } = "";
    public static int    ExpiresIn    { get; private set; } = 0;

    public static bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);

    public static void SetSession(string userId, string username, string email)
    {
        UserId   = userId;
        Username = username;
        Email    = email;
    }

    public static void SetTokens(string accessToken, string refreshToken, string tokenType, int expiresIn)
    {
        AccessToken  = accessToken;
        RefreshToken = refreshToken;
        TokenType    = tokenType;
        ExpiresIn    = expiresIn;
    }

    public static void Clear()
    {
        UserId = Username = Email = "";
        AccessToken = RefreshToken = TokenType = "";
        ExpiresIn = 0;
    }
}
