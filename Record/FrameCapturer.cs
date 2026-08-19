using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using RDRecorder.Core;

namespace RDRecorder.Record;

public class FrameCapturer : MonoBehaviour
{
    private RenderTexture _renderTexture;
    private bool _isCapturing;

    // The encoder this capture session is feeding. Captured once at OnEnable rather than
    // read from the static FFmpegEncoder.Instance inside the async callback, so that a
    // readback which completes after this session has stopped (or after a new recording
    // has already replaced Instance) can't write frames into the wrong encoder.
    private FFmpegEncoder _targetEncoder;

    private void OnEnable()
    {
        // Initialize a RenderTexture matching the current screen resolution
        // ARGB32 is chosen to ensure compatibility with standard raw pixel reading
        _renderTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        _isCapturing = true;
        _targetEncoder = FFmpegEncoder.Instance;
        
        Plugin.LogInfo($"FrameCapturer started at resolution {Screen.width}x{Screen.height}.");
        
        // Start the capture loop
        StartCoroutine(CaptureLoop());
    }

    private void OnDisable()
    {
        _isCapturing = false;
        StopAllCoroutines();

        // Release GPU resources
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }

    private IEnumerator CaptureLoop()
    {
        while (_isCapturing)
        {
            // Wait until Unity has completely finished rendering the current frame
            yield return new WaitForEndOfFrame();

            // We may have been stopped while waiting for the frame to finish rendering;
            // don't kick off a capture for a session that's already ending.
            if (!_isCapturing) yield break;

            // Blit the final screen frame (including UI) into our RenderTexture
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_renderTexture);

            // Request an asynchronous readback from the GPU to RAM
            // This prevents the game pipeline from stalling while downloading pixels
            AsyncGPUReadback.Request(_renderTexture, 0, OnCompleteReadback);

            // Advance the mock dspTime so the next frame's rhythm game logic calculates correctly
            TimeMockManager.AdvanceFrame();
        }
    }

    private void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        // Drop any readback that completes after this session has stopped, or whose
        // target encoder is no longer the live one (e.g. a quick stop -> start). Without
        // this, a frame captured just before StopRecording() could be enqueued into a
        // brand-new recording that started moments later, corrupting it with a stray
        // frame from the previous take.
        if (!_isCapturing || _targetEncoder == null || _targetEncoder != FFmpegEncoder.Instance) return;

        if (request.hasError)
        {
            Plugin.LogError("AsyncGPUReadback encountered an error while fetching frame data.");
            return;
        }

        // Extract the raw pixel data safely from the native GPU buffer
        NativeArray<byte> pixelData = request.GetData<byte>();
        
        // Convert to a managed byte array to send to another thread
        byte[] rawFrameBytes = [.. pixelData];
        _targetEncoder.EnqueueFrame(rawFrameBytes);
    }
}