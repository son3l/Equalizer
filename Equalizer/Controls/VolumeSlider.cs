using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using System;

namespace Equalizer.Controls
{
    internal class VolumeSlider : Control
    {
        private Avalonia.Point _CenterPoint;
        private double _Radius;
        private bool _IsDragging;
        /// <summary>
        /// Кисть для задней дуги
        /// </summary>
        public static readonly StyledProperty<IBrush> BackgroundBarBrushProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, IBrush>(
                nameof(BackgroundBarBrush),
                new SolidColorBrush(Colors.Black));
        public IBrush BackgroundBarBrush
        {
            get => GetValue(BackgroundBarBrushProperty);
            set => SetValue(BackgroundBarBrushProperty, value);
        }
        /// <summary>
        /// Кисть для передней дуге (дуга значения)
        /// </summary>
        public static readonly StyledProperty<IBrush> FrontBarBrushProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, IBrush>(
                nameof(FrontBarBrush),
                new SolidColorBrush(Colors.LightBlue));
        public IBrush FrontBarBrush
        {
            get => GetValue(FrontBarBrushProperty);
            set => SetValue(FrontBarBrushProperty, value);
        }
        /// <summary>
        /// Кисть для текста значения
        /// </summary>
        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, IBrush>(
                nameof(Foreground),
                new SolidColorBrush(Colors.Black));
        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }
        /// <summary>
        /// Значение слайдера
        /// </summary>
        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, double>(
                nameof(Value),
                100,
                defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public double Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        /// <summary>
        /// Размер текста
        /// </summary>
        public static readonly StyledProperty<double> FontSizeProperty =
            AvaloniaProperty.Register<SpectrumVisualizer, double>(
                nameof(FontSize),
                10);
        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }
        public VolumeSlider() { }
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            //фоновая дуга
            DrawCircle(context, BackgroundBarBrush, 180, 360);
            //дуга значения
            DrawCircle(context, FrontBarBrush, 180, CalculateSliderAngle(Value));
            //кружок за который тянем
            DrawThumb(context);
            //текст значения
            DrawValueText(context);
        }
        private void DrawThumb(DrawingContext context)
        {
            var thumb = GetPointOnCircleFromAngle(CalculateSliderAngle(Value));
            context.DrawEllipse(new SolidColorBrush(Colors.WhiteSmoke, BackgroundBarBrush.Opacity), null, thumb, 10, 10);
            context.DrawEllipse(FrontBarBrush, null, thumb, 5, 5);
        }
        private void DrawValueText(DrawingContext context)
        {
            var text = new FormattedText(Value.ToString(),
                                  new System.Globalization.CultureInfo("ru-RU"),
                                  FlowDirection.LeftToRight,
                                  new Typeface("Segoe UI"),
                                  FontSize,
                                  Foreground);
            double textX = _CenterPoint.X - text.Width / 2;
            double textY = _CenterPoint.Y - text.Height / 2;
            context.DrawText(
                text,
                new Avalonia.Point(textX, textY));
        }
        private void DrawCircle(DrawingContext context, IBrush brush, double startDegrees, double endDegrees)
        {
            _CenterPoint = new Avalonia.Point(Bounds.Width / 2, Bounds.Height - 20);
            _Radius = Math.Min(Bounds.Width, Bounds.Height * 2) / 2 - 30;
            var geometry = new PathGeometry();
            var figure = new PathFigure() { StartPoint = GetPointOnCircle(startDegrees), IsClosed = false };
            figure.Segments.Add(new ArcSegment()
            {
                IsLargeArc = false,
                Size = new Avalonia.Size(_Radius, _Radius),
                SweepDirection = SweepDirection.Clockwise,
                Point = GetPointOnCircle(endDegrees)
            });
            geometry.Figures.Add(figure);
            var pen = new Pen(brush, brush.Opacity);
            context.DrawGeometry(null, pen, geometry);
        }
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var point = e.GetPosition(this);
            if (IsPointOnCircle(point, 180, 360, _Radius, BackgroundBarBrush.Opacity))
            {
                Value = CalculateSliderValue(point);
                InvalidateVisual();
            }
            _IsDragging = !_IsDragging;
        }
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _IsDragging = false;
        }
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_IsDragging)
            {
                var point = e.GetPosition(this);
                if (IsPointOnCircle(point, 180, 360, _Radius, BackgroundBarBrush.Opacity))
                {
                    Value = CalculateSliderValue(point);
                    InvalidateVisual();
                }
            }
        }
        private Avalonia.Point GetPointOnCircleFromAngle(double angleDegrees)
        {
            double angleRadians = angleDegrees * Math.PI / 180;
            double x = _CenterPoint.X + _Radius * Math.Cos(angleRadians);
            double y = _CenterPoint.Y + _Radius * Math.Sin(angleRadians);

            return new Avalonia.Point(x, y);
        }
        private int CalculateSliderValue(Avalonia.Point point)
        {
            double dx = point.X - _CenterPoint.X;
            double dy = point.Y - _CenterPoint.Y;
            double angle = Math.Abs(Math.Atan2(dy, dx) * 180 / Math.PI);
            if (angle < 0)
                angle += 360;
            return 100 - (int)(angle / 180 * 100);
        }
        private double CalculateSliderAngle(double value)
        {
            return 180 + (value / 100.0) * 180;
        }
        private Avalonia.Point GetPointOnCircle(double angleDegrees)
        {
            var angleRadians = angleDegrees * Math.PI / 180;
            var x = _CenterPoint.X + _Radius * Math.Cos(angleRadians);
            var y = _CenterPoint.Y + _Radius * Math.Sin(angleRadians);
            return new Avalonia.Point(x, y);
        }
        private bool IsPointOnCircle(Avalonia.Point point, double startAngleDeg, double endAngleDeg, double radius, double thickness, double tolerance = 10)
        {
            // Расстояние до центра
            double dx = point.X - _CenterPoint.X;
            double dy = point.Y - _CenterPoint.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            // Половина визуальной толщины дуги + допуск
            double lowerBound = radius - thickness - tolerance;
            double upperBound = radius + thickness + tolerance;

            // Проверка попадания по расстоянию
            if (distance < lowerBound || distance > upperBound)
                return false;

            // Угол от центра до точки
            double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
            if (angle < 0)
                angle += 360;

            // Угол должен быть в пределах от startAngle до endAngle
            if (startAngleDeg < endAngleDeg)
            {
                return angle >= startAngleDeg && angle <= endAngleDeg;
            }
            else
            {
                // если дуга "пересекает 0°", например от 300 до 60
                return angle >= startAngleDeg || angle <= endAngleDeg;
            }
        }
    }
}
