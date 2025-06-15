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
        // Dependency property для коллекции значений спектра
        public static readonly StyledProperty<IEnumerable<float>?> SpectrumDataProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, IEnumerable<float>?>(
                nameof(SpectrumData));

        // Dependency property для цвета полос
        public static readonly StyledProperty<IBrush> BarBrushProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, IBrush>(
                nameof(BarBrush),
                new SolidColorBrush(Colors.LimeGreen));

        // Dependency property для минимальной высоты полос
        public static readonly StyledProperty<double> MinBarHeightProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, double>(
                nameof(MinBarHeight),
                2.0);

        // Dependency property для отступов между полосами
        public static readonly StyledProperty<double> BarSpacingProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, double>(
                nameof(BarSpacing),
                1.0);

        // Dependency property для сглаживания
        public static readonly StyledProperty<bool> SmoothingEnabledProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, bool>(
                nameof(SmoothingEnabled),
                true);

        private float[] _smoothedValues = Array.Empty<float>();
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

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (SpectrumData == null)
                return;

            var data = SpectrumData.ToArray();
            if (data.Length == 0)
                return;

            // Применяем сглаживание если включено
            if (SmoothingEnabled)
            {
                data = ApplySmoothing(data);
            }

            DrawSpectrum(context, data);
        }

        private float[] ApplySmoothing(float[] newData)
        {
            if (_smoothedValues.Length != newData.Length)
            {
                _smoothedValues = new float[newData.Length];
                Array.Copy(newData, _smoothedValues, newData.Length);
                return _smoothedValues;
            }

            for (int i = 0; i < newData.Length; i++)
            {
                _smoothedValues[i] = _smoothedValues[i] * (1 - SmoothingFactor) +
                                   newData[i] * SmoothingFactor;
            }

            return _smoothedValues;
        }

        private void DrawSpectrum(DrawingContext context, float[] data)
        {
            var bounds = Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var barCount = data.Length;
            var totalSpacing = (barCount - 1) * BarSpacing;
            var barWidth = Math.Max(1, (bounds.Width - totalSpacing) / barCount);

            for (int i = 0; i < barCount; i++)
            {
                var normalizedValue = Math.Max(0, Math.Min(1, data[i]));
                var barHeight = Math.Max(MinBarHeight, normalizedValue * bounds.Height);

                var x = i * (barWidth + BarSpacing);
                var y = bounds.Height - barHeight;

                var rect = new Rect(x, y, barWidth, barHeight);
                context.FillRectangle(BarBrush, rect);
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
