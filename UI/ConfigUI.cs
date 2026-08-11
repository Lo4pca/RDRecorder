using UnityEngine;
using RDRecorder.Config;
using RDRecorder.Core;

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
    }

    private void OnGUI()
    {
        if (!_isWindowVisible) return;
        _windowRect = GUILayout.Window(10086, _windowRect, DrawWindow, "RDRecorder");
    }

    private void DrawWindow(int windowID)
    {
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Target FPS:", GUILayout.Width(80));
        _tempFps = GUILayout.TextField(_tempFps);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.Label("Output folder:");
        _tempOutputFolder = GUILayout.TextField(_tempOutputFolder);

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
            if (isRecording)
            {
                GameManager.Instance.StopRecording();
            }
            else
            {
                GameManager.Instance.StartRecording();
            }
        }
        
        // Playback Button Logic
        bool isPlaying = GameManager.Instance.CurrentState == AppState.PlayingBack;
        string playBtnText = isPlaying ? "Stop Playback" : "Play Recordings";
        
        if (GUILayout.Button(playBtnText, GUILayout.Height(40)))
        {
            if (isPlaying)
            {
                GameManager.Instance.StopPlayback();
            }
            else
            {
                GameManager.Instance.StartPlayback();
            }
        }
        
        GUILayout.EndHorizontal();
        GUI.DragWindow();
    }

    private void RefreshTempVars()
    {
        _tempOutputFolder = PluginConfig.OutputFolder.Value;
        _tempFps = PluginConfig.TargetFPS.Value.ToString();
    }

    private void SaveConfig()
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