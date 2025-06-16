using System;
using System.Collections.Generic;

namespace Equalizer.Models
{
    public class SpectrumDataEventArgs : EventArgs
    {
        public float[] SpectrumData { get; set; }
        public SpectrumDataEventArgs() => SpectrumData = [];
        public SpectrumDataEventArgs(float[] data) => SpectrumData = data;
    }
}
