using HarmonyLib;
using UnityEngine;
using RDRecorder.Core;
using RDRecorder.Config;
using RDLevelEditor;
using System.Collections;

namespace RDRecorder.Record;

public class RecorderController : MonoBehaviour
{
    public static RecorderController Instance { get; private set; }

    private FrameCapturer _capturer;
    private int _originalCaptureFramerate;
    private bool _hasStarted; // In case there are multiple PlaySong events
    private bool _hasEnded;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        Plugin.LogDebug("RecorderController armed. Waiting for level start...");

        // Reuse an existing component instead of unconditionally AddComponent-ing a new
        // one, in case a previous session's Destroy() call (deferred to end-of-frame)
        // hasn't actually removed the old one yet - mirrors the TryGetComponent pattern
        // GameManager uses for its own subsystem controllers.
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

        _hasStarted = false;
        _hasEnded = false;
    }

    // Called once the level's PlaySong event actually fires, so PathInfo.GetOutputPath
    // and the encoder can safely assume a level is loaded. Deliberately NOT done in
    // OnEnable(): arming can happen before any level is loaded (e.g. from a menu), and
    // PathInfo.GetOutputPath reads the current level's metadata.
    private void BeginRecording()
    {
        if (!FFmpegEncoder.Instance.BeginEncoding())
        {
            Plugin.LogWarn("Recording could not start because the encoder failed to initialize.");
            GameManager.Instance.StopRecording();
            return;
        }

        Plugin.LogInfo("Recording started.");

        // 1. Hijack Unity's engine time
        _originalCaptureFramerate = Time.captureFramerate;
        Time.captureFramerate = PluginConfig.TargetFPS.Value;

        // 2. Hijack the rhythm game's audio logic time
        TimeMockManager.StartMocking();

        // 3. Command the capturer to start pushing frames
        _capturer.BeginCapture();

        // Mute the game so that no shrill noise comes out when recording
        AudioListener.volume = 0;
    }

    private void OnDisable()
    {
        Plugin.LogDebug("RecorderController disabled. Restoring original time flow...");

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

        // The game would be super noisy when the real gameplay tries to catch up with
        // the real dspTime. Hopefully 3s is enough for everything to sync.
        StartCoroutine(RestoreVolumeDelayed());
    }

    private IEnumerator RestoreVolumeDelayed()
    {
        yield return new WaitForSecondsRealtime(3f);
        AudioListener.volume = 1;
    }

    [HarmonyPatch(typeof(LevelEvent_PlaySong), nameof(LevelEvent_PlaySong.Run))]
    public static class LevelEvent_PlaySong_Run_Patch
    {
        static void Prefix()
        {
            if (Instance != null && !Instance._hasStarted && GameManager.Instance.CurrentState == AppState.Recording)
            {
                Instance._hasStarted = true;
                Instance.BeginRecording();
            }
        }
    }

    [HarmonyPatch(typeof(LevelEvent_FinishLevel), nameof(LevelEvent_FinishLevel.Run))]
    public static class LevelEvent_FinishLevel_Run_Patch
    {
        static bool Prefix()
        {
            if (Instance != null && !Instance._hasEnded && GameManager.Instance.CurrentState == AppState.Recording)
            {
                Instance._hasEnded = true;
                GameManager.Instance.StopRecording();
            }
            return true;
        }
    }
}