using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using RDRecorder.Config;
using RDRecorder.Tools;

namespace RDRecorder.Record;

public class FFmpegEncoder : MonoBehaviour
{
    public static FFmpegEncoder Instance { get; private set; }

    private ConcurrentQueue<byte[]> _frameQueue;
    private Thread _encoderThread;
    private Process _ffmpegProcess;
    private bool _isEncoding;
    
    private string _outputPath;

    // Hard cap on how many frames we'll buffer before dropping new ones, expressed as
    // seconds of buffer so it scales with TargetFPS rather than being a fixed frame
    // count. Without a cap, a slow disk falling behind for long enough would let this
    // queue - and the memory it holds - grow without bound until the process crashes.
    // Dropping frames instead means the recording may end up shorter/skip-y under
    // sustained I/O pressure, but the plugin (and the game) stay running.
    private const int MaxQueuedSeconds = 5;
    private int _droppedFrameCount;

    // Throttles the "queue backing up" warning so a sustained I/O slowdown doesn't spam
    // one log line per frame (every ~16ms at 60fps).
    private float _lastBackupWarningTime = -999f;

    private void Awake()
    {
        Instance = this;
        _frameQueue = new ConcurrentQueue<byte[]>();
    }

    // Called by RecorderController once the level's PlaySong event has actually fired,
    // so PathInfo.GetOutputPath can safely read the current level's metadata. NOT called
    // from OnEnable(): arming a recording can happen before any level is loaded, and
    // PathInfo.GetOutputPath would throw if called at that point.
    public bool BeginEncoding()
    {
        _outputPath = PathInfo.GetOutputPath(false);

        if (!StartFFmpegProcess())
        {
            Plugin.LogError("Failed to start FFmpeg process. Please ensure ffmpeg.exe is in the game root folder or system PATH.");
            return false;
        }

        _isEncoding = true;
        _droppedFrameCount = 0;
        
        // Start the background consumer thread
        _encoderThread = new Thread(EncoderLoop)
        {
            IsBackground = true,
            Name = "FFmpegEncoderThread"
        };
        _encoderThread.Start();
        
        Plugin.LogInfo($"Encoder pipeline started. Output: {_outputPath}");
        return true;
    }

    private void OnDisable()
    {
        if (_ffmpegProcess == null && _encoderThread == null)
        {
            // BeginEncoding() was never called (e.g. armed but stopped before any
            // level's PlaySong event fired) - nothing to flush or clean up.
            if (Instance == this) Instance = null;
            return;
        }

        Plugin.LogDebug("Stopping encoder pipeline. Flushing remaining frames...");
        
        // Signal the thread to stop accepting new frames
        _isEncoding = false; 

        // Wait for the background thread to finish processing the queue
        if (_encoderThread != null && _encoderThread.IsAlive)
        {
            _encoderThread.Join(5000); // Wait up to 5 seconds
        }

        // Safely close the pipe and process
        if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
        {
            try
            {
                // Closing the input stream tells FFmpeg to finalize the mp4 file
                _ffmpegProcess.StandardInput.Close();
                _ffmpegProcess.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Error while closing FFmpeg: {ex.Message}");
            }
            finally
            {
                _ffmpegProcess.Close();
                _ffmpegProcess = null;
            }
        }
        
        if (_droppedFrameCount > 0)
        {
            Plugin.LogWarn($"Encoder queue was full at some point during this recording; dropped {_droppedFrameCount} frame(s). The output video may run shorter than the actual level duration.");
        }

        // Clear the singleton reference now that this instance is fully shut down, so a
        // FrameCapturer readback that completes after this point sees "no active
        // encoder" instead of a stale reference to a shut-down instance. Guarded so a
        // late-running OnDisable can never clobber a newer instance's reference.
        if (Instance == this) Instance = null;

        Plugin.LogInfo("Encoder pipeline stopped successfully.");
    }

    // Called by FrameCapturer on the main thread
    public void EnqueueFrame(byte[] frameData)
    {
        if (!_isEncoding) return;

        int maxQueuedFrames = Mathf.Max(60, PluginConfig.TargetFPS.Value * MaxQueuedSeconds);
        if (_frameQueue.Count >= maxQueuedFrames)
        {
            _droppedFrameCount++;
            // Only log occasionally while dropping so a sustained I/O stall doesn't itself
            // become a logging bottleneck.
            if (_droppedFrameCount == 1 || _droppedFrameCount % 60 == 0)
            {
                Plugin.LogError($"Encoder queue is full ({maxQueuedFrames} frames); dropping frames because disk I/O can't keep up. Dropped so far this session: {_droppedFrameCount}.");
            }
            return;
        }

        _frameQueue.Enqueue(frameData);

        // Warn if the queue is growing too large (I/O is bottlenecking), throttled to at
        // most once per second so a sustained backup doesn't spam the log every frame.
        if (_frameQueue.Count > 120 && Time.realtimeSinceStartup - _lastBackupWarningTime > 1f)
        {
            _lastBackupWarningTime = Time.realtimeSinceStartup;
            Plugin.LogWarn($"Encoder queue is backing up! Current size: {_frameQueue.Count} frames.");
        }
    }

    // --- Background Thread Logic ---

    private bool StartFFmpegProcess()
    {
        try
        {
            int width = Screen.width;
            int height = Screen.height;
            int fps = PluginConfig.TargetFPS.Value;

            // Note: ARGB32 in Unity AsyncReadback usually corresponds to 'rgba' in FFmpeg.
            // If colors look inverted (red/blue swapped), change this to 'bgra'.
            string arguments = $"-y -f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i - -c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{_outputPath}\"";

            _ffmpegProcess = new Process();
            
            // Assume ffmpeg is in the system PATH or placed alongside the game .exe
            _ffmpegProcess.StartInfo.FileName = "ffmpeg"; 
            _ffmpegProcess.StartInfo.Arguments = arguments;
            _ffmpegProcess.StartInfo.UseShellExecute = false;
            _ffmpegProcess.StartInfo.RedirectStandardInput = true;
            _ffmpegProcess.StartInfo.CreateNoWindow = true;

            return _ffmpegProcess.Start();
        }
        catch (Exception ex)
        {
            Plugin.LogError($"FFmpeg Process Start Error: {ex.Message}");
            return false;
        }
    }

    private void EncoderLoop()
    {
        // Run as long as recording is active OR there are still frames left in the queue
        while (_isEncoding || !_frameQueue.IsEmpty)
        {
            if (_frameQueue.TryDequeue(out byte[] frameData))
            {
                try
                {
                    // Write raw pixel bytes directly into FFmpeg's standard input pipeline
                    _ffmpegProcess.StandardInput.BaseStream.Write(frameData, 0, frameData.Length);
                }
                catch (Exception ex)
                {
                    Plugin.LogError($"Pipe write error: {ex.Message}");
                    // The pipe is broken - every further write will just throw again. Stop
                    // the loop outright instead of continuing to drain the rest of the
                    // queue against a dead pipe, which would otherwise log one error per
                    // remaining frame still sitting in the queue.
                    _isEncoding = false;
                    break;
                }
            }
            else
            {
                // Queue is empty, sleep briefly to prevent CPU spinning
                Thread.Sleep(1);
            }
        }
    }
}