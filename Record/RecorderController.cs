using UnityEngine;
using RDRecorder.Core;
using RDRecorder.Config;

namespace RDRecorder.Record;

public class RecorderController : MonoBehaviour
{
    private FrameCapturer _capturer;
    private int _originalCaptureFramerate;

    private void OnEnable()
    {
        Plugin.LogInfo("RecorderController enabled. Initializing capture pipeline...");

        // 1. Hijack Unity's engine time (Animations, Physics, Update loops)
        // Setting captureFramerate forces Time.deltaTime to strictly equal (1.0 / TargetFPS)
        _originalCaptureFramerate = Time.captureFramerate;
        Time.captureFramerate = PluginConfig.TargetFPS.Value;

        // 2. Hijack the rhythm game's audio logic time
        TimeMockManager.StartMocking();

        // 3. Start capturing frames
        gameObject.AddComponent<FFmpegEncoder>();
        _capturer = gameObject.AddComponent<FrameCapturer>();
    }

    private void OnDisable()
    {
        Plugin.LogInfo("RecorderController disabled. Restoring original time flow...");

        // 1. Stop capturing
        if (_capturer != null) Destroy(_capturer);
        var encoder = GetComponent<FFmpegEncoder>();
        if (encoder != null) Destroy(encoder);

        // 2. Restore the rhythm game's audio logic time
        TimeMockManager.StopMocking();

        // 3. Restore Unity's engine time back to realtime
        Time.captureFramerate = _originalCaptureFramerate;
    }
}