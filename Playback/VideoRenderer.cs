using HarmonyLib;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using RDRecorder.Core;
using RDLevelEditor;

namespace RDRecorder.Playback;

public class VideoRenderer : MonoBehaviour
{
    public static VideoRenderer Instance { get; private set; }

    private GameObject _videoRoot;
    private VideoPlayer _videoPlayer;
    private RenderTexture _videoTexture;
    private bool _playbackMode;
    private bool _hasStarted;

    // Editor-specific tracking fields to restore original state on disable
    private RawImage _targetGameViewRawImage;
    private Texture _originalGameViewTexture;

    // Lets callers (PlaybackController) distinguish "enabled and actually playing" from a
    // silent no-op enable (invalid video path, no active level, or the editor gameView is
    // missing).
    public bool IsActive => _playbackMode;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        Plugin.LogDebug("VideoRenderer enabled. Initializing video playback...");
        string video = GameManager.Instance.TargetVideoPath;
        if (string.IsNullOrEmpty(video) || !File.Exists(video))
        {
            Plugin.LogError($"Invalid or missing video file path: {video}. Cannot start playback.");
            return;
        }

        // Defensive: GameManager.StartPlayback() already checks this before ever
        // enabling this component, but keep the check here too in case anything else
        // ever enables VideoRenderer directly.
        if (scnGame.instance == null || scnGame.instance.currentLevel == null)
        {
            Plugin.LogError("Cannot start playback: no level is currently active.");
            return;
        }

        scnGame.instance.SetEnabledCameras(false);
        bool isEditorMode = IsEditorScene();
        bool setupSucceeded = isEditorMode ? SetupEditorVideo(video) : SetupNormalPlayVideo(video);

        if (!setupSucceeded)
        {
            // Roll back the camera toggle we already applied above since we're bailing
            // out before actually entering playback mode.
            scnGame.instance.SetEnabledCameras(true);
            return;
        }

        _playbackMode = true;
        _hasStarted = false;
    }

    private void OnDisable()
    {
        // Only clean up if we actually entered playback mode - otherwise this fires (and
        // logs) on every failed-to-start attempt too, since OnDisable always runs when
        // the component is disabled regardless of whether OnEnable succeeded.
        if (!_playbackMode) return;

        Plugin.LogDebug("VideoRenderer disabled. Cleaning up video playback...");
        ExitPlayback();
    }

    private void ExitPlayback()
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

        // Explicitly clear cached references rather than relying on Unity's "destroyed
        // object compares equal to null" behavior to paper over it.
        _videoRoot = null;
        _videoPlayer = null;
        _videoTexture = null;
        _targetGameViewRawImage = null;
        _originalGameViewTexture = null;

        scnGame.instance?.SetEnabledCameras(true);
        _playbackMode = false;
    }

    private bool IsEditorScene()
    {
        return SceneManager.GetActiveScene().name == "scnEditor" || scnEditor.instance != null;
    }

    private bool SetupNormalPlayVideo(string videoPath)
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
        return true;
    }

    private bool SetupEditorVideo(string videoPath)
    {
        _targetGameViewRawImage = scnEditor.instance.gameView;
        if (_targetGameViewRawImage == null)
        {
            Plugin.LogError("Editor mode detected, but 'gameView' RawImage could not be found!");
            return false;
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
        return true;
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

    [HarmonyPatch(typeof(LevelEvent_PlaySong), nameof(LevelEvent_PlaySong.Run))]
    public static class LevelEvent_PlaySong_Run_Patch
    {
        static void Prefix()
        {
            if (Instance != null && Instance._playbackMode && !Instance._hasStarted)
            {
                Instance._hasStarted = true;
                Instance._videoPlayer.Play();
                Plugin.LogInfo($"Started playing video: {Instance._videoPlayer.url}");
            }
        }
    }

    // Cameras are restored when exiting playback mode; we need to show the rank screen
    // at the end of the level.
    [HarmonyPatch(typeof(LevelEvent_FinishLevel), nameof(LevelEvent_FinishLevel.Run))]
    public static class LevelEvent_FinishLevel_Run_Patch
    {
        static bool Prefix()
        {
            // Route through GameManager instead of calling ExitPlayback() directly, so a
            // level ending naturally during playback fully unwinds the whole session:
            // this disables PlaybackController -> disables VideoRenderer (still runs
            // ExitPlayback() via its own OnDisable) -> restores the filtered level events
            // -> resets GameManager's state back to Idle.
            if (Instance != null && Instance._playbackMode) GameManager.Instance.StopPlayback();
            return true;
        }
    }
}