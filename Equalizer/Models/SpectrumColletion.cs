using System;
using System.Collections.ObjectModel;

namespace Equalizer.Models
{
    public class SpectrumColletion : ObservableCollection<float>
    {
        public SpectrumColletion() : base()
        {
            for (int i = 0; i < 1024; i++)
                Add(0f);
        }
        public void UpdateCollection(Span<float> spectrum)
        {
            for (int i = 0; i < spectrum.Length && i < Count; i++)
            {
                if (spectrum[i] != this[i])
                {
                    SetItem(i, spectrum[i]);
                }
            }
        }
    }
}
