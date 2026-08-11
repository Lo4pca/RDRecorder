using HarmonyLib;
using UnityEngine;
using RDRecorder.Config;

namespace RDRecorder.Core;

public static class TimeMockManager
{
    private static double _mockDspTime;
    private static bool _isMocking = false;

    // Called by RecorderController when recording starts
    public static void StartMocking()
    {
        if (_isMocking) return;
        
        // Capture the real dspTime as our starting point to prevent logic jumps
        _mockDspTime = AudioSettings.dspTime;
        _isMocking = true;
        
        Plugin.LogInfo($"Time mocking started. Base dspTime initialized at: {_mockDspTime}");
    }

    // Called by RecorderController when recording stops
    public static void StopMocking()
    {
        if (!_isMocking) return;
        
        _isMocking = false;
        Plugin.LogInfo("Time mocking stopped. Restoring original dspTime flow.");
    }

    // Called by RecorderController after a frame has been successfully pushed to the encoder pipeline
    public static void AdvanceFrame()
    {
        if (!_isMocking) return;

        // Calculate the exact time delta for one frame based on TargetFPS
        _mockDspTime += 1.0 / PluginConfig.TargetFPS.Value;
    }

    // --- Harmony Patches ---

    [HarmonyPatch(typeof(AudioSettings), nameof(AudioSettings.dspTime), MethodType.Getter)]
    public static class AudioSettings_dspTime_Patch //For level events
    {
        public static bool Prefix(ref double __result)
        {
            if (_isMocking)
            {
                __result = _mockDspTime;
                return false; 
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(Time), nameof(Time.unscaledDeltaTime), MethodType.Getter)]
    public static class Time_unscaledDeltaTime_Patch //For background and character animations
    {
        static bool Prefix(ref float __result)
        {
            if (_isMocking)
            {
                __result = 1.0f / PluginConfig.TargetFPS.Value;
                return false; 
            }
            return true;
        }
    }
}