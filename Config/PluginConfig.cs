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
    public static ConfigEntry<KeyCode> RecordHotkey { get; private set; }
    public static ConfigEntry<KeyCode> PlaybackHotkey { get; private set; }
    public static ConfigEntry<KeyCode> AudioRecordHotkey { get; private set; }

    private static string _defaultOutputPath;

    public static void Init(ConfigFile config)
    {
        MenuHotkey = config.Bind(
            "UI", 
            "MenuHotkey", 
            KeyCode.F2, 
            "Shortcut key to open/close the configuration panel."
        );

        RecordHotkey = config.Bind(
            "UI", 
            "RecordHotkey", 
            KeyCode.F3, 
            "Shortcut key to start/stop recording."
        );

        PlaybackHotkey = config.Bind(
            "UI", 
            "PlaybackHotkey", 
            KeyCode.F4, 
            "Shortcut key to start/stop playback."
        );

        AudioRecordHotkey = config.Bind(
            "UI", 
            "AudioRecordHotkey", 
            KeyCode.F5, 
            "Shortcut key to start/stop recording audio."
        );

        TargetFPS = config.Bind(
            "Recording", 
            "TargetFPS", 
            60, 
            "Target FPS for recording."
        );

        _defaultOutputPath = Path.Combine(Application.dataPath, "../Recordings");
        OutputFolder = config.Bind(
            "Recording", 
            "OutputFolder", 
            _defaultOutputPath, 
            "Output folder to recorded videos."
        );

        if (!TryEnsureOutputFolder(out string error))
        {
            // A bad path saved in the config file (invalid characters, no write permission,
            // a drive that no longer exists, etc.) would otherwise throw here and take the
            // whole plugin down during Awake(), before Harmony even patches anything. Fall
            // back to the default folder so the plugin still loads in a working state.
            Plugin.LogError($"Could not create output folder '{OutputFolder.Value}': {error}. Falling back to default: {_defaultOutputPath}");
            OutputFolder.Value = _defaultOutputPath;
            Directory.CreateDirectory(_defaultOutputPath);
        }
    }

    // Shared by Init() (which falls back to the default path on failure) and ConfigUI's
    // manual "Save configuration" button (which just reports the error to the user).
    public static bool TryEnsureOutputFolder(out string error)
    {
        try
        {
            // Directory.CreateDirectory is already a no-op if the folder exists, so
            // there's no need to check Directory.Exists first.
            Directory.CreateDirectory(OutputFolder.Value);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}