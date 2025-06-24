using Avalonia.Threading;
using Equalizer.Models;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Equalizer.Service
{
    /// <summary>
    /// Экземплярный класс обработки аудио сигнала
    /// </summary>
    public sealed class DSProcessor : IDisposable
    {
        private WasapiCapture _CaptureDevice;
        private WasapiOut _OutDevice;
        private BufferedWaveProvider _BufferedWaveProvider;
        private bool _IsDisposed;
        private bool _IsOutDeviceRunning;
        private bool _IsCaptureDeviceRunning;
        /// <summary>
        /// Дефолт линия, используется если не найдено нужных линий
        /// </summary>
        private readonly FrequencyLine _DefaultLine;
        public bool Initialized { get; private set; }
        public bool IsRunning => _IsCaptureDeviceRunning || _IsOutDeviceRunning;
        /// <summary>
        /// Представляет собой коллекцию полос эквалайзера
        /// </summary>
        public ObservableCollection<FrequencyLine>? FrequencyLines { get; private set; }
        /// <summary>
        /// Ивент который срабатывает при пересчете фрейма спектра
        /// </summary>
        public delegate void SpectrumCalcucated(float[] spectrum);
        public SpectrumCalcucated? SpectrumCalcucatedHandler { get; set; }
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
        /// Буфер рассчитываемого фрейма
        /// </summary>
        private readonly float[] _FrameBuffer;
        /// <summary>
        /// Буфер для комплексных чисел  
        /// </summary>
        private readonly Complex[] _FFTBuffer;
        /// <summary>
        /// Буфер для обратно преобразованных комплексных чисел  
        /// </summary>
        private readonly float[] _IFFTBuffer;
        /// <summary>
        /// Буфер для выходных семплов
        /// </summary>
        private readonly List<float> _OutputSamples;
        /// <summary>
        /// Шаг дискретизации
        /// </summary>
        private float _FrequencyStep;
        /// <summary>
        /// Количество фреймов (для жесткого ограничения количества кадров при рендере)
        /// </summary>
        private int _SpectrumFramesCount;
        /// <summary>
        ///  Очередь выполнения обработки звука
        /// </summary>
        private readonly ProcessingThreadQueue _ProcessingQueue;
        /// <summary>
        /// Инициализирует устройство вывода и устройство для захвата
        /// </summary>
        public void Initialize(MMDevice outDevice, MMDevice captureDevice)
        {
            if (!Initialized)
            {

                Initialized = true;
                _CaptureDevice = new WasapiLoopbackCapture(captureDevice) { ShareMode = AudioClientShareMode.Shared };
                _CaptureDevice.RecordingStopped += (s, e) =>
                {
                    _IsCaptureDeviceRunning = false;
                };
                _CaptureDevice.DataAvailable += (s, e) =>
                {
                    _ProcessingQueue.Enqueue(ProcessAudioData, e);
                };
                _FrequencyStep = _CaptureDevice.WaveFormat.SampleRate / (float)FrameSize;
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
        private void ProcessAudioData(WaveInEventArgs e)
        {
            if (FrequencyLines is null || FrequencyLines.Count == 0)
                _BufferedWaveProvider.AddSamples(e.Buffer,0,e.BytesRecorded);
            // считываем данные в инпут буфер
            int inputSize = e.BytesRecorded / (_OutDevice.OutputWaveFormat.BitsPerSample / 8);
            float[] inputBuffer = ArrayPool<float>.Shared.Rent(inputSize);
            ConvertBytesToFloats(e.Buffer, inputBuffer, inputSize, _OutDevice.OutputWaveFormat);
            for (int i = 0; i < inputSize; i++)
            {
                _InputBuffer.Add(inputBuffer[i]);
            }
            ArrayPool<float>.Shared.Return(inputBuffer);
            while (_InputBuffer.Count >= FrameSize)
            {
                // берем кусок в 1024 элемента из инпут буфера
                _InputBuffer.CopyTo(0, _FrameBuffer, 0, FrameSize);
                _SpectrumFramesCount++;
                // буфер приходит раз в 100 мс, в одном буфере около 9 кадров, следовательно ~90 кадров в сек,
                // жестко ограничиваем что за 1 буфер отрисовываем только 1 раз => 10 кадров в сек
                if (_SpectrumFramesCount > 6)
                {
                    CalculateSpectrumForFrame(_FrameBuffer);
                    _SpectrumFramesCount = 0;
                }
                // добавляем окно в кусок элементов
                for (int i = 0; i < FrameSize; i++)
                {
                    _FrameBuffer[i] *= (float)unchecked(FastFourierTransform.HannWindow(i, FrameSize));
                }
                for (int i = 0; i < FrameSize; i++)
                {
                    _FFTBuffer[i].X = _FrameBuffer[i];
                    _FFTBuffer[i].Y = 0;
                }
                // обрабатываем массив комплексных чисел для перевода спектра из время/частоты в амплитуды/частоты
                FastFourierTransform.FFT(true, (int)Math.Log2(FrameSize), _FFTBuffer);
                // находим линию которая содержит децибелы и границы частот и если нашлась такая то получаем мультипликатор на который умножаем амплитуду
                for (int i = 0; i < FrameSize; i++)
                {
                    for (int j = 0; j < FrequencyLines.Count; j++)
                    {
                        //TODO самое тяжелое место для цп (20% цп времени от всего приложения)
                        if (FrequencyLines[j].From < i * _FrequencyStep && FrequencyLines[j].To > i * _FrequencyStep)
                        {
                            float gain = (float)unchecked(GetMultiplier(FrequencyLines[j].GainDecibells));
                            _FFTBuffer[i].X *= gain;
                            _FFTBuffer[i].Y *= gain;
                        }
                    }
                }
                // преобразуем обратно из амплитуды/частоты в время/частоты
                FastFourierTransform.FFT(false, (int)Math.Log2(FrameSize), _FFTBuffer);
                // записываем в выходной буфер массив после преобразования бпф и перекрытие
                for (int i = 0; i < FrameSize; i++)
                {
                    _IFFTBuffer[i] = _FFTBuffer[i].X + _OverlapBuffer[i];
                }
                // закидываем второй кусок из выходного буфера в буфер для перекрытия
                Array.Copy(_IFFTBuffer, HopSize, _OverlapBuffer, 0, HopSize);
                // записываем в массив который будет рендериться на устройстве первый кусок (второй кусок будет в некст куске для перекрытия)
                for (int i = 0; i < HopSize; i++)
                {
                    _OutputSamples.Add(_IFFTBuffer[i]);
                }
                // чистим инпут буффер для получения некст данных
                _InputBuffer.RemoveRange(0, HopSize);
            }
            int outSize = _OutputSamples.Count * (_OutDevice.OutputWaveFormat.BitsPerSample / 8);
            byte[] outBuffer = ArrayPool<byte>.Shared.Rent(outSize);
            ConvertFloatToBytes(_OutputSamples, outBuffer, outSize, _OutDevice.OutputWaveFormat);
            _OutputSamples.Clear();
            _BufferedWaveProvider.AddSamples(outBuffer, 0, outSize);
            ArrayPool<byte>.Shared.Return(outBuffer, true);

        }
        /// <summary>
        /// Рассчитывает значения для фрейма из спектра спектра
        /// </summary>
        private void CalculateSpectrumForFrame(float[] frame)
        {
            // создаем буфер БПФ из пула массивов
            var fftBuffer = ArrayPool<Complex>.Shared.Rent(FrameSize);
            var spectrum = new float[HopSize];
            // копируем данные
            for (int i = 0; i < spectrum.Length; i++)
            {
                fftBuffer[i].X = frame[i];
                fftBuffer[i].Y = 0;
            }
            // прогоняем БПФ
            FastFourierTransform.FFT(true, (int)Math.Log2(FrameSize), fftBuffer);
            // вычисляем амплитуды и нормализуем
            for (int i = 0; i < HopSize; i++)
            {
                float magnitude = MathF.Sqrt((fftBuffer[i].X * fftBuffer[i].X) + (fftBuffer[i].Y * fftBuffer[i].Y));
                float dbValue = 20 * MathF.Log10(magnitude + 1e-10f);
                spectrum[i] = Math.Clamp((dbValue + 100f) / 100f, 0f, 1f);
            }
            // освобождаем буфер обратно в пул
            ArrayPool<Complex>.Shared.Return(fftBuffer);
            // дергаем ивент 
            SpectrumCalcucatedHandler?.Invoke(spectrum);

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
        private static void ConvertFloatToBytes(List<float> samples, byte[] outSamples, int size, WaveFormat waveFormat)
        {
            if (waveFormat.BitsPerSample == 16)
            {
                Span<byte> sampleBytes = stackalloc byte[4];
                for (int i = 0; i < samples.Count; i++)
                {
                    float clampedSample = Math.Clamp(samples[i], -1f, 1f);
                    BitConverter.TryWriteBytes(sampleBytes, (short)clampedSample * short.MaxValue);
                    outSamples[i * 2] = sampleBytes[0];
                    outSamples[(i * 2) + 1] = sampleBytes[1];
                }
            }
            else if (waveFormat.BitsPerSample == 32)
            {
                Span<byte> sampleBytes = stackalloc byte[4];
                for (int i = 0; i < samples.Count; i++)
                {
                    if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        float clampedSample = Math.Clamp(samples[i], -1f, 1f);
                        BitConverter.TryWriteBytes(sampleBytes, clampedSample);
                    }
                    else
                    {
                        float clampedSample = Math.Clamp(samples[i], -1f, 1f);
                        sampleBytes = BitConverter.GetBytes(clampedSample * int.MaxValue);
                    }
                    for (int j = 0; j < 4; j++)
                    {
                        outSamples[(i * 4) + j] = sampleBytes[j];
                    }
                }
            }
        }
        /// <summary>
        /// Преобразует byte[] в float[] учитывая формат аудио
        /// </summary>
        private static void ConvertBytesToFloats(byte[] buffer, float[] outSamples, int size, WaveFormat format)
        {
            if (format.BitsPerSample == 16)
            {
                for (int i = 0; i < size; i++)
                {
                    outSamples[i] = BitConverter.ToInt16(buffer, i * 2) / (float)short.MaxValue;
                }
            }
            else if (format.BitsPerSample == 32)
            {
                for (int i = 0; i < size; i++)
                {
                    if (format.Encoding == WaveFormatEncoding.IeeeFloat)
                    {

                        outSamples[i] = BitConverter.ToSingle(buffer, i * 4);
                    }
                    else
                    {
                        int sample = BitConverter.ToInt32(buffer, i * 4);
                        outSamples[i] = sample / (float)int.MaxValue;
                    }
                }
            }
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
        /// <summary>
        /// Меняет громкость устройства рендера(выходного)
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
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
            _FrameBuffer = new float[FrameSize];
            _FFTBuffer = new Complex[FrameSize];
            _IFFTBuffer = new float[FrameSize];
            _OverlapBuffer = new float[FrameSize];
            _OutputSamples = new(10_000);
            _InputBuffer = new(10_000);
            FrequencyLines = [];
            _DefaultLine = new(0, 0);
            _ProcessingQueue = new();
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
