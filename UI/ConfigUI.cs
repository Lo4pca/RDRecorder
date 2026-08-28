using UnityEngine;
using UnityFileDialog;
using RDRecorder.Config;
using RDRecorder.Core;
using System.Collections;

namespace RDRecorder.UI;

public class ConfigUI : MonoBehaviour
{
    private bool _isWindowVisible = false;
    private Rect _windowRect = new(50, 50, 350, 200);
    private string _tempOutputFolder;
    private string _tempFps;

    private void Start()
    {
        RefreshTempVars();
    }

    private void Update()
    {
        if (Input.GetKeyDown(PluginConfig.MenuHotkey.Value))
        {
            _isWindowVisible = !_isWindowVisible;
            if (_isWindowVisible)
            {
                RefreshTempVars();
            }
        }
        if (Input.GetKeyDown(PluginConfig.RecordHotkey.Value))
        {
            ToggleRecording();
        }

        if (Input.GetKeyDown(PluginConfig.PlaybackHotkey.Value))
        {
            TogglePlayback();
        }
    }

    private void OnGUI()
    {
        if (!_isWindowVisible) return;
        _windowRect = GUILayout.Window(10086, _windowRect, DrawWindow, "RDRecorder");
    }

    private void DrawWindow(int windowID)
    {
        GUILayout.Space(10);

        // FFmpeg's output framerate and the mocked timeline are both locked in for the
        // duration of a recording session, and playback re-encodes nothing but still
        // reads TargetFPS for its own bookkeeping. Changing FPS mid-session would desync
        // audio/video, so the field is locked whenever we're not Idle.
        bool isIdle = GameManager.Instance.CurrentState == AppState.Idle;

        GUILayout.BeginHorizontal();
        GUILayout.Label("Target FPS:", GUILayout.Width(80));
        GUI.enabled = isIdle;
        _tempFps = GUILayout.TextField(_tempFps);
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (!isIdle)
        {
            GUILayout.Label("FPS is locked while recording or playing back.");
        }

        GUILayout.Space(10);

        GUILayout.Label("Output folder:");
        GUILayout.BeginHorizontal();
        _tempOutputFolder = GUILayout.TextField(_tempOutputFolder);
        
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            BrowseFolder();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(15);

        if (GUILayout.Button("Save configuration", GUILayout.Height(30)))
        {
            SaveConfig();
        }

        GUILayout.Space(15);
        
        GUILayout.BeginHorizontal();
        
        // Recording Button Logic
        bool isRecording = GameManager.Instance.CurrentState == AppState.Recording;
        string recBtnText = isRecording ? "Stop Recording" : "Start Recording";
        
        if (GUILayout.Button(recBtnText, GUILayout.Height(40)))
        {
            ToggleRecording();
        }
        
        // Playback Button Logic
        bool isPlaying = GameManager.Instance.CurrentState == AppState.PlayingBack;
        string playBtnText = isPlaying ? "Stop Playback" : "Play Recordings";
        
        if (GUILayout.Button(playBtnText, GUILayout.Height(40)))
        {
            TogglePlayback();
        }
        
        GUILayout.EndHorizontal();
        GUI.DragWindow();
    }
    private void BrowseFolder()
    {
        StartCoroutine(BrowseFolderCoroutine());
    }

    private IEnumerator BrowseFolderCoroutine()
    {
        string folder=FileBrowser.PickFolder(null,null,null,"Select Output Folder");
        if(string.IsNullOrEmpty(folder)) yield break;
        _tempOutputFolder=folder;
    }

    private void BrowseAndStartPlayback()
    {
        StartCoroutine(BrowseAndStartPlaybackCoroutine());
    }

    private IEnumerator BrowseAndStartPlaybackCoroutine()
    {
        string file=FileBrowser.PickFile(_tempOutputFolder,"mp4 videos",["mp4"],"Select a video to play");
        if(string.IsNullOrEmpty(file)) yield break;
        Plugin.LogInfo(file);
        _isWindowVisible = false;
        GameManager.Instance.TargetVideoPath = file;
        GameManager.Instance.StartPlayback();
    }
    private void ToggleRecording()
    {
        if (GameManager.Instance.CurrentState == AppState.Recording)
        {
            GameManager.Instance.StopRecording();
        }
        else
        {
            _isWindowVisible = false;
            GameManager.Instance.StartRecording();
        }
    }

    private void TogglePlayback()
    {
        if (GameManager.Instance.CurrentState == AppState.PlayingBack)
        {
            GameManager.Instance.StopPlayback();
        }
        else
        {
            BrowseAndStartPlayback();
        }
    }

    private void RefreshTempVars()
    {
        _tempOutputFolder = PluginConfig.OutputFolder.Value;
        _tempFps = PluginConfig.TargetFPS.Value.ToString();
    }

    private void SaveConfig()
    {
        bool isIdle = GameManager.Instance.CurrentState == AppState.Idle;

        if (isIdle)
        {
            if (int.TryParse(_tempFps, out int parsedFps) && parsedFps > 0)
            {
                PluginConfig.TargetFPS.Value = parsedFps;
            }
            else
            {
                Plugin.LogError($"Invalid FPS: '{_tempFps}', reset to default value.");
                _tempFps = PluginConfig.TargetFPS.Value.ToString();
            }
        }
        else
        {
            // The field was disabled, but guard here too and revert any edit that
            // could still have been queued (e.g. from a pasted value) so the UI never
            // shows a change that was silently dropped.
            _tempFps = PluginConfig.TargetFPS.Value.ToString();
        }

        PluginConfig.OutputFolder.Value = _tempOutputFolder;

        try
        {
            if (!System.IO.Directory.Exists(PluginConfig.OutputFolder.Value))
            {
                System.IO.Directory.CreateDirectory(PluginConfig.OutputFolder.Value);
                Plugin.LogInfo($"Folder created: {PluginConfig.OutputFolder.Value}");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.LogError($"Unable to create folder: {ex.Message}");
        }

        Plugin.LogInfo("Configuration saved.");
    }
}