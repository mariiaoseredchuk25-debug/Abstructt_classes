using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace InterpolationDemo
{
    public struct PointD
    {
        public double X { get; set; }
        public double Y { get; set; }
        public PointD(double x, double y) { X = x; Y = y; }
    }

    public abstract class Interpolation
    {
        public abstract double Calculate(IReadOnlyList<PointD> points, double x);
    }

    public sealed class LinearInterpolation : Interpolation
    {
        public override double Calculate(IReadOnlyList<PointD> points, double x)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (x >= points[i].X && x <= points[i + 1].X)
                {
                    double t = (x - points[i].X) / (points[i + 1].X - points[i].X);
                    return points[i].Y + t * (points[i + 1].Y - points[i].Y);
                }
            }
            return double.NaN;
        }
    }

    public sealed class HermiteInterpolation : Interpolation
    {
        public override double Calculate(IReadOnlyList<PointD> points, double x)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (x >= points[i].X && x <= points[i + 1].X)
                    return InterpolateSegment(points, i, x);
            }
            return double.NaN;
        }

        private double InterpolateSegment(IReadOnlyList<PointD> points, int i, double x)
        {
            double x0 = points[i].X, x1 = points[i + 1].X;
            double y0 = points[i].Y, y1 = points[i + 1].Y;
            double dx = x1 - x0;
            double t = (x - x0) / dx;

            double m0 = Derivative(points, i);
            double m1 = Derivative(points, i + 1);

            double h00 = 2 * t * t * t - 3 * t * t + 1;
            double h10 = t * t * t - 2 * t * t + t;
            double h01 = -2 * t * t * t + 3 * t * t;
            double h11 = t * t * t - t * t;

            double y = h00 * y0 + h10 * dx * m0 + h01 * y1 + h11 * dx * m1;
            return Clamp(y, y0, y1);
        }

        private double Derivative(IReadOnlyList<PointD> points, int i)
        {
            if (i == 0)
                return (points[1].Y - points[0].Y) / (points[1].X - points[0].X);
            if (i == points.Count - 1)
                return (points[i].Y - points[i - 1].Y) / (points[i].X - points[i - 1].X);
            return (points[i + 1].Y - points[i - 1].Y) / (points[i + 1].X - points[i - 1].X);
        }

        private double Clamp(double y, double y0, double y1)
        {
            double min = Math.Min(y0, y1);
            double max = Math.Max(y0, y1);
            return Math.Max(min, Math.Min(max, y));
        }
    }

    public class MainViewModel
    {
        public ObservableCollection<PointD> A1 { get; } = new();
        public ObservableCollection<PointD> A2Linear { get; } = new();
        public ObservableCollection<PointD> A2Hermite { get; } = new();
        public ObservableCollection<PointD> Reference { get; } = new();

        public bool ShowLinear { get; set; } = true;
        public bool ShowHermite { get; set; } = true;
        public bool ShowReference { get; set; } = true;

        public double H1 { get; set; } = 1.0;
        public double H2 { get; set; } = 0.1;

        public void Generate()
        {
            ClearAll();
            TabulateFunction();
            InterpolateAll();
        }

        private void ClearAll()
        {
            A1.Clear(); A2Linear.Clear(); A2Hermite.Clear(); Reference.Clear();
        }

        private void TabulateFunction()
        {
            for (double x = 0; x <= 2 * Math.PI; x += H1)
                A1.Add(new PointD(x, Math.Sin(x)));
        }

        private void InterpolateAll()
        {
            var interpolations = new Interpolation[]
            {
                new LinearInterpolation(),
                new HermiteInterpolation()
            };

            for (double x = 0; x <= 2 * Math.PI; x += H2)
            {
                Reference.Add(new PointD(x, Math.Sin(x)));
                A2Linear.Add(new PointD(x, interpolations[0].Calculate(A1, x)));
                A2Hermite.Add(new PointD(x, interpolations[1].Calculate(A1, x)));
            }
        }

        public void LoadFromJson(string path)
        {
            var json = File.ReadAllText(path);
            var pts = JsonSerializer.Deserialize<List<PointD>>(json);
            A1.Clear();
            foreach (var p in pts) A1.Add(p);
        }
    }

    public partial class MainWindow : Window
    {
        private MainViewModel vm = new MainViewModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = vm;
            vm.Generate();
            Draw();
        }

        private void Draw()
        {
            canvas.Children.Clear();
            if (vm.ShowReference) DrawPolyline(vm.Reference, Brushes.Green);
            if (vm.ShowLinear) DrawPolyline(vm.A2Linear, Brushes.Red);
            if (vm.ShowHermite) DrawPolyline(vm.A2Hermite, Brushes.Blue);
            DrawPoints(vm.A1, Brushes.Black);
        }

        private void DrawPolyline(IEnumerable<PointD> pts, Brush brush)
        {
            var polyline = new Polyline { Stroke = brush, StrokeThickness = 2 };
            foreach (var p in pts)
                polyline.Points.Add(new Point(p.X * 50, 100 - p.Y * 50));
            canvas.Children.Add(polyline);
        }

        private void DrawPoints(IEnumerable<PointD> pts, Brush brush)
        {
            foreach (var p in pts)
            {
                var ellipse = new Ellipse { Width = 4, Height = 4, Fill = brush };
                Canvas.SetLeft(ellipse, p.X * 50);
                Canvas.SetTop(ellipse, 100 - p.Y * 50);
                canvas.Children.Add(ellipse);
            }
        }

        private void CheckBoxChanged(object sender, RoutedEventArgs e) => Draw();
    }
}
