using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RDRecorder.Core;
using RDRecorder.Config;
using RDRecorder.UI;
using UnityEngine;
using RDRecorder.Tools;

namespace RDRecorder;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private static new ManualLogSource Logger;

    public static void LogWarn(object message)
    {
        Logger.LogWarning(message);
    }

    public static void LogError(object message)
    {
        Logger.LogError(message);
    }

    public static void LogInfo(object message)
    {
        Logger.LogInfo(message);
    }

    // Granular, per-session-lifecycle traces (component armed/enabled/disabled, etc.)
    // that are useful when troubleshooting but too noisy to show by default. Uses
    // BepInEx's own Debug log level rather than a bespoke config toggle, since that's
    // the filtering mechanism BepInEx users already know how to control.
    public static void LogDebug(object message)
    {
        Logger.LogDebug(message);
    }

    void Awake()
    {
        Logger = base.Logger;
        
        PluginConfig.Init(Config);
        LogInfo("PluginConfig finished initializing.");

        GameObject coreObj = new("RDRecorder_Core");
        DontDestroyOnLoad(coreObj);
        coreObj.AddComponent<ConfigUI>();
        coreObj.AddComponent<GameManager>();
        coreObj.AddComponent<EventFilter>();

        Harmony harmony = new(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
    }
}