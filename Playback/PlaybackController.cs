using UnityEngine;
using RDRecorder.Core;

namespace RDRecorder.Playback;

/// <summary>
/// Controls the overall playback flow.
/// Coordinates the scene optimizer and video renderer.
/// </summary>
public class PlaybackController : MonoBehaviour
{
    private VideoRenderer _videoRenderer;
    private void Awake()
    {
        // Initialize dependencies as sibling components and keep them disabled initially
        _videoRenderer = gameObject.AddComponent<VideoRenderer>();
        _videoRenderer.enabled = false;
    }

    private void OnEnable()
    {
        Plugin.LogInfo("Starting playback process.");

        // Enable optimization to suppress heavy game rendering and logic
        EventFilter.Instance.ToggleEventFilter(true);
        _videoRenderer.enabled = true;

        // VideoRenderer.OnEnable() runs synchronously above, so by this point it has
        // either fully started (IsActive == true) or bailed out (no recorded video found,
        // or the editor gameView couldn't be located). If it bailed, unwind everything we
        // just did instead of leaving the app stuck in a "PlayingBack" state with nothing
        // actually playing.
        if (!VideoRenderer.IsActive)
        {
            Plugin.LogWarn("Video renderer failed to start; aborting playback session.");
            GameManager.Instance.StopPlayback();
        }
    }

    private void OnDisable()
    {
        Plugin.LogInfo("Stopping playback.");
        
        // Disabling the component triggers OnDisable() in VideoRenderer, handling cleanup automatically
        _videoRenderer?.enabled = false;
        
        // Restore standard scene rendering and event execution
        EventFilter.Instance.ToggleEventFilter(false);
    }
}