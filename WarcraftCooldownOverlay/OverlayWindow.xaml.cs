using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WarcraftCooldownOverlay;

public partial class OverlayWindow : Window
{
    private readonly List<OverlaySlot> _views = [];

    public OverlayWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };
    }

    internal void SetSlots(IReadOnlyList<MainWindow.SlotModel> slots, bool vertical)
    {
        GroupPanel.Children.Clear();
        GroupPanel.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        _views.Clear();

        var selectedItemIndexes = Enumerable.Range(0, Math.Min(6, slots.Count))
            .Where(index => slots[index].Toggle.IsChecked == true).ToArray();
        var inventoryRows = selectedItemIndexes.Length == 0 ? 0 : selectedItemIndexes.Max() / 2 + 1;
        var inventory = inventoryRows > 0
            ? CreateGrid(inventoryRows, 2, new Thickness(0, 0, 4, 0))
            : null;
        var skills = CreateGrid(2, 4, new Thickness(0));
        if (inventory is not null) GroupPanel.Children.Add(inventory);
        GroupPanel.Children.Add(skills);

        for (var index = 0; index < slots.Count; index++)
        {
            var model = slots[index];
            if (model.Toggle.IsChecked != true) continue;
            var image = new Image
            {
                Width = 56, Height = 56, Stretch = Stretch.Fill, Source = model.Source
            };
            var border = new Border
            {
                Width = 58, Height = 58, Margin = new Thickness(0), Padding = new Thickness(0),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2), Child = image
            };
            Grid target;
            int row, column;
            if (index < 6)
            {
                target = inventory!;
                row = index / 2;
                column = index % 2;
            }
            else
            {
                target = skills;
                var skillIndex = index - 6;
                // Preserve the real Warcraft command-card positions: the monitored top row
                // occupies columns 2-4 because the red arrow button in column 1 is excluded.
                if (skillIndex < 3) { row = 0; column = skillIndex + 1; }
                else { row = 1; column = skillIndex - 3; }
            }
            Grid.SetRow(border, row);
            Grid.SetColumn(border, column);
            target.Children.Add(border);
            _views.Add(new OverlaySlot(model, image, border));
        }
        RefreshImages();
    }

    private static Grid CreateGrid(int rows, int columns, Thickness margin)
    {
        var grid = new Grid { Margin = margin };
        for (var i = 0; i < rows; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        return grid;
    }

    internal void RefreshImages()
    {
        foreach (var view in _views)
        {
            view.Image.Source = view.Model.Source;
            view.Border.BorderBrush = new SolidColorBrush(view.Model.Cooldown
                ? Color.FromRgb(255, 75, 75)
                : Color.FromRgb(83, 216, 255));
        }
    }

    private sealed record OverlaySlot(MainWindow.SlotModel Model, Image Image, Border Border);
}
