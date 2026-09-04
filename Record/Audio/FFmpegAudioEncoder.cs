using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using RDRecorder.Tools;
using System;

namespace RDRecorder.Record.Audio;

public class FFmpegAudioEncoder : MonoBehaviour
{
    private readonly ConcurrentQueue<byte[]> _audioQueue = new();
    private Thread _encoderThread;
    private Process _ffmpegProcess;
    private bool _isEncoding;
    private string _outputPath;

    // Audio chunks from OnAudioFilterRead are much smaller than a full video frame, but
    // still capped to avoid unbounded growth if disk I/O falls behind for a long time -
    // mirrors FFmpegEncoder's frame-drop cap.
    private const int MaxQueuedChunks = 2000;
    private int _droppedChunkCount;

    // Called by AudioRecorderController once the level's PlaySong event has actually
    // fired, so PathInfo.GetOutputPath can safely read the current level's metadata.
    public bool BeginEncoding()
    {
        if (!StartFFmpegProcess())
        {
            Plugin.LogError("Failed to start FFmpeg for audio. Please ensure ffmpeg.exe is in the game root folder or system PATH.");
            return false;
        }

        _isEncoding = true;
        _droppedChunkCount = 0;
        _encoderThread = new Thread(EncoderLoop) { IsBackground = true, Name = "FFmpegAudioEncoderThread" };
        _encoderThread.Start();

        Plugin.LogInfo($"Audio encoder pipeline started. Output: {_outputPath}");
        return true;
    }

    private void OnDisable()
    {
        if (_ffmpegProcess == null && _encoderThread == null) return; // BeginEncoding() was never called

        Plugin.LogDebug("Stopping audio encoder pipeline...");
        _isEncoding = false;
        
        if (_encoderThread != null && _encoderThread.IsAlive)
            _encoderThread.Join(2000);
        
        if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
        {
            try
            {
                _ffmpegProcess.StandardInput.Close();
                _ffmpegProcess.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Error while closing FFmpeg (audio): {ex.Message}");
            }
            finally
            {
                _ffmpegProcess.Close();
                _ffmpegProcess = null;
            }
        }

        if (_droppedChunkCount > 0)
        {
            Plugin.LogWarn($"Audio encoder queue was full at some point during this recording; dropped {_droppedChunkCount} chunk(s).");
        }

        Plugin.LogInfo("Audio encoder pipeline stopped.");
    }

    private bool StartFFmpegProcess()
    {
        try
        {
            _outputPath = PathInfo.GetOutputPath(true);
            int sampleRate = AudioSettings.outputSampleRate;
            int channels = 2; // Unity standard stereo

            // Requesting raw 32-bit float little-endian (f32le)
            string arguments = $"-y -f f32le -ar {sampleRate} -ac {channels} -i - -c:a aac -b:a 192k \"{_outputPath}\"";

            _ffmpegProcess = new Process();
            _ffmpegProcess.StartInfo.FileName = "ffmpeg";
            _ffmpegProcess.StartInfo.Arguments = arguments;
            _ffmpegProcess.StartInfo.UseShellExecute = false;
            _ffmpegProcess.StartInfo.RedirectStandardInput = true;
            _ffmpegProcess.StartInfo.CreateNoWindow = true;

            return _ffmpegProcess.Start();
        }
        catch (Exception ex)
        {
            Plugin.LogError($"FFmpeg Process Start Error (audio): {ex.Message}");
            return false;
        }
    }

    public void EnqueueAudio(byte[] audioData)
    {
        if (!_isEncoding) return;

        if (_audioQueue.Count >= MaxQueuedChunks)
        {
            _droppedChunkCount++;
            if (_droppedChunkCount == 1 || _droppedChunkCount % 200 == 0)
            {
                Plugin.LogError($"Audio encoder queue is full; dropping audio chunks because disk I/O can't keep up. Dropped so far this session: {_droppedChunkCount}.");
            }
            return;
        }

        _audioQueue.Enqueue(audioData);
    }

    private void EncoderLoop()
    {
        while (_isEncoding || !_audioQueue.IsEmpty)
        {
            if (_audioQueue.TryDequeue(out byte[] data))
            {
                try
                {
                    _ffmpegProcess.StandardInput.BaseStream.Write(data, 0, data.Length);
                }
                catch (Exception ex)
                {
                    Plugin.LogError($"Audio pipe write error: {ex.Message}");
                    _isEncoding = false;
                    break;
                }
            }
            else
            {
                Thread.Sleep(1);
            }
        }
    }
}