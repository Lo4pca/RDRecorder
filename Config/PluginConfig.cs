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

        if (!Directory.Exists(OutputFolder.Value))
        {
            Directory.CreateDirectory(OutputFolder.Value);
        }
    }
}