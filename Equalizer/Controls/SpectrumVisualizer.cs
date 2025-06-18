using Avalonia.Controls;
using Avalonia.Media;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Equalizer.Controls
{
    public class SpectrumVisualizer : Control
    {
        /// <summary>
        /// Массив данных спектра
        /// </summary>
        public static readonly StyledProperty<IEnumerable<float>?> SpectrumDataProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, IEnumerable<float>?>(
                nameof(SpectrumData));
        /// <summary>
        /// Кисть для столбиков
        /// </summary>
        public static readonly StyledProperty<IBrush> BarBrushProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, IBrush>(
                nameof(BarBrush),
                new SolidColorBrush(Colors.LimeGreen));
        /// <summary>
        /// Минимальная высота столбиков
        /// </summary>
        public static readonly StyledProperty<double> MinBarHeightProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, double>(
                nameof(MinBarHeight),
                2.0);
        /// <summary>
        /// Размер пропусков между стобликами
        /// </summary>
        public static readonly StyledProperty<double> BarSpacingProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, double>(
                nameof(BarSpacing),
                1.0);
        /// <summary>
        /// Булево вкл и выкл сглаживание изменения высоты столбиков
        /// </summary>
        public static readonly StyledProperty<bool> SmoothingEnabledProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, bool>(
                nameof(SmoothingEnabled),
                true);
        /// <summary>
        /// Размер группировки стобликов (например было 1024, мы поставили GroupSize = 16, значит в итоге будет столбиков 64 с средним значением группы из 16 столбиков)
        /// </summary>
        public static readonly StyledProperty<int> GroupSizeProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, int>(
                nameof(GroupSize),
                4);
        private float[] _smoothedValues = [];
        private const float SmoothingFactor = 0.3f;

        static SpectrumVisualizer()
        {
            AffectsRender<SpectrumVisualizer>(
                SpectrumDataProperty,
                BarBrushProperty,
                MinBarHeightProperty,
                BarSpacingProperty,
                SmoothingEnabledProperty);
        }

        public IEnumerable<float>? SpectrumData
        {
            get => GetValue(SpectrumDataProperty);
            set => SetValue(SpectrumDataProperty, value);
        }

        public IBrush BarBrush
        {
            get => GetValue(BarBrushProperty);
            set => SetValue(BarBrushProperty, value);
        }

        public double MinBarHeight
        {
            get => GetValue(MinBarHeightProperty);
            set => SetValue(MinBarHeightProperty, value);
        }

        public double BarSpacing
        {
            get => GetValue(BarSpacingProperty);
            set => SetValue(BarSpacingProperty, value);
        }

        public bool SmoothingEnabled
        {
            get => GetValue(SmoothingEnabledProperty);
            set => SetValue(SmoothingEnabledProperty, value);
        }
        public int GroupSize
        {
            get => GetValue(GroupSizeProperty);
            set => SetValue(GroupSizeProperty, value);
        }
        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (SpectrumData == null || !SpectrumData.Any())
                return;

            Span<float> aggregatedSpectrum = stackalloc float[SpectrumData.Count()/ GroupSize];
            AggregateSpectrum(SpectrumData.ToArray().AsSpan(), aggregatedSpectrum, GroupSize);
            if (SmoothingEnabled)
            {
                aggregatedSpectrum = ApplySmoothing(aggregatedSpectrum);
            }
            
            DrawSpectrum(context, aggregatedSpectrum);
        }

        private float[] ApplySmoothing(Span<float> newData)
        {
            if (_smoothedValues.Length != newData.Length)
            {
                _smoothedValues = new float[newData.Length];
                Array.Copy(newData.ToArray(), _smoothedValues, newData.Length);
                return _smoothedValues;
            }
            for (int i = 0; i < newData.Length; i++)
            {
                _smoothedValues[i] = _smoothedValues[i] * (1 - SmoothingFactor) +
                                   newData[i] * SmoothingFactor;
            }

            return _smoothedValues;
        }
        private void DrawSpectrum(DrawingContext context, Span<float> data)
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0)
                return;
            double barWidth = (Bounds.Width - (data.Length - 1) * BarSpacing) / data.Length;
            for (int i = 0; i < data.Length; i++)
            {
                double normalizedValue = Math.Clamp(data[i],0,1);
                double barHeight = Math.Max(MinBarHeight, normalizedValue * Bounds.Height);
                double x = i * (barWidth + BarSpacing);
                double y = Bounds.Height - barHeight;
                context.FillRectangle(BarBrush,new Rect(x, y, barWidth, barHeight),3);
            }
        }
        public static void AggregateSpectrum(Span<float> input, Span<float> output, int groupSize)
        {
            int outputLength = input.Length / groupSize;
            for (int i = 0; i < outputLength; i++)
            {
                float sum = 0;
                int start = i * groupSize;
                int end = start + groupSize;
                for (int j = start; j < end; j++)
                {
                    sum += input[j];
                }
                output[i] = sum / groupSize; 
            }
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(
                double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 100 : availableSize.Height);
        }
    }
}
