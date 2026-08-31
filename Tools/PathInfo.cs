using System;
using System.IO;
using HarmonyLib;
using RDLevelEditor;
using RDRecorder.Config;

namespace RDRecorder.Tools;
public static class PathInfo
{
    public static string levelName;
    public static string GetOutputPath(bool isAudio)
    {
        string outputPath;
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        RDLevelSettings settings=scnGame.instance.currentLevel.data.settings;
        string suffix=isAudio?"m4a":"mp4";
        if (settings.song.IsNullOrEmpty()) //Builtin levels
        {
            outputPath = Path.Combine(PluginConfig.OutputFolder.Value, $"{levelName}_{timestamp}.{suffix}");
        }
        else
        {
            outputPath = Path.Combine(PluginConfig.OutputFolder.Value, $"{settings.song}_{settings.artist}_{settings.author}_{timestamp}.{suffix}");
        }
        return outputPath;
    }
}
[HarmonyPatch(typeof(HeartMonitor), nameof(HeartMonitor.Show))]
public static class HeartMonitor_Show_Patch
{
    static void Prefix(SelectableCharacter character,Difficulty difficulty)
    {
        PathInfo.levelName=RDString.Get($"levelSelect.{character.levels[difficulty]}");
    }
}