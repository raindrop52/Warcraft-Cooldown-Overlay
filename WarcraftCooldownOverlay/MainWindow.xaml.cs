using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WarcraftCooldownOverlay;

public partial class MainWindow : Window
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyToggleOverlay = 4101;
    private const int HotkeyExit = 4102;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(125) };
    private readonly List<SlotModel> _slots = [];
    private OverlayWindow? _overlay;
    private IntPtr _gameWindow;
    private bool _isExiting;
    private readonly PersistedSettings _settings;
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WarcraftCooldownOverlay", "settings.json");

    private static readonly SlotDefinition[] Definitions =
    [
        new("아이템 1", SlotKind.Item, .6656, .8420), new("아이템 2", SlotKind.Item, .7135, .8420),
        new("아이템 3", SlotKind.Item, .6656, .9104), new("아이템 4", SlotKind.Item, .7135, .9104),
        new("아이템 5", SlotKind.Item, .6656, .9695), new("아이템 6", SlotKind.Item, .7135, .9695),
        new("스킬 1", SlotKind.Skill, .8505, .8835), new("스킬 2", SlotKind.Skill, .9047, .8835),
        new("스킬 3", SlotKind.Skill, .9583, .8835),
        new("스킬 4", SlotKind.Skill, .7969, .9538), new("스킬 5", SlotKind.Skill, .8505, .9538),
        new("스킬 6", SlotKind.Skill, .9047, .9538), new("스킬 7", SlotKind.Skill, .9583, .9538),
    ];

    public MainWindow()
    {
        InitializeComponent();
        _settings = LoadSettings();
        HorizontalRadio.IsChecked = !_settings.Vertical;
        VerticalRadio.IsChecked = _settings.Vertical;
        for (var index = 0; index < Definitions.Length; index++)
        {
            var definition = Definitions[index];
            var isSelected = index >= _settings.Selected.Length || _settings.Selected[index];
            var model = CreateSlotCard(definition, isSelected);
            _slots.Add(model);
            if (definition.Kind == SlotKind.Item)
                ItemPanel.Children.Add(model.Card);
            else if (index < 9)
                SkillTopPanel.Children.Add(model.Card);
            else
                SkillBottomPanel.Children.Add(model.Card);
        }

        Loaded += OnLoaded;
        Closed += OnClosed;
        _timer.Tick += (_, _) => RefreshSlots();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsUsablePosition(_settings.SettingsLeft, _settings.SettingsTop))
        {
            Left = _settings.SettingsLeft;
            Top = _settings.SettingsTop;
        }
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WindowProc);
        RegisterHotKey(hwnd, HotkeyToggleOverlay, 0, 0x76); // F7
        RegisterHotKey(hwnd, HotkeyExit, 0, 0x77);          // F8
        _timer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _isExiting = true;
        SaveSettings();
        _timer.Stop();
        _overlay?.Close();
        var hwnd = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(hwnd, HotkeyToggleOverlay);
        UnregisterHotKey(hwnd, HotkeyExit);
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;
        handled = true;
        if (wParam.ToInt32() == HotkeyToggleOverlay)
        {
            if (_overlay is null) StartOverlay(this, new RoutedEventArgs());
            else StopOverlay(this, new RoutedEventArgs());
        }
        else if (wParam.ToInt32() == HotkeyExit)
        {
            Close();
        }
        return IntPtr.Zero;
    }

    private SlotModel CreateSlotCard(SlotDefinition definition, bool isSelected)
    {
        var previewWidth = definition.Kind == SlotKind.Item ? 74 : 64;
        var previewHeight = 64;
        var image = new System.Windows.Controls.Image
        {
            Width = previewWidth, Height = previewHeight, Stretch = Stretch.Fill
        };
        var previewBorder = new Border
        {
            Width = definition.Kind == SlotKind.Item ? previewWidth + 2 : previewWidth,
            Height = definition.Kind == SlotKind.Item ? previewHeight + 2 : previewHeight,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(12, 16, 22)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(65, 78, 94)),
            BorderThickness = new Thickness(1), Child = image
        };
        var toggle = new CheckBox
        {
            Content = definition.Name, IsChecked = isSelected, Foreground = System.Windows.Media.Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0)
        };
        var card = new StackPanel
        {
            Width = definition.Kind == SlotKind.Item ? previewWidth + 2 : previewWidth,
            Margin = new Thickness(0)
        };
        card.Children.Add(previewBorder);
        card.Children.Add(toggle);

        var model = new SlotModel(definition, card, previewBorder, image, toggle);
        toggle.Checked += (_, _) => UpdateOverlaySlots();
        toggle.Checked += (_, _) => SaveSettings();
        toggle.Unchecked += (_, _) => { UpdateOverlaySlots(); SaveSettings(); };
        return model;
    }

    private void RefreshSlots()
    {
        if (_gameWindow == IntPtr.Zero || !IsWindow(_gameWindow)) _gameWindow = FindWarcraftWindow();
        if (_gameWindow == IntPtr.Zero || !TryGetClientBounds(_gameWindow, out var bounds))
        {
            StatusText.Text = "Warcraft III 창을 찾을 수 없음";
            StatusText.Foreground = new SolidColorBrush(Colors.Orange);
            return;
        }

        StatusText.Text = $"게임 연결됨 · {bounds.Width}×{bounds.Height}";
        StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 255, 153));

        foreach (var slot in _slots)
        {
            var scale = Math.Min(bounds.Width / 1920.0, bounds.Height / 1082.0);
            var captureWidth = Math.Max(36, (int)Math.Round(scale *
                (slot.Definition.Kind == SlotKind.Item ? 74 : 64)));
            var captureHeight = Math.Max(36, (int)Math.Round(scale * 64));
            var cx = bounds.Left + (int)Math.Round(bounds.Width * slot.Definition.X);
            var cy = bounds.Top + (int)Math.Round(bounds.Height * slot.Definition.Y);
            if (slot.Definition.Kind == SlotKind.Skill)
                cy += (int)Math.Round(scale * 3); // Final offset: 3 px lower at 1920×1082.
            var source = new System.Drawing.Rectangle(
                cx - captureWidth / 2, cy - captureHeight / 2, captureWidth, captureHeight);
            try
            {
                using var bitmap = new Bitmap(captureWidth, captureHeight,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(source.Left, source.Top, 0, 0, source.Size, CopyPixelOperation.SourceCopy);

                slot.Source = ToBitmapSource(bitmap);
                slot.Cooldown = HasCooldownText(bitmap, slot.Definition.Kind);
                if (slot.Definition.Kind == SlotKind.Item)
                {
                    slot.Preview.Width = captureWidth;
                    slot.Preview.Height = captureHeight;
                    slot.PreviewBorder.Width = captureWidth + 2;
                    slot.PreviewBorder.Height = captureHeight + 2;
                    slot.Card.Width = captureWidth + 2;
                }
                else
                {
                    slot.PreviewBorder.Width = captureWidth;
                    slot.PreviewBorder.Height = captureHeight;
                    slot.Card.Width = captureWidth;
                    slot.Preview.Width = Math.Max(1, captureWidth - 2);
                    slot.Preview.Height = Math.Max(1, captureHeight - 2);
                }
                slot.Preview.Source = slot.Source;
                slot.PreviewBorder.BorderBrush = new SolidColorBrush(slot.Cooldown
                    ? System.Windows.Media.Color.FromRgb(255, 82, 82)
                    : System.Windows.Media.Color.FromRgb(83, 216, 255));
            }
            catch
            {
                // The game can move between coordinate lookup and capture. Retry next tick.
            }
        }
        _overlay?.RefreshImages();
    }

    private void StartOverlay(object sender, RoutedEventArgs e)
    {
        if (_overlay is null)
        {
            _overlay = new OverlayWindow();
            if (IsUsablePosition(_settings.OverlayLeft, _settings.OverlayTop))
            {
                _overlay.Left = _settings.OverlayLeft;
                _overlay.Top = _settings.OverlayTop;
            }
            _overlay.Closed += (_, _) =>
            {
                RememberOverlayPosition();
                _overlay = null;
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                if (!_isExiting) RestoreSettingsWindow();
            };
        }
        UpdateOverlaySlots();
        _overlay.Show();
        _overlay.Activate();
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        Hide();
        SaveSettings();
    }

    private void StopOverlay(object sender, RoutedEventArgs e)
    {
        RememberOverlayPosition();
        _overlay?.Close();
        _overlay = null;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        if (!_isExiting) RestoreSettingsWindow();
    }

    private void RestoreSettingsWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void UpdateOverlaySlots()
    {
        if (_overlay is null) return;
        _overlay.SetSlots(_slots, VerticalRadio.IsChecked == true);
    }

    private void OrientationChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            UpdateOverlaySlots();
            SaveSettings();
        }
    }

    private void SelectAll(object sender, RoutedEventArgs e)
    {
        foreach (var slot in _slots) slot.Toggle.IsChecked = true;
        UpdateOverlaySlots();
    }

    private void ClearAll(object sender, RoutedEventArgs e)
    {
        foreach (var slot in _slots) slot.Toggle.IsChecked = false;
        UpdateOverlaySlots();
    }

    private void RememberOverlayPosition()
    {
        if (_overlay is null) return;
        _settings.OverlayLeft = _overlay.Left;
        _settings.OverlayTop = _overlay.Top;
    }

    private void SaveSettings()
    {
        if (_slots.Count == 0) return;
        RememberOverlayPosition();
        _settings.Selected = _slots.Select(slot => slot.Toggle.IsChecked == true).ToArray();
        _settings.Vertical = VerticalRadio.IsChecked == true;
        if (IsVisible && WindowState == WindowState.Normal)
        {
            _settings.SettingsLeft = Left;
            _settings.SettingsTop = Top;
        }
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_settings,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* A settings write failure must not stop the overlay. */ }
    }

    private static PersistedSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch { /* Use defaults if the file is missing or damaged. */ }
        return new PersistedSettings();
    }

    private static bool IsUsablePosition(double left, double top) =>
        !double.IsNaN(left) && !double.IsInfinity(left) &&
        !double.IsNaN(top) && !double.IsInfinity(top) && left > -2000 && top > -2000;

    private static bool HasCooldownText(Bitmap bitmap, SlotKind kind)
    {
        var brightNeutral = 0;
        var x0 = bitmap.Width / 5;
        var x1 = kind == SlotKind.Item ? bitmap.Width * 9 / 10 : bitmap.Width * 4 / 5;
        var y0 = bitmap.Height / 5;
        var y1 = kind == SlotKind.Item ? bitmap.Height * 9 / 10 : bitmap.Height * 4 / 5;
        for (var y = y0; y < y1; y += 2)
        for (var x = x0; x < x1; x += 2)
        {
            var c = bitmap.GetPixel(x, y);
            var spread = Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));
            if (c.R > 205 && c.G > 205 && c.B > 205 && spread < 32) brightNeutral++;
        }
        return brightNeutral >= 8;
    }

    private static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var source = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Bgra32,
                null, data.Scan0, data.Stride * bitmap.Height, data.Stride);
            source.Freeze();
            return source;
        }
        finally { bitmap.UnlockBits(data); }
    }

    private static IntPtr FindWarcraftWindow()
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            var title = new System.Text.StringBuilder(GetWindowTextLength(hwnd) + 1);
            GetWindowText(hwnd, title, title.Capacity);
            if (!title.ToString().Trim().Equals("Warcraft III", StringComparison.OrdinalIgnoreCase)) return true;
            result = hwnd;
            return false;
        }, IntPtr.Zero);
        return result;
    }

    private static bool TryGetClientBounds(IntPtr hwnd, out System.Drawing.Rectangle bounds)
    {
        bounds = System.Drawing.Rectangle.Empty;
        if (!GetClientRect(hwnd, out var rect)) return false;
        var point = new PointNative();
        if (!ClientToScreen(hwnd, ref point)) return false;
        bounds = new System.Drawing.Rectangle(point.X, point.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    internal enum SlotKind { Item, Skill }
    internal sealed record SlotDefinition(string Name, SlotKind Kind, double X, double Y);
    internal sealed class SlotModel(SlotDefinition definition, StackPanel card, Border previewBorder,
        System.Windows.Controls.Image preview, CheckBox toggle)
    {
        public SlotDefinition Definition { get; } = definition;
        public StackPanel Card { get; } = card;
        public Border PreviewBorder { get; } = previewBorder;
        public System.Windows.Controls.Image Preview { get; } = preview;
        public CheckBox Toggle { get; } = toggle;
        public BitmapSource? Source { get; set; }
        public bool Cooldown { get; set; }
    }

    private sealed class PersistedSettings
    {
        public bool[] Selected { get; set; } = [];
        public bool Vertical { get; set; }
        public double SettingsLeft { get; set; } = double.NaN;
        public double SettingsTop { get; set; } = double.NaN;
        public double OverlayLeft { get; set; } = 24;
        public double OverlayTop { get; set; } = 92;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct RectNative { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int maxCount);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RectNative rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref PointNative point);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
