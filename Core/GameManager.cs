using System.IO;
using UnityEngine;
using RDRecorder.Record;
using RDRecorder.Record.Audio;
using RDRecorder.Playback;

namespace RDRecorder.Core;

// Defines the current operating mode of the plugin
public enum AppState
{
    Idle,
    Recording,
    PlayingBack,
    AudioRecording
}

public class GameManager : MonoBehaviour
{
    // Singleton instance for easy access from UI
    public static GameManager Instance { get; private set; }

    // Read-only state property
    public AppState CurrentState { get; private set; } = AppState.Idle;

    // Set only via StartPlayback(videoPath), so there's exactly one place that can put
    // the plugin into PlayingBack with a path, instead of relying on callers to set this
    // field themselves right before calling StartPlayback().
    public string TargetVideoPath { get; private set; }

    private void Awake()
    {
        // Enforce Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void StartRecording()
    {
        if (CurrentState == AppState.Recording)
        {
            Plugin.LogWarn("Already in recording state.");
            return;
        }
        if (CurrentState != AppState.Idle)
        {
            Plugin.LogWarn($"Cannot start recording while {CurrentState}. Stop the current session first.");
            return;
        }

        Plugin.LogInfo("Recording armed. Capture will begin once the level starts.");
        CurrentState = AppState.Recording;

        // Dynamically add the Recorder subsystem to this GameObject if it isn't already
        // attached (its own Awake() sets RecorderController.Instance).
        if (!gameObject.TryGetComponent<RecorderController>(out _))
        {
            gameObject.AddComponent<RecorderController>();
        }
        RecorderController.Instance.enabled = true;
    }

    public void StopRecording()
    {
        if (CurrentState != AppState.Recording) return;

        Plugin.LogInfo("Stopping recording session...");

        RecorderController.Instance?.enabled = false;

        CurrentState = AppState.Idle;
    }

    public void StartPlayback(string videoPath)
    {
        if (CurrentState == AppState.PlayingBack)
        {
            Plugin.LogWarn("Already in playback state.");
            return;
        }
        if (CurrentState != AppState.Idle)
        {
            Plugin.LogWarn($"Cannot start playback while {CurrentState}. Stop the current session first.");
            return;
        }
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
        {
            Plugin.LogError($"Cannot start playback: '{videoPath}' does not exist.");
            return;
        }
        // Playback (unlike recording) starts immediately rather than waiting for a level
        // event, so unlike RecorderController it can't defer this check - it needs a
        // level to already be active.
        if (scnGame.instance == null || scnGame.instance.currentLevel == null)
        {
            Plugin.LogError("Cannot start playback: no level is currently active. Start playback while in a level.");
            return;
        }

        Plugin.LogInfo("Playback started.");
        TargetVideoPath = videoPath;
        CurrentState = AppState.PlayingBack;

        // Dynamically add the Playback subsystem to this GameObject
        if (!gameObject.TryGetComponent<PlaybackController>(out _))
        {
            gameObject.AddComponent<PlaybackController>();
        }
        PlaybackController.Instance.enabled = true;
    }

    public void StopPlayback()
    {
        if (CurrentState != AppState.PlayingBack) return;

        Plugin.LogInfo("Stopping playback and restoring original game state...");

        PlaybackController.Instance?.enabled = false;

        CurrentState = AppState.Idle;
    }

    public void StartAudioRecording()
    {
        if (CurrentState == AppState.AudioRecording)
        {
            Plugin.LogWarn("Already in audio recording state.");
            return;
        }
        if (CurrentState != AppState.Idle)
        {
            Plugin.LogWarn($"Cannot start audio recording while {CurrentState}. Stop the current session first.");
            return;
        }

        Plugin.LogInfo("Audio recording armed. Capture will begin once the level starts.");
        CurrentState = AppState.AudioRecording;

        if (!gameObject.TryGetComponent<AudioRecorderController>(out _))
        {
            gameObject.AddComponent<AudioRecorderController>();
        }
        AudioRecorderController.Instance.enabled = true;
    }

    public void StopAudioRecording()
    {
        if (CurrentState != AppState.AudioRecording) return;

        Plugin.LogInfo("Stopping audio recording session...");
        AudioRecorderController.Instance?.enabled = false;
        CurrentState = AppState.Idle;
    }
}