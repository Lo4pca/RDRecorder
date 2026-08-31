using HarmonyLib;
using UnityEngine;
using RDLevelEditor;
using RDRecorder.Core;

namespace RDRecorder.Record.Audio;

public class AudioRecorderController : MonoBehaviour
{
    public static bool IsRecordingActive { get; private set; }
    static bool _has_started;
    private static AudioCapturer _capturer;
    private FFmpegAudioEncoder _encoder;

    private void OnEnable()
    {
        Plugin.LogInfo("Starting audio-only recording process. Waiting for level start...");
        IsRecordingActive = false;
        
        EventFilter.Instance.ToggleEventFilter(true);

        var listener = FindFirstObjectByType<AudioListener>();
        if (listener == null)
        {
            Plugin.LogError("No AudioListener found in the scene! Cannot capture audio.");
            GameManager.Instance.StopAudioRecording();
            return;
        }

        _encoder = gameObject.AddComponent<FFmpegAudioEncoder>();
        _capturer = listener.gameObject.AddComponent<AudioCapturer>();
        _capturer.TargetEncoder = _encoder;
        
        // Ensure it doesn't start capturing desktop/menu noise immediately
        _capturer.IsCapturing = false; 
    }

    public static void BeginRecording()
    {
        if (IsRecordingActive) return;
        
        Plugin.LogInfo("LevelEvent_PlaySong triggered. Starting audio capture stream...");
        IsRecordingActive = true;
        
        _capturer?.IsCapturing = true;
    }

    private void OnDisable()
    {
        IsRecordingActive = false;

        if (_capturer != null)
        {
            _capturer.enabled=false;
            Destroy(_capturer);
        }
        if (_encoder != null)
        {
            _encoder.enabled=false;
            Destroy(_encoder);
        }

        EventFilter.Instance.ToggleEventFilter(false);
    }
    [HarmonyPatch(typeof(LevelEvent_PlaySong), nameof(LevelEvent_PlaySong.Run))]
    public static class LevelEvent_PlaySong_AudioPatch
    {
        public static void Postfix()
        {
            if (!_has_started && !IsRecordingActive)
            {
                _has_started=true;
                BeginRecording();
            }
        }
    }
}