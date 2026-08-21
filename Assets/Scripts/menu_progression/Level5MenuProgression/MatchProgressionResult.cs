using System;

[Serializable]
public class MatchProgressionResult
{
    public string ResultId { get; private set; }
    public int CharacterId { get; private set; }
    public float ExperienceGained { get; private set; }
    public bool Applied { get; set; }
    public bool Duplicate { get; set; }
    public string Message { get; set; }

    public MatchProgressionResult(string resultId, int characterId, float experienceGained)
    {
        ResultId = string.IsNullOrEmpty(resultId) ? Guid.NewGuid().ToString("N") : resultId;
        CharacterId = characterId;
        ExperienceGained = experienceGained;
    }
}
