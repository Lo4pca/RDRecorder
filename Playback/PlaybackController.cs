using UnityEngine;
using RDLevelEditor;
using System.Collections.Generic;
using RDRecorder.Core;

namespace RDRecorder.Playback;

/// <summary>
/// Controls the overall playback flow.
/// Coordinates the scene optimizer and video renderer.
/// </summary>
public class PlaybackController : MonoBehaviour
{
    private VideoRenderer _videoRenderer;
    private List<LevelEvent_Base>[] originalEventsBackup;
    private bool isPlaybackModeActive = false;

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
        TogglePlaybackMode(true);
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
        TogglePlaybackMode(false);
    }
    private void TogglePlaybackMode(bool enablePlayback)
    {
        if (scnGame.instance == null || scnGame.instance.currentLevel == null) return;
        
        LevelBase currentLevel = scnGame.instance.currentLevel;

        if (enablePlayback && !isPlaybackModeActive)
        {
            // 1. Backup the original array if we haven't already
            if (originalEventsBackup == null)
            {
                originalEventsBackup = new List<LevelEvent_Base>[currentLevel.levelEventsPerBar.Length];
                for (int i = 0; i < currentLevel.levelEventsPerBar.Length; i++)
                {
                    // Storing the original list references
                    originalEventsBackup[i] = currentLevel.levelEventsPerBar[i];
                }
            }

            // 2. Replace with Playback Mode events
            for (int i = 0; i < currentLevel.levelEventsPerBar.Length; i++)
            {
                currentLevel.levelEventsPerBar[i] = FilterEvents(currentLevel.levelEventsPerBar[i]);
            }
            
            isPlaybackModeActive = true;
        }
        else if (!enablePlayback && isPlaybackModeActive)
        {
            // 3. Restore Normal Mode by reassigning the original lists
            if (originalEventsBackup != null)
            {
                for (int i = 0; i < currentLevel.levelEventsPerBar.Length; i++)
                {
                    currentLevel.levelEventsPerBar[i] = originalEventsBackup[i];
                }
            }
            
            // Clear the cached backup so the *next* playback session captures a fresh
            // snapshot of whichever level is current at that time.
            originalEventsBackup = null;
            isPlaybackModeActive = false;
        }
    }
    private List<LevelEvent_Base> FilterEvents(List<LevelEvent_Base> original)
    {
        List<LevelEvent_Base> filteredEvents = [];
        foreach (var ev in original)
        {
            // Retain beat generation and rhythm control events
            if (ev is LevelEvent_AddClassicBeat ||
                ev is LevelEvent_AddOneshotBeat ||
                ev is LevelEvent_AddFreeTimeBeat ||
                ev is LevelEvent_SetBeatsPerMinute ||
                ev is LevelEvent_SetCrotchetsPerBar||
                ev is LevelEvent_PlaySong||
                ev is LevelEvent_SetCountingSound||
                ev is LevelEvent_SetClapSounds||
                ev is LevelEvent_SetHeartExplodeVolume||
                ev is LevelEvent_SayReadyGetSetGo||
                ev is LevelEvent_SetGameSound||
                ev is LevelEvent_SetRowXs||
                ev is LevelEvent_FinishLevel)
            {
                filteredEvents.Add(ev);
            }
        }
        return filteredEvents;
    }
}