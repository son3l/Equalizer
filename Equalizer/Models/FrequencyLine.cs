using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Equalizer.Models
{
    public partial class FrequencyLine(double from, double to) : ObservableObject
    {
        /// <summary>
        /// С какой частоты обрабатывать сигнал
        /// </summary>
        public double From { get; private set; } = from;
        /// <summary>
        /// По какую частоту обрабатывать сигнал
        /// </summary>
        public double To { get; private set; } = to;
        private string? _Name;
        /// <summary>
        /// Представляет собой имя полосы, если не задано передает просто промежуток
        /// </summary>
        public string Name
        {
            get => _Name ?? string.Join(" - ", From, To);
            set => _Name = value;
        }
        /// <summary>
        /// Показатель увеличения или уменьшения амплитуды в децибелах
        /// </summary>
        [ObservableProperty]
        private int _GainDecibells;

        public override bool Equals(object? obj)
        {
            if(obj is null) return false;
            if(obj is not FrequencyLine) return false;
            return (obj as FrequencyLine).From == From && (obj as FrequencyLine).To == To && (obj as FrequencyLine).GainDecibells == GainDecibells;
        }
    }
}
