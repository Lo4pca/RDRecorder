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

    private void OnEnable()
    {
        // Initialize a RenderTexture matching the current screen resolution
        // ARGB32 is chosen to ensure compatibility with standard raw pixel reading
        _renderTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        _isCapturing = true;
        
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
        if (request.hasError)
        {
            Plugin.LogError("AsyncGPUReadback encountered an error while fetching frame data.");
            return;
        }

        // Extract the raw pixel data safely from the native GPU buffer
        NativeArray<byte> pixelData = request.GetData<byte>();
        
        // Convert to a managed byte array to send to another thread
        byte[] rawFrameBytes = [.. pixelData];
        FFmpegEncoder.Instance.EnqueueFrame(rawFrameBytes);
    }
}