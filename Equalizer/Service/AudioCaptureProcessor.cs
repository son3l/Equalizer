using Microsoft.VisualBasic;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Equalizer.Service
{
    public class AudioCaptureProcessor : IDisposable
    {
        private readonly WasapiCapture _CaptureDevice;
        private readonly WasapiOut _OutDevice;
        private readonly BufferedWaveProvider _BufferedWaveProvider;
        private bool _IsDisposed;
        private bool IsRunning;
        public AudioCaptureProcessor(MMDevice outDevice)
        {
            _CaptureDevice = new WasapiLoopbackCapture(
                new MMDeviceEnumerator()
                .EnumerateAudioEndPoints(DataFlow.Render,DeviceState.All)
                .First(item=>item.FriendlyName.Contains("Virtual"))
                );
            // TODO сделать отмену остановки при переключении устройства
            _CaptureDevice.RecordingStopped += (s,e) => 
            {
                IsRunning = false;
            };
            _CaptureDevice.ShareMode = AudioClientShareMode.Shared;
            _BufferedWaveProvider = new BufferedWaveProvider(_CaptureDevice.WaveFormat)
            {
                BufferLength = _CaptureDevice.WaveFormat.AverageBytesPerSecond * 2,
                DiscardOnBufferOverflow = true,
            };
            _CaptureDevice.DataAvailable += (s,e)=> 
            {
                byte[] processedData = ProcessAudioData(e.Buffer, e.BytesRecorded);
                _BufferedWaveProvider.AddSamples(processedData, 0, processedData.Length);
            };
            _OutDevice = new WasapiOut(outDevice, AudioClientShareMode.Shared, false, 100); 
            _OutDevice.Init(_BufferedWaveProvider);
        }
        public static List<MMDevice> GetDevices()
        {
            return [.. new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)];
        }
        private byte[] ProcessAudioData(byte[] inputBuffer, int bytesRecorded)
        {
            float[] audioData = ConvertBytesToFloats(inputBuffer, _OutDevice.OutputWaveFormat, bytesRecorded);


            //под сомнением (дерьмеще какое то реально)
            Complex[] fftData = new Complex[audioData.Length];
            for (int i = 0; i < audioData.Length; i++)
            {
                fftData[i] = new Complex() { X = audioData[i], Y = 0 };
            }
            FastFourierTransform.FFT(true, (int)Math.Log2(audioData.Length), fftData);
            float freq = _CaptureDevice.WaveFormat.SampleRate / (float) fftData.Length;
            for (int i = 0; i < audioData.Length; i++)
            {
                if (i * freq <= 2000)
                {
                    fftData[i].X *= (float)unchecked(GetMultiplier(3));
                    fftData[i].Y *= (float)unchecked(GetMultiplier(3));
                }
            }
            //обратно комплексное в флоат и потом в байты на рендер
            FastFourierTransform.FFT(false,(int)Math.Log2(audioData.Length),fftData);
            float[] processed = new float[audioData.Length];
            for (int i = 0; i < processed.Length; i++)
            {
                processed[i] = fftData[i].X;
            }




            byte[] processedBuffer = ConvertFloatToBytes(processed, _OutDevice.OutputWaveFormat);
            return processedBuffer;
        }
        private double GetMultiplier(int decibells) 
        {
            double multiplier = Math.Pow(10, decibells / 10d);
            return Math.Pow(10,decibells/10d);
        }
        private byte[] ConvertFloatToBytes(float[] samples, WaveFormat waveFormat)
        {
            byte[] bytes = new byte[samples.Length * (waveFormat.BitsPerSample / 8)];

            if (waveFormat.BitsPerSample == 16)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    float clampedSample = Math.Clamp(samples[i], -1f, 1f);
                    byte[] sampleBytes = BitConverter.GetBytes((short)clampedSample * short.MaxValue);
                    bytes[i * 2] = sampleBytes[0];
                    bytes[i * 2 + 1] = sampleBytes[1];
                }
            }
            else if (waveFormat.BitsPerSample == 32)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    byte[] sampleBytes;
                    if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        float clampedSample = Math.Clamp(samples[i],-1f,1f);
                        sampleBytes = BitConverter.GetBytes(clampedSample);
                    }
                    else
                    {
                        float clampedSample = Math.Clamp(samples[i], -1f, 1f);
                        sampleBytes = BitConverter.GetBytes(clampedSample * int.MaxValue);
                    }
                    Array.Copy(sampleBytes, 0, bytes, i * 4, 4);
                }
            }
            return bytes;
        }
        private float[] ConvertBytesToFloats(byte[] buffer, WaveFormat format, int bytesRecorded)
        {
            int samplesCount = bytesRecorded / (format.BitsPerSample / 8);
            float[] samples = new float[samplesCount];
            if (format.BitsPerSample == 16)
            {
                for (int i = 0; i < samplesCount; i++)
                {
                    samples[i] = BitConverter.ToInt16(buffer, i * 2) / (float)short.MaxValue;
                }
            }
            else if (format.BitsPerSample == 32)
            {
                for (int i = 0; i < samplesCount; i++)
                {
                    if (format.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        samples[i] = BitConverter.ToSingle(buffer, i * 4);
                    }
                    else
                    {
                        int sample = BitConverter.ToInt32(buffer, i * 4);
                        samples[i] = sample / (float)int.MaxValue;
                    }
                }
            }
            return samples;
        }
        public void StartCapture()
        {
            if (!IsRunning)
            {
                IsRunning = true;
                _CaptureDevice.StartRecording();
                _OutDevice.Play();
            }
        }
        public void StopCapture()
        {
            _CaptureDevice?.StopRecording();
            _OutDevice?.Stop();
        }
        public void Dispose()
        {
            if (!_IsDisposed)
            {
                _IsDisposed = true;
                StopCapture();
                _BufferedWaveProvider.ClearBuffer();
                _CaptureDevice?.Dispose();
                _OutDevice?.Dispose();
            }
        }
    }
}
