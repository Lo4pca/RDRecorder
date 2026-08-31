using System.Collections.Generic;
using RDLevelEditor;
using UnityEngine;
public class EventFilter:MonoBehaviour
{
    public static EventFilter Instance { get; private set; }
    bool isFilteredActive=false;
    List<LevelEvent_Base>[] originalEventsBackup;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    public void ToggleEventFilter(bool enableFilter)
    {
        if (scnGame.instance == null || scnGame.instance.currentLevel == null) return;
        
        LevelBase currentLevel = scnGame.instance.currentLevel;

        if (enableFilter && !isFilteredActive)
        {
            if (originalEventsBackup == null)
            {
                originalEventsBackup = new List<LevelEvent_Base>[currentLevel.levelEventsPerBar.Length];
                for (int i = 0; i < currentLevel.levelEventsPerBar.Length; i++)
                {
                    originalEventsBackup[i] = currentLevel.levelEventsPerBar[i];
                }
            }

            for (int i = 0; i < currentLevel.levelEventsPerBar.Length; i++)
            {
                currentLevel.levelEventsPerBar[i] = FilterEvents(currentLevel.levelEventsPerBar[i]);
            }
            
            isFilteredActive = true;
        }
        else if (!enableFilter && isFilteredActive)
        {
            if (originalEventsBackup != null)
            {
                for (int i = 0; i < currentLevel.levelEventsPerBar.Length; i++)
                {
                    currentLevel.levelEventsPerBar[i] = originalEventsBackup[i];
                }
            }
            originalEventsBackup = null;
            isFilteredActive = false;
        }
    }

    private List<LevelEvent_Base> FilterEvents(List<LevelEvent_Base> original)
    {
        List<LevelEvent_Base> filteredEvents = [];
        foreach (var ev in original)
        {
            if (ev is LevelEvent_AddClassicBeat ||
                ev is LevelEvent_AddOneshotBeat ||
                ev is LevelEvent_AddFreeTimeBeat ||
                ev is LevelEvent_SetBeatsPerMinute ||
                ev is LevelEvent_SetCrotchetsPerBar ||
                ev is LevelEvent_PlaySong ||
                ev is LevelEvent_SetCountingSound ||
                ev is LevelEvent_SetClapSounds ||
                ev is LevelEvent_SetHeartExplodeVolume ||
                ev is LevelEvent_SayReadyGetSetGo ||
                ev is LevelEvent_SetGameSound ||
                ev is LevelEvent_SetRowXs ||
                ev is LevelEvent_FinishLevel)
            {
                filteredEvents.Add(ev);
            }
        }
        return filteredEvents;
    }
}