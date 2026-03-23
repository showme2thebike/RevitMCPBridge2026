using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using WpfColor     = System.Windows.Media.Color;
using WpfEllipse   = System.Windows.Shapes.Ellipse;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SnakeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var handler = new SpinViewHandler();
                var spinEvent = ExternalEvent.Create(handler);
                var win = new SnakeWindow(spinEvent, handler);
                win.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("BIM Monkey", $"Snake error: {ex.Message}");
                return Result.Succeeded;
            }
        }
    }

    // ── Revit spin handler ─────────────────────────────────────────────────────

    internal class SpinViewHandler : IExternalEventHandler
    {
        public double AngleDegrees { get; set; } = 0.0;

        private bool _initialized = false;
        private XYZ _target;
        private double _radius;
        private double _height;

        public void Execute(UIApplication app)
        {
            try
            {
                var uidoc = app.ActiveUIDocument;
                if (uidoc == null) return;

                var view3d = uidoc.ActiveView as View3D;
                if (view3d == null || view3d.IsTemplate) return;

                if (!_initialized)
                {
                    var o = view3d.GetOrientation();
                    // Estimate look-at point 80 feet along forward direction
                    var dist = 80.0;
                    _target = o.EyePosition + o.ForwardDirection.Multiply(dist);
                    var dx = o.EyePosition.X - _target.X;
                    var dy = o.EyePosition.Y - _target.Y;
                    _radius = Math.Sqrt(dx * dx + dy * dy);
                    if (_radius < 10) _radius = 80;
                    _height = o.EyePosition.Z;
                    AngleDegrees = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    _initialized = true;
                }

                double rad = AngleDegrees * Math.PI / 180.0;
                var eye = new XYZ(
                    _target.X + _radius * Math.Cos(rad),
                    _target.Y + _radius * Math.Sin(rad),
                    _height);

                var forward = (_target - eye).Normalize();
                var worldUp = new XYZ(0, 0, 1);
                var right = forward.CrossProduct(worldUp);
                if (right.GetLength() < 1e-6)
                    right = new XYZ(1, 0, 0);
                else
                    right = right.Normalize();
                var up = right.CrossProduct(forward).Normalize();

                using (var t = new Transaction(uidoc.Document, "Spin 3D View"))
                {
                    t.Start();
                    view3d.SetOrientation(new ViewOrientation3D(eye, up, forward));
                    t.Commit();
                }
            }
            catch { /* non-critical */ }
        }

        public string GetName() => "Spin 3D View";

        public void Reset() { _initialized = false; }
    }

    // ── Snake game window ──────────────────────────────────────────────────────

    internal class SnakeWindow : Window
    {
        private const int CellSize = 14;
        private const int Cols = 42;
        private const int Rows = 30;

        private readonly ExternalEvent _spinEvent;
        private readonly SpinViewHandler _spinHandler;
        private readonly DispatcherTimer _spinTimer;

        private Canvas _canvas;
        private TextBlock _scoreText;
        private DispatcherTimer _gameTimer;

        private List<(int x, int y)> _snake;
        private (int dx, int dy) _dir;
        private (int x, int y) _food;
        private int _score;
        private bool _gameOver;
        private bool _started;
        private readonly Random _rng = new Random();

        public SnakeWindow(ExternalEvent spinEvent, SpinViewHandler spinHandler)
        {
            _spinEvent = spinEvent;
            _spinHandler = spinHandler;

            Title = "BIM Monkey — Snake";
            Width = Cols * CellSize + 32;
            Height = Rows * CellSize + 80;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(WpfColor.FromRgb(10, 10, 20));

            var outer = new DockPanel();

            // Header bar
            var header = new Border
            {
                Background = new SolidColorBrush(Colors.Black),
                Padding = new Thickness(10, 5, 10, 5)
            };
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
            var titleText = new TextBlock
            {
                Text = "SNAKE  ·  ← → to turn  ·  spinning your Revit model",
                Foreground = new SolidColorBrush(WpfColor.FromRgb(200, 200, 200)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            _scoreText = new TextBlock
            {
                Text = "Score: 0",
                Foreground = new SolidColorBrush(WpfColor.FromRgb(80, 210, 80)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(18, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerRow.Children.Add(titleText);
            headerRow.Children.Add(_scoreText);
            header.Child = headerRow;
            DockPanel.SetDock(header, Dock.Top);
            outer.Children.Add(header);

            _canvas = new Canvas
            {
                Width = Cols * CellSize,
                Height = Rows * CellSize,
                Background = new SolidColorBrush(WpfColor.FromRgb(10, 10, 20)),
                Focusable = true
            };
            outer.Children.Add(_canvas);
            Content = outer;

            Loaded += (s, e) =>
            {
                _canvas.Focus();
                InitGame();
                DrawStartOverlay();
            };

            KeyDown += OnKeyDown;

            Closed += (s, e) =>
            {
                _gameTimer?.Stop();
                _spinTimer?.Stop();
            };

            // Revit spin: 2° per tick, 80ms interval = ~15s per revolution
            _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            _spinTimer.Tick += (s, e) =>
            {
                _spinHandler.AngleDegrees = (_spinHandler.AngleDegrees + 2.0) % 360.0;
                _spinEvent.Raise();
            };
            _spinTimer.Start();
        }

        private void InitGame()
        {
            _snake = new List<(int, int)>
            {
                (Cols / 2,     Rows / 2),
                (Cols / 2 - 1, Rows / 2),
                (Cols / 2 - 2, Rows / 2)
            };
            _dir = (1, 0);
            _score = 0;
            _gameOver = false;
            _started = false;
            _scoreText.Text = "Score: 0";
            PlaceFood();
            DrawFrame();
        }

        private void DrawStartOverlay()
        {
            var msg = new TextBlock
            {
                Text = "Press ← or → to start",
                Foreground = new SolidColorBrush(WpfColor.FromRgb(180, 180, 180)),
                FontSize = 15,
                FontWeight = FontWeights.Light
            };
            Canvas.SetLeft(msg, Cols * CellSize / 2.0 - 100);
            Canvas.SetTop(msg, Rows * CellSize / 2.0 - 10);
            _canvas.Children.Add(msg);
        }

        private void PlaceFood()
        {
            var occupied = new HashSet<(int, int)>(_snake);
            (int x, int y) pos;
            int tries = 0;
            do { pos = (_rng.Next(Cols), _rng.Next(Rows)); tries++; }
            while (occupied.Contains(pos) && tries < 500);
            _food = pos;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_gameOver)
            {
                _spinHandler.Reset();
                InitGame();
                DrawStartOverlay();
                return;
            }

            if (e.Key == Key.Left)
            {
                // Turn left relative to heading: (dx,dy) -> (dy, -dx)
                _dir = (_dir.dy, -_dir.dx);
                if (!_started) StartGame();
            }
            else if (e.Key == Key.Right)
            {
                // Turn right relative to heading: (dx,dy) -> (-dy, dx)
                _dir = (-_dir.dy, _dir.dx);
                if (!_started) StartGame();
            }
        }

        private void StartGame()
        {
            _started = true;
            _gameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(130) };
            _gameTimer.Tick += (s, e) => Tick();
            _gameTimer.Start();
        }

        private void Tick()
        {
            var head = _snake[0];
            var next = (head.x + _dir.dx, head.y + _dir.dy);

            // Wall collision
            if (next.Item1 < 0 || next.Item1 >= Cols || next.Item2 < 0 || next.Item2 >= Rows)
            {
                DoGameOver();
                return;
            }

            // Self collision (skip tail tip since it will move)
            for (int i = 0; i < _snake.Count - 1; i++)
            {
                if (_snake[i] == next) { DoGameOver(); return; }
            }

            _snake.Insert(0, next);

            bool ateFood = (next == _food);
            if (ateFood)
            {
                _score++;
                _scoreText.Text = $"Score: {_score}";
                PlaceFood();
                // Speed up slightly (min 60ms)
                if (_gameTimer.Interval.TotalMilliseconds > 62)
                    _gameTimer.Interval = TimeSpan.FromMilliseconds(
                        _gameTimer.Interval.TotalMilliseconds - 3);
            }
            else
            {
                _snake.RemoveAt(_snake.Count - 1);
            }

            DrawFrame();
        }

        private void DrawFrame()
        {
            _canvas.Children.Clear();

            // Subtle grid
            var gridPen = new Pen(new SolidColorBrush(WpfColor.FromArgb(18, 255, 255, 255)), 0.5);
            for (int x = 0; x <= Cols; x++)
                _canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = x * CellSize, Y1 = 0,
                    X2 = x * CellSize, Y2 = Rows * CellSize,
                    Stroke = gridPen.Brush, StrokeThickness = 0.5
                });
            for (int y = 0; y <= Rows; y++)
                _canvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = 0,           Y1 = y * CellSize,
                    X2 = Cols * CellSize, Y2 = y * CellSize,
                    Stroke = gridPen.Brush, StrokeThickness = 0.5
                });

            // Food — glowing yellow circle
            var glow = new RadialGradientBrush();
            glow.GradientStops.Add(new GradientStop(WpfColor.FromRgb(255, 240, 80), 0));
            glow.GradientStops.Add(new GradientStop(WpfColor.FromRgb(220, 150, 0), 0.7));
            glow.GradientStops.Add(new GradientStop(WpfColor.FromArgb(0, 180, 80, 0), 1));
            var foodEl = new WpfEllipse
            {
                Width = CellSize + 2,
                Height = CellSize + 2,
                Fill = glow
            };
            Canvas.SetLeft(foodEl, _food.x * CellSize - 1);
            Canvas.SetTop(foodEl, _food.y * CellSize - 1);
            _canvas.Children.Add(foodEl);

            // Snake segments
            for (int i = _snake.Count - 1; i >= 0; i--)
            {
                var seg = _snake[i];
                bool isHead = i == 0;
                byte g = isHead ? (byte)230 : (byte)Math.Max(55, 170 - i * 3);
                byte b = isHead ? (byte)90 : (byte)40;
                byte r = isHead ? (byte)30 : (byte)20;

                var rect = new WpfRectangle
                {
                    Width  = CellSize - 1,
                    Height = CellSize - 1,
                    Fill   = new SolidColorBrush(WpfColor.FromRgb(r, g, b)),
                    RadiusX = isHead ? 4 : 2,
                    RadiusY = isHead ? 4 : 2
                };
                Canvas.SetLeft(rect, seg.x * CellSize);
                Canvas.SetTop(rect,  seg.y * CellSize);
                _canvas.Children.Add(rect);

                // Head eyes
                if (isHead)
                {
                    double ex1, ey1, ex2, ey2;
                    if (_dir == (1, 0))       { ex1 = 10; ey1 = 3;  ex2 = 10; ey2 = 9; }
                    else if (_dir == (-1, 0)) { ex1 = 2;  ey1 = 3;  ex2 = 2;  ey2 = 9; }
                    else if (_dir == (0, -1)) { ex1 = 3;  ey1 = 2;  ex2 = 9;  ey2 = 2; }
                    else                      { ex1 = 3;  ey1 = 10; ex2 = 9;  ey2 = 10; }

                    foreach (var (ox, oy) in new[] { (ex1, ey1), (ex2, ey2) })
                    {
                        var eye = new WpfEllipse { Width = 2.5, Height = 2.5, Fill = Brushes.White };
                        Canvas.SetLeft(eye, seg.x * CellSize + ox - 1);
                        Canvas.SetTop(eye,  seg.y * CellSize + oy - 1);
                        _canvas.Children.Add(eye);
                    }
                }
            }
        }

        private void DoGameOver()
        {
            _gameTimer?.Stop();
            _gameOver = true;
            DrawFrame();

            var over = new TextBlock
            {
                Text = $"GAME OVER  ·  Score: {_score}\nPress any key to restart",
                Foreground = new SolidColorBrush(WpfColor.FromRgb(255, 70, 70)),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                LineHeight = 24
            };
            Canvas.SetLeft(over, Cols * CellSize / 2.0 - 135);
            Canvas.SetTop(over,  Rows * CellSize / 2.0 - 22);
            _canvas.Children.Add(over);
        }
    }
}
