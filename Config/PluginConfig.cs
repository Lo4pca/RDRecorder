using System;
using BepInEx.Configuration;
using System.IO;
using UnityEngine;

namespace RDRecorder.Config;

public static class PluginConfig
{
    public static ConfigEntry<int> TargetFPS { get; private set; }
    public static ConfigEntry<string> OutputFolder { get; private set; }
    public static ConfigEntry<KeyCode> MenuHotkey { get; private set; }

    public static void Init(ConfigFile config)
    {
        MenuHotkey = config.Bind(
            "UI", 
            "MenuHotkey", 
            KeyCode.F2, 
            "Shortcut key to open/close the configuration panel."
        );

        TargetFPS = config.Bind(
            "Recording", 
            "TargetFPS", 
            60, 
            "Target FPS for recording."
        );

        string defaultPath = Path.Combine(Application.dataPath, "../Recordings");
        OutputFolder = config.Bind(
            "Recording", 
            "OutputFolder", 
            defaultPath, 
            "Output folder to recorded videos."
        );

        try
        {
            // Directory.CreateDirectory is already a no-op if the folder exists, so there's
            // no need to check Directory.Exists first.
            Directory.CreateDirectory(OutputFolder.Value);
        }
        catch (Exception ex)
        {
            // A bad path saved in the config file (invalid characters, no write permission,
            // a drive that no longer exists, etc.) would otherwise throw here and take the
            // whole plugin down during Awake(), before Harmony even patches anything. Fall
            // back to the default folder so the plugin still loads in a working state.
            Plugin.LogError($"Could not create output folder '{OutputFolder.Value}': {ex.Message}. Falling back to default: {defaultPath}");
            OutputFolder.Value = defaultPath;
            Directory.CreateDirectory(defaultPath);
        }
    }
}