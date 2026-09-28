using Level5.Core;

public static class CharacterProgressAccountId
{
    public static string GetCurrent()
    {
        return Resolve(LocalAccountIdentity.UserId, LocalAccountIdentity.UserName);
    }

    /// <summary>
    /// Pure account-scope resolution rule, usable for any candidate profile (not only the currently
    /// selected one) without mutating <see cref="LocalAccountIdentity"/>. <see cref="GetCurrent"/>
    /// delegates here so there is exactly one mapping: a positive userId always wins as an invariant
    /// decimal string, otherwise a non-empty userName, otherwise the literal "guest" fallback scope.
    /// </summary>
    public static string Resolve(int userId, string userName)
    {
        if (userId > 0)
        {
            return userId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName;
        }

        return "guest";
    }
}
