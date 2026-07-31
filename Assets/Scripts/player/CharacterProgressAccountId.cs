public static class CharacterProgressAccountId
{
    public static string GetCurrent()
    {
        if (GameOptions.userid > 0)
        {
            return GameOptions.userid.ToString();
        }

        if (!string.IsNullOrWhiteSpace(GameOptions.userName))
        {
            return GameOptions.userName;
        }

        return "guest";
    }
}
