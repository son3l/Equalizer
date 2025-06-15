using System;
using System.Collections.Generic;

namespace Equalizer.Models
{
    public class SpectrumDataEventArgs : EventArgs
    {
        public List<float> SpectrumData { get; set; }
        public SpectrumDataEventArgs() => SpectrumData = [];
        public SpectrumDataEventArgs(List<float> data) => SpectrumData = data;
    }
}
