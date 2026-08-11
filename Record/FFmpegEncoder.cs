using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using RDRecorder.Config;

namespace RDRecorder.Record;

public class FFmpegEncoder : MonoBehaviour
{
    public static FFmpegEncoder Instance { get; private set; }

    private ConcurrentQueue<byte[]> _frameQueue;
    private Thread _encoderThread;
    private Process _ffmpegProcess;
    private bool _isEncoding;
    
    private string _outputPath;

    private void Awake()
    {
        Instance = this;
        _frameQueue = new ConcurrentQueue<byte[]>();
    }

    private void OnEnable()
    {
        // Setup output file path with timestamp
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _outputPath = Path.Combine(PluginConfig.OutputFolder.Value, $"Record_{timestamp}.mp4");

        if (!StartFFmpegProcess())
        {
            Plugin.LogError("Failed to start FFmpeg process. Please ensure ffmpeg.exe is in the game root folder or system PATH.");
            enabled = false;
            return;
        }

        _isEncoding = true;
        
        // Start the background consumer thread
        _encoderThread = new Thread(EncoderLoop)
        {
            IsBackground = true,
            Name = "FFmpegEncoderThread"
        };
        _encoderThread.Start();
        
        Plugin.LogInfo($"Encoder pipeline started. Output: {_outputPath}");
    }

    private void OnDisable()
    {
        Plugin.LogInfo("Stopping encoder pipeline. Flushing remaining frames...");
        
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
        
        Plugin.LogInfo("Encoder pipeline stopped successfully.");
    }

    // Called by FrameCapturer on the main thread
    public void EnqueueFrame(byte[] frameData)
    {
        if (!_isEncoding) return;
        
        _frameQueue.Enqueue(frameData);

        // Warn if the queue is growing too large (I/O is bottlenecking)
        if (_frameQueue.Count > 120) 
        {
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
                    _isEncoding = false; // Abort on fatal pipe error
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