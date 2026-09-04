using HarmonyLib;
using UnityEngine;
using RDLevelEditor;
using RDRecorder.Core;
using RDRecorder.Tools;

namespace RDRecorder.Record.Audio;

public class AudioRecorderController : MonoBehaviour
{
    public static AudioRecorderController Instance { get; private set; }

    private bool _hasStarted;
    private bool _hasEnded;
    private AudioCapturer _capturer;
    private FFmpegAudioEncoder _encoder;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        Plugin.LogDebug("Audio recorder armed. Waiting for level start...");
        _hasStarted = false;
        _hasEnded = false;

        // The encoder has no scene dependency, so it's safe to create now. The
        // AudioListener/AudioCapturer lookup below is NOT done here on purpose - see
        // BeginRecording().
        if (!gameObject.TryGetComponent(out _encoder))
        {
            _encoder = gameObject.AddComponent<FFmpegAudioEncoder>();
        }
    }

    // Called once the level's PlaySong event has actually fired, so PathInfo.GetOutputPath
    // (used by the encoder) and the event filter (which needs a live level) can safely
    // assume a level is loaded. Deliberately NOT done in OnEnable(): arming can happen
    // before any level is loaded, e.g. from a menu.
    private void BeginRecording()
    {
        // Resolving the AudioListener here too - not just the encoder path - matters:
        // if armed from a menu, OnEnable() would find (and bind AudioCapturer to) the
        // MENU's listener. If the level then loads its own listener/camera, the menu's
        // gets torn down along with the rest of that scene, silently orphaning our
        // capturer - the encoder still launches fine, but OnAudioFilterRead never fires
        // again, so nothing ever gets enqueued. Looking this up now, after PlaySong,
        // guarantees we bind to the level's actual, stable listener instead.
        var listener = FindFirstObjectByType<AudioListener>();
        if (listener == null)
        {
            Plugin.LogError("No AudioListener found in the scene! Cannot capture audio.");
            GameManager.Instance.StopAudioRecording();
            return;
        }

        if (!listener.gameObject.TryGetComponent(out _capturer))
        {
            _capturer = listener.gameObject.AddComponent<AudioCapturer>();
        }
        _capturer.TargetEncoder = _encoder;
        _capturer.IsCapturing = false; // don't start capturing until the encoder is confirmed up

        if (!_encoder.BeginEncoding())
        {
            Plugin.LogWarn("Audio recording could not start because the encoder failed to initialize.");
            GameManager.Instance.StopAudioRecording();
            return;
        }

        Plugin.LogInfo("Audio recording started.");

        // Suppress heavy game rendering/logic the same way playback does, since we only
        // need the level's beats and audio, not its visuals.
        EventFilter.Instance.ToggleEventFilter(true);
        _capturer.IsCapturing = true;
    }

    private void OnDisable()
    {
        if (_capturer != null)
        {
            _capturer.IsCapturing = false;
            _capturer.enabled = false;
            Destroy(_capturer);
        }
        if (_encoder != null)
        {
            _encoder.enabled = false;
            Destroy(_encoder);
        }

        EventFilter.Instance.ToggleEventFilter(false);
    }

    [HarmonyPatch(typeof(LevelEvent_PlaySong), nameof(LevelEvent_PlaySong.Run))]
    public static class LevelEvent_PlaySong_AudioPatch
    {
        public static void Postfix()
        {
            if (Instance != null && !Instance._hasStarted && GameManager.Instance.CurrentState == AppState.AudioRecording)
            {
                Instance._hasStarted = true;
                Instance.BeginRecording();
            }
        }
    }

    // Video recording and playback both auto-stop when the level finishes; audio-only
    // recording previously had no equivalent, so it would just keep running through the
    // results screen and back to level select until manually stopped.
    [HarmonyPatch(typeof(LevelEvent_FinishLevel), nameof(LevelEvent_FinishLevel.Run))]
    public static class LevelEvent_FinishLevel_AudioPatch
    {
        static bool Prefix()
        {
            if (Instance != null && !Instance._hasEnded && GameManager.Instance.CurrentState == AppState.AudioRecording)
            {
                Instance._hasEnded = true;
                GameManager.Instance.StopAudioRecording();
            }
            return true;
        }
    }
}