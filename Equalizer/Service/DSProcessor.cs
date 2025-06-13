using Equalizer.Models;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Equalizer.Service
{
    /// <summary>
    /// Экземплярный класс обработки аудио сигнала
    /// </summary>
    public class DSProcessor : IDisposable
    {
        private WasapiCapture _CaptureDevice;
        private WasapiOut _OutDevice;
        private BufferedWaveProvider _BufferedWaveProvider;
        private bool _IsDisposed;
        private bool _IsOutDeviceRunning;
        private bool _IsCaptureDeviceRunning;
        private readonly FrequencyLine _DefaultLine;
        public bool Initialized { get; private set; }
        public bool IsRunning => _IsCaptureDeviceRunning || _IsOutDeviceRunning;
        /// <summary>
        /// Представляет собой коллекцию полос эквалайзера
        /// </summary>
        public List<FrequencyLine> FrequencyLines { get; private set; }
        //---------------------------------------------------------------------------------------
        //для overlap (потом привести к нормальному виду)
        /// <summary>
        /// Размер фрейма для работы с окнами и перекрытием (обычно степень двойки)
        /// </summary>
        private const int FrameSize = 1024;
        /// <summary>
        /// Размер перекрытия (50%)
        /// </summary>
        private const int HopSize = FrameSize / 2; 
        private float[] _overlapBuffer = new float[FrameSize]; // для хранения наложения
        private List<float> _inputBuffer = new(); // накопитель входных данных
        //---------------------------------------------------------------------------------------



        /// <summary>
        /// инициализирует устройство вывода и устройство для захвата
        /// </summary>
        public void Initialize(MMDevice outDevice, MMDevice captureDevice)
        {
            if (!Initialized)
            {
                Initialized = true;
                _CaptureDevice = new WasapiLoopbackCapture(captureDevice) { ShareMode = AudioClientShareMode.Shared };
                // TODO сделать отмену остановки при переключении устройства и переключениями видосов в браузере (хз почему он останавливается)
                _CaptureDevice.RecordingStopped += (s, e) =>
                {
                    _IsCaptureDeviceRunning = false;
                };
                _CaptureDevice.DataAvailable += (s, e) =>
                {
                    byte[] processedData = ProcessAudioData(e.Buffer, e.BytesRecorded);
                    _BufferedWaveProvider.AddSamples(processedData, 0, processedData.Length);
                };
                _BufferedWaveProvider = new BufferedWaveProvider(_CaptureDevice.WaveFormat)
                {
                    BufferLength = _CaptureDevice.WaveFormat.AverageBytesPerSecond * 2,
                    DiscardOnBufferOverflow = true,
                };
                _OutDevice = new WasapiOut(outDevice, AudioClientShareMode.Shared, false, 100);
                _OutDevice.Init(_BufferedWaveProvider);
                _OutDevice.PlaybackStopped += (s, e) =>
                {
                    _IsOutDeviceRunning = false;
                };
            }
        }
        /// <summary>
        /// инициализирует устройство вывода и по дефолту ищет устройство для захвата VAC
        /// </summary>
        public void Initialize(MMDevice outDevice)
        {
            Initialize(outDevice,
                       new MMDeviceEnumerator()
                      .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                      .First(item => item.FriendlyName.Contains("Virtual")));
        }
        private byte[] ProcessAudioData(byte[] inputBuffer, int bytesRecorded)
        {
            List<float> outputSamples = [];
            if (FrequencyLines.Count != 0)
            {
                float[] audioData = ConvertBytesToFloats(inputBuffer, _OutDevice.OutputWaveFormat, bytesRecorded);
                _inputBuffer.AddRange(audioData);
                //--------------------------------------------------------------------------------
                while (_inputBuffer.Count >= FrameSize)
                {
                    // 1. Берем FrameSize сэмплов
                    float[] frame = [.. _inputBuffer.GetRange(0, FrameSize)];

                    // 2. Применяем Hann-окно
                    for (int i = 0; i < FrameSize; i++)
                    {
                        frame[i] *= (float)unchecked(FastFourierTransform.HannWindow(i, FrameSize));
                    }

                    // 3. FFT
                    Complex[] fftData = new Complex[FrameSize];
                    for (int i = 0; i < FrameSize; i++)
                    {
                        fftData[i] = new Complex { X = frame[i], Y = 0 };
                    }

                    FastFourierTransform.FFT(true, (int)Math.Log2(FrameSize), fftData);

                    // 4. Усиление по частотам
                    float freqStep = _CaptureDevice.WaveFormat.SampleRate / (float)FrameSize;
                    for (int i = 0; i < FrameSize; i++)
                    {
                        var line = FrequencyLines.FirstOrDefault(
                            item => item.From < i * freqStep && item.To > i * freqStep, _DefaultLine);
                        float gain = (float)unchecked(GetMultiplier(line.GainDecibells));
                        fftData[i].X *= gain;
                        fftData[i].Y *= gain;
                    }

                    // 5. Обратное FFT
                    FastFourierTransform.FFT(false, (int)Math.Log2(FrameSize), fftData);

                    float[] ifftResult = new float[FrameSize];
                    for (int i = 0; i < FrameSize; i++)
                    {
                        ifftResult[i] = fftData[i].X;
                    }

                    // 6. Overlap-Add (накладываем результат)
                    for (int i = 0; i < FrameSize; i++)
                    {
                        int index = i;
                        if (index < _overlapBuffer.Length)
                            ifftResult[i] += _overlapBuffer[i];
                    }

                    // 7. Сохраняем половину для следующего блока (в перекрытие)
                    Array.Copy(ifftResult, HopSize, _overlapBuffer, 0, FrameSize - HopSize);

                    // 8. Сохраняем первую половину в выход
                    for (int i = 0; i < HopSize; i++)
                    {
                        outputSamples.Add(ifftResult[i]);
                    }

                    // 9. Удаляем использованные входные данные
                    _inputBuffer.RemoveRange(0, HopSize);
                }
                return ConvertFloatToBytes([.. outputSamples], _OutDevice.OutputWaveFormat);
                //--------------------------------------------------------------------------------





                /* Complex[] fftData = new Complex[audioData.Length];
                 for (int i = 0; i < audioData.Length; i++)
                 {
                     fftData[i] = new Complex() { X = audioData[i] , Y = 0 };
                 }
                 FastFourierTransform.FFT(true, (int)Math.Log2(audioData.Length), fftData);
                 float freq = _CaptureDevice.WaveFormat.SampleRate / (float)fftData.Length;
                 FrequencyLine CurrentLine;
                 for (int i = 0; i < audioData.Length; i++)
                 {
                     CurrentLine = FrequencyLines.FirstOrDefault(item => item.From < i * freq && item.To > i * freq, _DefaultLine);
                     fftData[i].X *= (float)unchecked(GetMultiplier(CurrentLine.GainDecibells));
                     fftData[i].Y *= (float)unchecked(GetMultiplier(CurrentLine.GainDecibells));
                 }
                 FastFourierTransform.FFT(false, (int)Math.Log2(audioData.Length), fftData);
                 float[] processed = new float[audioData.Length];
                 for (int i = 0; i < processed.Length; i++)
                 {
                     processed[i] = fftData[i].X;
                 }
                 return ConvertFloatToBytes(processed, _OutDevice.OutputWaveFormat);*/
            }
            else 
            {
                return inputBuffer;
            }
        }
        /// <summary>
        /// Преобразует децибелы в мультипликатор для увеличения/уменьшения амплитуды сигнала
        /// </summary>
        private static double GetMultiplier(int decibells)
        {
            //TODO проверить как правильно получать мультипликатор на основе децибел
            return Math.Pow(10, decibells / 10d);
        }
        #region Конвертации
        /// <summary>
        /// Преобразует float[] в byte[] учитывая формат аудио
        /// </summary>
        private static byte[] ConvertFloatToBytes(float[] samples, WaveFormat waveFormat)
        {
            byte[] bytes = new byte[samples.Length * (waveFormat.BitsPerSample / 8)];

            if (waveFormat.BitsPerSample == 16)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    float clampedSample = Math.Clamp(samples[i], -1f, 1f);
                    byte[] sampleBytes = BitConverter.GetBytes((short)clampedSample * short.MaxValue);
                    bytes[i * 2] = sampleBytes[0];
                    bytes[(i * 2) + 1] = sampleBytes[1];
                }
            }
            else if (waveFormat.BitsPerSample == 32)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    byte[] sampleBytes;
                    if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        float clampedSample = Math.Clamp(samples[i], -1f, 1f);
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
        /// <summary>
        /// Преобразует byte[] в float[] учитывая формат аудио
        /// </summary>
        private static float[] ConvertBytesToFloats(byte[] buffer, WaveFormat format, int bytesRecorded)
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
        #endregion
        /// <summary>
        /// Диспозит текущие устройства захвата и вывода и записывает новые
        /// </summary>
        public void ChangeDevices(MMDevice outDevice, MMDevice captureDevice) 
        {
            if (Initialized && !_IsDisposed)
            {
                StopCapture();
                _BufferedWaveProvider.ClearBuffer();
                _CaptureDevice?.Dispose();
                _OutDevice?.Dispose();
            }
            _IsDisposed = false;
            Initialized = false;
            Initialize(outDevice,captureDevice);
        }
        /// <summary>
        /// Диспозит текущие устройства захвата и вывода и записывает новое устройство вывода (устройство захвата по дефолту VAC)
        /// </summary>
        public void ChangeDevices(MMDevice outDevice)
        {
            ChangeDevices(outDevice, new MMDeviceEnumerator()
                      .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                      .First(item => item.FriendlyName.Contains("Virtual")));
        }
        /// <summary>
        /// Начинает захват, преобразование и передачу на устройство вывода аудио сигнала
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public void StartCapture()
        {
            if (!Initialized)
                throw new ArgumentException("processor doesn't initialized (no capture and output devices)");
            if (!IsRunning)
            {
                _CaptureDevice.StartRecording();
                _IsCaptureDeviceRunning = true;
                _OutDevice.Play();
                _IsOutDeviceRunning = true;
            }
        }
        /// <summary>
        /// Останавливает захват, преобразование и передачу на устройство вывода аудио сигнала. Полная остановка происходит с задержкой
        /// </summary>
        public void StopCapture()
        {
            if (Initialized)
            {
                _CaptureDevice?.StopRecording();
                _OutDevice?.Stop();
            }
        }
        public void Dispose()
        {
            if (!_IsDisposed && Initialized)
            {
                _IsDisposed = true;
                StopCapture();
                _BufferedWaveProvider.ClearBuffer();
                _CaptureDevice?.Dispose();
                _OutDevice?.Dispose();
            }
        }
        public DSProcessor()
        {
            FrequencyLines = [];
            _DefaultLine = new(0,0);
        }
        /// <summary>
        /// Возвращает список устройств доступных для вывода аудио сигнала
        /// </summary>
        public static List<MMDevice> GetDevices()
        {
            return [.. new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)];
        }
    }
}
