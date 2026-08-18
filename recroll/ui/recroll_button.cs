using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace recroll.ui
{
    internal static class recroll_button
    {
        internal static Button Create()
        {
            return new Button
            {
                Content = "RECROLL",
                Width = 105,
                Height = 36,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = Brushes.Black,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Opacity = 0.85,
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Disconnect Hearthstone TCP connection"
            };
        }
    }
}
