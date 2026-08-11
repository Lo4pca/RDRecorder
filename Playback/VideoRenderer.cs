using HarmonyLib;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using RDRecorder.Config;
using RDLevelEditor;

namespace RDRecorder.Playback;

public class VideoRenderer : MonoBehaviour
{
    private static GameObject _videoRoot;
    private static VideoPlayer _videoPlayer;
    private static RenderTexture _videoTexture;
    private static bool _playback_mode=false;

    // Editor-specific tracking fields to restore original state on disable
    private static RawImage _targetGameViewRawImage;
    private static Texture _originalGameViewTexture;

    private void OnEnable()
    {
        Plugin.LogInfo("VideoRenderer enabled. Initializing video playback...");
        
        string latestVideo = GetLatestRecordedVideo();
        if (string.IsNullOrEmpty(latestVideo))
        {
            Plugin.LogError("No recorded video found in the output folder. Cannot start playback.");
            return;
        }

        scnGame.instance.SetEnabledCameras(false);
        bool isEditorMode = IsEditorScene();
        if (isEditorMode)
        {
            SetupEditorVideo(latestVideo);
        }
        else
        {
            SetupNormalPlayVideo(latestVideo);
        }
        _playback_mode=true;
    }

    private void OnDisable()
    {
        if(!_playback_mode) return;
        Plugin.LogInfo("VideoRenderer disabled. Cleaning up video playback...");
        ExitPlayback();
    }
    private static void ExitPlayback()
    {
        _videoPlayer?.Stop();

        // Restore editor RawImage texture if modified
        _targetGameViewRawImage?.texture = _originalGameViewTexture;

        if (_videoTexture != null)
        {
            _videoTexture.Release();
            Destroy(_videoTexture);
        }

        if (_videoRoot != null)
        {
            Destroy(_videoRoot);
        }
        scnGame.instance.SetEnabledCameras(true);
        _playback_mode=false;
    }

    private bool IsEditorScene()
    {
        return SceneManager.GetActiveScene().name == "scnEditor" || scnEditor.instance != null;
    }

    private string GetLatestRecordedVideo()
    {
        string folder = PluginConfig.OutputFolder.Value;
        if (!Directory.Exists(folder)) return null;

        var directory = new DirectoryInfo(folder);
        var latestFile = directory.GetFiles("*.mp4")
                                  .OrderByDescending(f => f.CreationTime)
                                  .FirstOrDefault();

        return latestFile?.FullName;
    }

    private void SetupNormalPlayVideo(string videoPath)
    {
        _videoRoot = new GameObject("RDRecorder_VideoUI");
        DontDestroyOnLoad(_videoRoot);

        Canvas canvas = _videoRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        CanvasScaler scaler = _videoRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(Screen.width, Screen.height);

        GameObject rawImageObj = new("VideoScreen");
        rawImageObj.transform.SetParent(_videoRoot.transform, false);
        
        RawImage rawImage = rawImageObj.AddComponent<RawImage>();
        RectTransform rect = rawImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        _videoTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        rawImage.texture = _videoTexture;

        SetupVideoPlayerComponent(videoPath);
    }

    private void SetupEditorVideo(string videoPath)
    {
        _targetGameViewRawImage = scnEditor.instance.gameView;
        if (_targetGameViewRawImage == null)
        {
            Plugin.LogError("Editor mode detected, but 'gameView' RawImage could not be found!");
            return;
        }

        // Cache original texture to restore later
        _originalGameViewTexture = _targetGameViewRawImage.texture;

        // Create texture matching the gameView dimensions or screen size fallback
        int texWidth = _targetGameViewRawImage.mainTexture ? _targetGameViewRawImage.mainTexture.width : Screen.width;
        int texHeight = _targetGameViewRawImage.mainTexture ? _targetGameViewRawImage.mainTexture.height : Screen.height;
        
        _videoTexture = new RenderTexture(texWidth, texHeight, 0, RenderTextureFormat.ARGB32);
        
        // Swap editor preview texture with our video texture
        _targetGameViewRawImage.texture = _videoTexture;

        // We can create a dummy root object to house the VideoPlayer component
        _videoRoot = new GameObject("RDRecorder_EditorVideoPlayer");
        DontDestroyOnLoad(_videoRoot);

        SetupVideoPlayerComponent(videoPath);
    }

    private void SetupVideoPlayerComponent(string videoPath)
    {
        _videoPlayer = _videoRoot.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _videoTexture;
        _videoPlayer.source = VideoSource.Url;
        _videoPlayer.url = videoPath;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // Game handles native audio sync
    }

    [HarmonyPatch(typeof(LevelEvent_PlaySong), nameof(LevelEvent_PlaySong.Run))] //This event is always at the beginning of every level, and there is only one.
    public static class LevelEvent_PlaySong_Run_Patch
    {
        static void Postfix()
        {
            if (_playback_mode)
            {
                _videoPlayer.Play();
                Plugin.LogInfo($"Started playing video: {_videoPlayer.url}");
            }
        }
    }
    [HarmonyPatch(typeof(LevelEvent_FinishLevel), nameof(LevelEvent_FinishLevel.Run))]
    public static class LevelEvent_FinishLevel_Run_Patch
    {
        static bool Prefix()
        {
            if (_playback_mode) ExitPlayback(); //Cameras are restored when exiting playback mode, we need to show the rank screen at the end of the level. 
            return true;
        }
    }
}