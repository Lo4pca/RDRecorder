using System;
using UnityEngine;

namespace RDRecorder.Record.Audio;

public class AudioCapturer : MonoBehaviour
{
    public FFmpegAudioEncoder TargetEncoder;
    public bool IsCapturing = false;

    private void OnAudioFilterRead(float[] data, int channels)
    {
        // Only process audio if the recording signal has been given
        if (!IsCapturing || TargetEncoder == null || !TargetEncoder.enabled) return;

        byte[] byteData = new byte[data.Length * 4]; 
        Buffer.BlockCopy(data, 0, byteData, 0, byteData.Length);
        
        TargetEncoder.EnqueueAudio(byteData);
    }
}