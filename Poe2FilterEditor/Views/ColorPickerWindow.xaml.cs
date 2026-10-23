using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Poe2FilterEditor.Models;

namespace Poe2FilterEditor.Views
{
    public partial class ColorPickerWindow : Window
    {
        private static readonly string[] Presets =
        {
            "#FF0000", "#FF7800", "#FFD700", "#FFFF00", "#7CFF4D", "#00FF7F", "#00E5C0", "#00C8FF",
            "#3B82F6", "#6B4FA0", "#B24BD8", "#FF4FA3", "#FFFFFF", "#E8E2D6", "#9A9086", "#5A5048",
            "#2A241C", "#120F0D", "#C9A227", "#E0B94B", "#8E2C2C", "#B23A3A", "#3E8E63", "#1E3A5F"
        };

        private bool _syncing;

        public ColorPickerWindow(string title, FilterColor initial)
        {
            InitializeComponent();
            Infrastructure.WindowChromeHelper.ApplyDarkTitleBar(this);

            HeaderText.Text = title;
            Title = title;

            BuildPresets();
            ApplyColor(initial, updateHex: true);
        }

        public FilterColor SelectedColor { get; private set; }

        private void BuildPresets()
        {
            foreach (var preset in Presets)
            {
                var color = (Color)ColorConverter.ConvertFromString(preset)!;
                var button = new Button
                {
                    Width = 34,
                    Height = 26,
                    Margin = new Thickness(0, 0, 6, 6),
                    Tag = preset,
                    ToolTip = preset,
                    Cursor = Cursors.Hand,
                    Style = (Style)FindResource("PickerSwatch")
                };

                var brush = new SolidColorBrush(color);
                brush.Freeze();
                button.Background = brush;
                button.Click += (_, _) => ApplyColor(FilterColor.FromColor(color), updateHex: true);
                PresetPanel.Children.Add(button);
            }
        }

        private void ApplyColor(FilterColor color, bool updateHex)
        {
            _syncing = true;
            try
            {
                RSlider.Value = color.R;
                GSlider.Value = color.G;
                BSlider.Value = color.B;
                ASlider.Value = color.A;

                RValue.Text = color.R.ToString();
                GValue.Text = color.G.ToString();
                BValue.Text = color.B.ToString();
                AValue.Text = color.A.ToString();

                var brush = color.ToBrush();
                PreviewBorder.Background = brush;
                PreviewText.Foreground = Luminance(color) > 150 ? Brushes.Black : Brushes.White;
                PreviewText.Text = $"样例文本  {color.ToRgbText()}";

                if (updateHex)
                {
                    HexBox.Text = color.ToHexText();
                }

                SelectedColor = color;
            }
            finally
            {
                _syncing = false;
            }
        }

        private static double Luminance(FilterColor color) => 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;

        private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncing)
            {
                return;
            }

            ApplyColor(new FilterColor((byte)RSlider.Value, (byte)GSlider.Value, (byte)BSlider.Value, (byte)ASlider.Value), true);
        }

        private void OnHexLostFocus(object sender, RoutedEventArgs e) => TryApplyHex();

        private void OnHexKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryApplyHex();
            }
        }

        private void TryApplyHex()
        {
            if (FilterColor.TryParse(HexBox.Text, out var color))
            {
                ApplyColor(color, updateHex: false);
                HexBox.Text = color.ToHexText();
            }
            else
            {
                HexBox.Text = SelectedColor.ToHexText();
            }
        }

        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            SelectedColor = new FilterColor((byte)RSlider.Value, (byte)GSlider.Value, (byte)BSlider.Value, (byte)ASlider.Value);
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
