using System;
using System.Collections.Generic;

[Serializable]
public class SelectedLoadout
{
    public string playerCharacterId;
    public string cheerleaderId;
    public string modeId;
    public string levelId;
    public List<string> cpuCharacterIds = new List<string>();
}
