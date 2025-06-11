using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.VisualBasic;

namespace Equalizer.Models
{
    public partial class FrequencyLine : ObservableObject
    {
        public double From { get; private set; }
        public double To { get; private set; }
        [ObservableProperty]
        private int _GainDecibells;
        public FrequencyLine(double from, double to)
        {
            From = from;
            To = to;
        }
    }
}
