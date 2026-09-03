using UnityEngine;
using RDRecorder.Core;
using RDRecorder.Tools;

namespace RDRecorder.Playback;

/// <summary>
/// Controls the overall playback flow.
/// Coordinates the scene optimizer and video renderer.
/// </summary>
public class PlaybackController : MonoBehaviour
{
    public static PlaybackController Instance { get; private set; }

    private VideoRenderer _videoRenderer;

    private void Awake()
    {
        Instance = this;

        // Initialize dependencies as sibling components and keep them disabled initially
        _videoRenderer = gameObject.AddComponent<VideoRenderer>();
        _videoRenderer.enabled = false;
    }

    private void OnEnable()
    {
        Plugin.LogDebug("Starting playback process.");

        // Enable optimization to suppress heavy game rendering and logic
        EventFilter.Instance.ToggleEventFilter(true);
        _videoRenderer.enabled = true;

        // VideoRenderer.OnEnable() runs synchronously above, so by this point it has
        // either fully started (IsActive == true) or bailed out (invalid video path, no
        // active level, or the editor gameView couldn't be located). If it bailed, unwind
        // everything we just did instead of leaving the app stuck in a "PlayingBack"
        // state with nothing actually playing.
        if (!_videoRenderer.IsActive)
        {
            Plugin.LogWarn("Video renderer failed to start; aborting playback session.");
            GameManager.Instance.StopPlayback();
        }
    }

    private void OnDisable()
    {
        Plugin.LogDebug("Stopping playback.");
        
        // Disabling the component triggers OnDisable() in VideoRenderer, handling cleanup automatically
        _videoRenderer?.enabled = false;
        
        // Restore standard scene rendering and event execution
        EventFilter.Instance.ToggleEventFilter(false);
    }
}