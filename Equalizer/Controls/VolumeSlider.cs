using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Equalizer.Controls
{
    internal class VolumeSlider: Control
    {
        private Avalonia.Point _center;
        private double _radius;
        private int _value;
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            _center = new Avalonia.Point(Bounds.Width / 2, Bounds.Height - 20);
            _radius = Math.Min(Bounds.Width, Bounds.Height * 2) / 2 - 30;
            var geometry = new PathGeometry();
            var figure = new PathFigure() { StartPoint=GetPointOnCircle(180)};
            figure.Segments.Add(new ArcSegment() 
            {
                IsLargeArc = false,
                Size = new Avalonia.Size(_radius, _radius),
                SweepDirection = SweepDirection.Clockwise,
                Point = GetPointOnCircle(360)
            });
            geometry.Figures.Add(figure);
            var pen = new Pen(new SolidColorBrush(Colors.Black,4),4);
            context.DrawGeometry(null, pen,geometry);
        }
        
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var point = e.GetPosition(this);
            if (IsPointOnCircle(point, 180, 360, _radius, 4))
            {
                _value = CalculateSliderValue(point);
            }
        }
        private int CalculateSliderValue(Avalonia.Point point) 
        {
            double dx = point.X - _center.X;
            double dy = point.Y - _center.Y;
            double angle = Math.Abs(Math.Atan2(dy, dx) * 180 / Math.PI);
            if (angle < 0)
                angle += 360;
            return 100 - (int)(angle / 180 * 100);
        }
        private Avalonia.Point GetPointOnCircle(double angleDegrees)
        {
            var angleRadians = angleDegrees * Math.PI / 180;
            var x = _center.X + _radius * Math.Cos(angleRadians);
            var y = _center.Y + _radius * Math.Sin(angleRadians);
            return new Avalonia.Point(x, y);
        }
        private bool IsPointOnCircle(Avalonia.Point point, double startAngleDeg, double endAngleDeg, double radius, double thickness)
        {
            // Вектор от центра к точке
            double dx = point.X - _center.X;
            double dy = point.Y - _center.Y;

            // Расстояние от центра
            double distance = Math.Sqrt(dx * dx + dy * dy);

            // Проверка попадания в толщину дуги (допускаем +/- половину толщины)
            double halfThickness = thickness / 2;
            if (distance < radius - halfThickness || distance > radius + halfThickness)
                return false;

            // Вычисляем угол точки
            double angle = Math.Atan2(dy, dx) * 180 / Math.PI; // угол в градусах
            if (angle < 0)
                angle += 360;

            // Проверка попадания в дугу от startAngle до endAngle
            if (startAngleDeg < endAngleDeg)
            {
                return angle >= startAngleDeg && angle <= endAngleDeg;
            }
            else // если дуга через 0 (например, от 300° до 60°)
            {
                return angle >= startAngleDeg || angle <= endAngleDeg;
            }
        }
    }
}
