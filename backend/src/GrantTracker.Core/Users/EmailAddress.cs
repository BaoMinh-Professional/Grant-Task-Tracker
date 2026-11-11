namespace GrantTracker.Core.Users;

public static class EmailAddress
{
    public static string Normalise(string email) => email.Trim().ToLowerInvariant();
}
