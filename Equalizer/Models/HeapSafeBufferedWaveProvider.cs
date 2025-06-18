using NAudio.Utils;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Equalizer.Models
{
    public class HeapSafeBufferedWaveProvider : IWaveProvider
    {

        //TODO переписать провайдер под оптимизацию памяти
        private CircularBuffer circularBuffer;
        private byte[] _Buffer;
        private object _BufferLock;
        private readonly WaveFormat waveFormat;
        public bool ReadFully { get; set; }
        public int BufferLength { get; set; }
        public TimeSpan BufferDuration
        {
            get
            {
                return TimeSpan.FromSeconds((double)BufferLength / (double)WaveFormat.AverageBytesPerSecond);
            }
            set
            {
                BufferLength = (int)(value.TotalSeconds * (double)WaveFormat.AverageBytesPerSecond);
            }
        }
        public bool DiscardOnBufferOverflow { get; set; }
        public int BufferedBytes
        {
            get
            {
                if (circularBuffer != null)
                {
                    return circularBuffer.Count;
                }

                return 0;
            }
        }
        public TimeSpan BufferedDuration => TimeSpan.FromSeconds((double)BufferedBytes / (double)WaveFormat.AverageBytesPerSecond);
        public WaveFormat WaveFormat => waveFormat;
        public HeapSafeBufferedWaveProvider(WaveFormat waveFormat)
        {
            this.waveFormat = waveFormat;
            BufferLength = waveFormat.AverageBytesPerSecond * 5;
            ReadFully = true;
            _BufferLock = new();
        }
        public void AddSamples(byte[] buffer, int offset, int count)
        {
            if (circularBuffer == null)
            {
                circularBuffer = new CircularBuffer(BufferLength);
            }

            if (circularBuffer.Write(buffer, offset, count) < count && !DiscardOnBufferOverflow)
            {
                throw new InvalidOperationException("Buffer full");
            }
        }
        public int Read(byte[] buffer, int offset, int count)
        {
            int num = 0;
            if (circularBuffer != null)
            {
                num = circularBuffer.Read(buffer, offset, count);
            }

            if (ReadFully && num < count)
            {
                Array.Clear(buffer, offset + num, count - num);
                num = count;
            }

            return num;
        }
        public void ClearBuffer()
        {
            if (circularBuffer != null)
            {
                circularBuffer.Reset();
            }
        }
    }
}
