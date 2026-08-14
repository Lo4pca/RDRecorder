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

        // 3. Start capturing frames. Reuse an existing component instead of unconditionally
        // AddComponent-ing a new one, in case a previous session's Destroy() call (which is
        // deferred to end-of-frame) hasn't actually removed the old one yet - mirrors the
        // TryGetComponent pattern GameManager already uses for its own subsystem controllers.
        if (gameObject.TryGetComponent<FFmpegEncoder>(out var existingEncoder))
        {
            existingEncoder.enabled = true;
        }
        else
        {
            gameObject.AddComponent<FFmpegEncoder>();
        }

        if (gameObject.TryGetComponent<FrameCapturer>(out var existingCapturer))
        {
            _capturer = existingCapturer;
            _capturer.enabled = true;
        }
        else
        {
            _capturer = gameObject.AddComponent<FrameCapturer>();
        }
    }

    private void OnDisable()
    {
        Plugin.LogInfo("RecorderController disabled. Restoring original time flow...");

        // 1. Stop capturing. Destroy() only marks a component for removal at the end of
        // the current frame - its OnDisable doesn't run synchronously. Explicitly setting
        // enabled = false first forces FrameCapturer/FFmpegEncoder to stop and flush
        // *immediately*, so we don't restore real time below while they're still running
        // for the rest of this frame on mocked time.
        if (_capturer != null)
        {
            _capturer.enabled = false;
            Destroy(_capturer);
        }
        var encoder = GetComponent<FFmpegEncoder>();
        if (encoder != null)
        {
            encoder.enabled = false;
            Destroy(encoder);
        }

        // 2. Restore the rhythm game's audio logic time
        TimeMockManager.StopMocking();

        // 3. Restore Unity's engine time back to realtime
        Time.captureFramerate = _originalCaptureFramerate;
    }
}