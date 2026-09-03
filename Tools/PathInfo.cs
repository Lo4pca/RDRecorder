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
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string suffix = isAudio ? "m4a" : "mp4";
        string baseName = "Recording";

        // Defensive: this should only happen if GetOutputPath is ever called outside an
        // active level. Neither current caller does this - both encoders only call this
        // once a level's PlaySong event has already fired - but a generic fallback name
        // is safer than an unhandled NullReferenceException here.
        if (scnGame.instance != null && scnGame.instance.currentLevel != null)
        {
            RDLevelSettings settings = scnGame.instance.currentLevel.data.settings;
            baseName = settings.song.IsNullOrEmpty()
                ? (levelName ?? "Recording") // Builtin levels
                : $"{settings.song}_{settings.artist}_{settings.author}";
        }
        else
        {
            Plugin.LogWarn("GetOutputPath called with no active level; using a generic filename.");
        }

        string fileName = $"{SanitizeFileNameComponent(baseName)}_{timestamp}.{suffix}";
        return Path.Combine(PluginConfig.OutputFolder.Value, fileName);
    }

    // Song/artist/author metadata is free text and can contain characters that aren't
    // legal in filenames (e.g. a colon in a song title), which would otherwise make
    // ffmpeg fail to open the output path.
    private static string SanitizeFileNameComponent(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }
        return input;
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