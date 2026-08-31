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
    public string TargetVideoPath;

    // References to our subsystem controllers
    private RecorderController _recorderController;
    private PlaybackController _playbackController;
    private AudioRecorderController _audioRecorderController;

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

        if (CurrentState == AppState.PlayingBack)
        {
            Plugin.LogWarn("Cannot start recording while playing back. Stop playback first.");
            return;
        }

        Plugin.LogInfo("Starting recording session...");
        CurrentState = AppState.Recording;

        // Dynamically add the Recorder subsystem to this GameObject
        if (!gameObject.TryGetComponent<RecorderController>(out var existing))
        {
            _recorderController = gameObject.AddComponent<RecorderController>();
        }
        else
        {
            _recorderController = existing;
        }
        _recorderController.enabled = true;
    }

    public void StopRecording()
    {
        if (CurrentState != AppState.Recording) return;

        Plugin.LogInfo("Stopping recording session...");
        
        _recorderController?.enabled = false;

        CurrentState = AppState.Idle;
    }

    public void StartPlayback()
    {
        if (CurrentState == AppState.PlayingBack)
        {
            Plugin.LogWarn("Already in playback state.");
            return;
        }

        if (CurrentState == AppState.Recording)
        {
            Plugin.LogWarn("Cannot start playback while recording. Stop recording first.");
            return;
        }

        Plugin.LogInfo("Starting pre-rendered playback...");
        CurrentState = AppState.PlayingBack;

        // Dynamically add the Playback subsystem to this GameObject
        if (!gameObject.TryGetComponent<PlaybackController>(out var existing))
        {
            _playbackController = gameObject.AddComponent<PlaybackController>();
        }
        else
        {
            _playbackController = existing;
        }
        _playbackController.enabled = true;
    }

    public void StopPlayback()
    {
        if (CurrentState != AppState.PlayingBack) return;

        Plugin.LogInfo("Stopping playback and restoring original game state...");

        _playbackController?.enabled = false;

        CurrentState = AppState.Idle;
    }
    public void StartAudioRecording()
    {
        if (CurrentState != AppState.Idle)
        {
            Plugin.LogWarn("Cannot start audio recording unless idle.");
            return;
        }

        Plugin.LogInfo("Starting audio recording session...");
        CurrentState = AppState.AudioRecording;

        if (!gameObject.TryGetComponent<AudioRecorderController>(out var existing))
        {
            _audioRecorderController = gameObject.AddComponent<AudioRecorderController>();
        }
        else
        {
            _audioRecorderController = existing;
        }
        _audioRecorderController.enabled = true;
    }

    public void StopAudioRecording()
    {
        if (CurrentState != AppState.AudioRecording) return;

        Plugin.LogInfo("Stopping audio recording session...");
        _audioRecorderController?.enabled = false;
        CurrentState = AppState.Idle;
    }
}