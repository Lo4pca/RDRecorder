using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using RDRecorder.Tools;
using System;

namespace RDRecorder.Record.Audio;

public class FFmpegAudioEncoder : MonoBehaviour
{
    private ConcurrentQueue<byte[]> _audioQueue = new();
    private Thread _encoderThread;
    private Process _ffmpegProcess;
    private bool _isEncoding;

    private void OnEnable()
    {
        if (!StartFFmpegProcess())
        {
            Plugin.LogError("Failed to start FFmpeg for audio.");
            enabled = false;
            return;
        }

        _isEncoding = true;
        _encoderThread = new Thread(EncoderLoop) { IsBackground = true, Name = "FFmpegAudioEncoderThread" };
        _encoderThread.Start();
    }

    private void OnDisable()
    {
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
                Plugin.LogError($"Error while closing FFmpeg: {ex.Message}");
            }
            finally
            {
                _ffmpegProcess.Close();
                _ffmpegProcess = null;
            }
        }
    }

    private bool StartFFmpegProcess()
    {
        try
        {
            int sampleRate = AudioSettings.outputSampleRate;
            int channels = 2; // Unity standard stereo

            // Requesting raw 32-bit float little-endian (f32le)
            string arguments = $"-y -f f32le -ar {sampleRate} -ac {channels} -i - -c:a aac -b:a 192k \"{PathInfo.GetOutputPath(true)}\"";

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
            Plugin.LogError($"FFmpeg Process Start Error: {ex.Message}");
            return false;
        }
    }

    public void EnqueueAudio(byte[] audioData)
    {
        if (_isEncoding) _audioQueue.Enqueue(audioData);
    }

    private void EncoderLoop()
    {
        while (_isEncoding || !_audioQueue.IsEmpty)
        {
            if (_audioQueue.TryDequeue(out byte[] data))
            {
                try { _ffmpegProcess.StandardInput.BaseStream.Write(data, 0, data.Length); }
                catch { _isEncoding = false; break; }
            }
            else
            {
                Thread.Sleep(1);
            }
        }
    }
}