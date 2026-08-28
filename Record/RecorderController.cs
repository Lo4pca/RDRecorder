using HarmonyLib;
using UnityEngine;
using RDRecorder.Core;
using RDRecorder.Config;
using RDLevelEditor;
using System.Collections;

namespace RDRecorder.Record;

public class RecorderController : MonoBehaviour
{
    private static FrameCapturer _capturer;
    private static int _originalCaptureFramerate;
    static bool _has_started; //In case there are multiple PlaySong events
    static bool _has_ended;
    private void OnEnable()
    {
        Plugin.LogInfo("RecorderController enabled. Initializing capture pipeline...");
        // Start capturing frames. Reuse an existing component instead of unconditionally
        // AddComponent-ing a new one, in case a previous session's Destroy() call (which is
        // deferred to end-of-frame) hasn't actually removed the old one yet - mirrors the
        // TryGetComponent pattern GameManager already uses for its own subsystem controllers.
        if (gameObject.TryGetComponent<FFmpegEncoder>(out var existingEncoder))
        {
            existingEncoder.enabled = true;
        }
        else
        {
            gameObject.AddComponent<FFmpegEncoder>();
        }

        if (gameObject.TryGetComponent<FrameCapturer>(out var existingCapturer))
        {
            _capturer = existingCapturer;
            _capturer.enabled = true;
        }
        else
        {
            _capturer = gameObject.AddComponent<FrameCapturer>();
        }
        _has_started=false;
        _has_ended=false;
    }
    public static void BeginRecording()
    {
        Plugin.LogInfo("LevelEvent_PlaySong triggered. Starting time manipulation and capture loop...");

        // 1. Hijack Unity's engine time
        _originalCaptureFramerate = Time.captureFramerate;
        Time.captureFramerate = PluginConfig.TargetFPS.Value;

        // 2. Hijack the rhythm game's audio logic time
        TimeMockManager.StartMocking();

        // 3. Command the capturer to start pushing frames
        _capturer.BeginCapture();
        //Mute the game so that no shrill noise comes out when recording
        AudioListener.volume = 0;
    }

    private void OnDisable()
    {
        Plugin.LogInfo("RecorderController disabled. Restoring original time flow...");

        // 1. Stop capturing. Destroy() only marks a component for removal at the end of
        // the current frame - its OnDisable doesn't run synchronously. Explicitly setting
        // enabled = false first forces FrameCapturer/FFmpegEncoder to stop and flush
        // *immediately*, so we don't restore real time below while they're still running
        // for the rest of this frame on mocked time.
        if (_capturer != null)
        {
            _capturer.enabled = false;
            Destroy(_capturer);
        }
        var encoder = GetComponent<FFmpegEncoder>();
        if (encoder != null)
        {
            encoder.enabled = false;
            Destroy(encoder);
        }

        // 2. Restore the rhythm game's audio logic time
        TimeMockManager.StopMocking();

        // 3. Restore Unity's engine time back to realtime
        Time.captureFramerate = _originalCaptureFramerate;
        //The game would be super noisy when the real gameplay tries to catch up with the real dspTime
        //Hopefully 3s is enough for everything to sync
        StartCoroutine(RestoreVolumeDelayed());
    }
    IEnumerator RestoreVolumeDelayed()
    {
        yield return new WaitForSecondsRealtime(3f);
        AudioListener.volume = 1;
    }
    [HarmonyPatch(typeof(LevelEvent_PlaySong), nameof(LevelEvent_PlaySong.Run))]
    public static class LevelEvent_PlaySong_Run_Patch
    {
        static void Postfix()
        {
            if (!_has_started&&GameManager.Instance.CurrentState==AppState.Recording)
            {
                BeginRecording();
                _has_started=true;
            }
        }
    }
    [HarmonyPatch(typeof(LevelEvent_FinishLevel), nameof(LevelEvent_FinishLevel.Run))]
    public static class LevelEvent_FinishLevel_Run_Patch
    {
        static bool Prefix()
        {
            if (!_has_ended&&GameManager.Instance.CurrentState==AppState.Recording)
            {
                GameManager.Instance.StopRecording();
                _has_ended=true;
            }
            return true;
        }
    }
}