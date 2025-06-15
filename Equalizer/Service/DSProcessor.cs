using Equalizer.Models;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public ObservableCollection<FrequencyLine> FrequencyLines { get; private set; }
        //-----------------------------------------------------------------------
        private float[] _Spectrum;
        public delegate void SpectrumCalculatedHandler(float[] spectrum);
        public event SpectrumCalculatedHandler SpectrumCalculated;
        //-----------------------------------------------------------------------
        /// <summary>
        /// Размер фрейма для работы с окнами и перекрытием (обычно степень двойки)
        /// </summary>
        private const int FrameSize = 1024;
        /// <summary>
        /// Размер перекрытия (50%)
        /// </summary>
        private const int HopSize = FrameSize / 2;
        /// <summary>
        /// Буфер для перекрытия кусков
        /// </summary>
        private readonly float[] _OverlapBuffer;
        /// <summary>
        /// Буфер для чтения данных
        /// </summary>
        private readonly List<float> _InputBuffer;
        /// <summary>
        /// Инициализирует устройство вывода и устройство для захвата
        /// </summary>
        public void Initialize(MMDevice outDevice, MMDevice captureDevice)
        {
            if (!Initialized)
            {
                Initialized = true;
                _CaptureDevice = new WasapiLoopbackCapture(captureDevice) { ShareMode = AudioClientShareMode.Shared };
                // TODO сделать отмену остановки при переключении устройства и переключениями видосов в браузере (хз почему он останавливается, в ++ не выключался)
                // проблема с касперским (при отключении касперского отключение не происходит)
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
        /// Инициализирует устройство вывода и по дефолту ищет устройство для захвата VAC
        /// </summary>
        public void Initialize(MMDevice outDevice)
        {
            Initialize(outDevice,
                       new MMDeviceEnumerator()
                      .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                      .First(item => item.FriendlyName.Contains("Virtual")));
        }
        /// <summary>
        /// Обрабатывает данные с устройства захвата по добавленным полосам и возвращает обработанные данные
        /// </summary>
        private byte[] ProcessAudioData(byte[] inputBuffer, int bytesRecorded)
        {
            //TODO сделать чет с GC (срабатывает раз в 3 сек на 1 поколении)
            List<float> OutputSamples = [];
            if (FrequencyLines.Count != 0)
            {
                // считываем данные в инпут буфер
                _InputBuffer.AddRange(ConvertBytesToFloats(inputBuffer, _OutDevice.OutputWaveFormat, bytesRecorded));
                while (_InputBuffer.Count >= FrameSize)
                {
                    // берем кусок в 1024 элемента из инпут буфера
                    float[] Frame = [.. _InputBuffer.GetRange(0, FrameSize)];
                    // добавляем окно в кусок элементов
                    for (int i = 0; i < FrameSize; i++)
                    {
                        Frame[i] *= (float)unchecked(FastFourierTransform.HannWindow(i, FrameSize));
                    }
                    // создаем массив комплексных чисел для бпф
                    Complex[] FFTData = new Complex[FrameSize];
                    for (int i = 0; i < FrameSize; i++)
                    {
                        FFTData[i] = new Complex { X = Frame[i], Y = 0 };
                    }
                    // обрабатываем массив комплексных чисел для перевода спектра из время/частоты в амплитуды/частоты
                    FastFourierTransform.FFT(true, (int)Math.Log2(FrameSize), FFTData);
                    //--------------------------------------------------------------------------------------
                    //TODO придумать как отображать без приколов от hann window
                    for (int i = 0; i < FrameSize; i++)
                    {
                        float magnitude = (float)Math.Sqrt(FFTData[i].X * FFTData[i].X + FFTData[i].Y * FFTData[i].Y);
                        var dbValue = 20 * (float)Math.Log10(magnitude + 1e-10f); 
                        _Spectrum[i] = Math.Clamp((dbValue + 60) / 60,0,1);
                    }
                    SpectrumCalculated?.Invoke(_Spectrum);
                    //--------------------------------------------------------------------------------------
                    // вычисляем шаг частот
                    float FrequencyStep = _CaptureDevice.WaveFormat.SampleRate / (float)FrameSize;
                    // находим линию которая содержит децибелы и границы частот и если нашлась такая то получаем мультипликатор на который умножаем амплитуду
                    for (int i = 0; i < FrameSize; i++)
                    {
                        FrequencyLine Line = FrequencyLines.FirstOrDefault(
                            item => item.From < i * FrequencyStep && item.To > i * FrequencyStep, _DefaultLine);
                        float gain = (float)unchecked(GetMultiplier(Line.GainDecibells));
                        FFTData[i].X *= gain;
                        FFTData[i].Y *= gain;
                    }
                    // преобразуем обратно из амплитуды/частоты в время/частоты
                    FastFourierTransform.FFT(false, (int)Math.Log2(FrameSize), FFTData);
                    // записываем в выходной буфер массив после преобразования бпф и перекрытие
                    float[] IFFTBuffer = new float[FrameSize];
                    for (int i = 0; i < FrameSize; i++)
                    {
                        IFFTBuffer[i] = FFTData[i].X + _OverlapBuffer[i];
                    }
                    // закидываем второй кусок из выходного буфера в буфер для перекрытия
                    Array.Copy(IFFTBuffer, HopSize, _OverlapBuffer, 0, HopSize);
                    // записываем в массив который будет рендериться на устройстве первый кусок (второй кусок будет в некст куске для перекрытия)
                    for (int i = 0; i < HopSize; i++)
                    {
                        OutputSamples.Add(IFFTBuffer[i]);
                    }
                    // чистим инпут буффер для получения некст данных
                    _InputBuffer.RemoveRange(0, HopSize);
                }
                return ConvertFloatToBytes([.. OutputSamples], _OutDevice.OutputWaveFormat);
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
            return Math.Pow(10, decibells / 20d);
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
            Initialize(outDevice, captureDevice);
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
        public void ChangeVolume(int Volume)
        {
            if (Volume < 0 || Volume > 100)
                throw new ArgumentException("Volume must be positive and less or equal than 100");
            _OutDevice.Volume = Volume / 100f;
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
            _Spectrum = new float[FrameSize];
            FrequencyLines = [];
            _DefaultLine = new(0, 0);
            _OverlapBuffer = new float[FrameSize];
            _InputBuffer = [];
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
