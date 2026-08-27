using HarmonyLib;
public static class LevelInfo
{
    public static string levelName;
}
[HarmonyPatch(typeof(HeartMonitor), nameof(HeartMonitor.Show))]
public static class HeartMonitor_Show_Patch
{
    static void Prefix(SelectableCharacter character,Difficulty difficulty)
    {
        LevelInfo.levelName=RDString.Get($"levelSelect.{character.levels[difficulty]}");
    }
}