using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TextProcessor;

/// <summary>
/// Вспомогательный класс для отображения placeholder-текста в TextBox.
/// </summary>
public static class TextBoxPlaceholder
{
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached(
            "Placeholder",
            typeof(string),
            typeof(TextBoxPlaceholder),
            new PropertyMetadata(string.Empty, OnPlaceholderChanged));

    public static string GetPlaceholder(DependencyObject obj) =>
        (string)obj.GetValue(PlaceholderProperty);

    public static void SetPlaceholder(DependencyObject obj, string value) =>
        obj.SetValue(PlaceholderProperty, value);

    private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            textBox.TextChanged -= TextBox_TextChanged;
            textBox.TextChanged += TextBox_TextChanged;
            textBox.Loaded -= TextBox_Loaded;
            textBox.Loaded += TextBox_Loaded;
            UpdatePlaceholder(textBox);
        }
    }

    private static void TextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
            UpdatePlaceholder(textBox);
    }

    private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            UpdatePlaceholder(textBox);
    }

    private static void UpdatePlaceholder(TextBox textBox)
    {
        var placeholder = GetPlaceholder(textBox);
        var layer = AdornerLayer.GetAdornerLayer(textBox);

        if (layer is null)
            return;

        var existing = layer.GetAdorners(textBox)?
            .OfType<PlaceholderAdorner>()
            .FirstOrDefault();

        if (string.IsNullOrEmpty(textBox.Text) && !string.IsNullOrEmpty(placeholder))
        {
            if (existing is null)
                layer.Add(new PlaceholderAdorner(textBox, placeholder));
        }
        else if (existing is not null)
        {
            layer.Remove(existing);
        }
    }

    private sealed class PlaceholderAdorner : Adorner
    {
        private readonly TextBlock _textBlock;

        public PlaceholderAdorner(TextBox adornedElement, string placeholder)
            : base(adornedElement)
        {
            _textBlock = new TextBlock
            {
                Text = placeholder,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                IsHitTestVisible = false,
                Margin = new Thickness(2, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            AddVisualChild(_textBlock);
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) => _textBlock;

        protected override Size MeasureOverride(Size constraint)
        {
            _textBlock.Measure(constraint);
            return constraint;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _textBlock.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
