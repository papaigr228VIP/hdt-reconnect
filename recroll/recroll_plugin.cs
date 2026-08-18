using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;
using recroll.ui;

namespace recroll
{
    public sealed class recroll_plugin : IPlugin
    {
        private readonly recroll_service service = new recroll_service();
        private Button overlayButton;
        private bool overlayEnabled = true;
        private bool busy;

        public string Name => "recroll";
        public string Description => "Modern x64 Hearthstone reconnect tool.";
        public string ButtonText => "Reconnect now";
        public string Author => "papaigr228VIP";
        public Version Version => new Version(0, 1, 0, 0);
        public MenuItem MenuItem { get; private set; }

        public void OnLoad()
        {
            Log.Info("[recroll] Plugin loading.");
            CreateMenuItem();
            TryAttachOverlayButton();
            Log.Info("[recroll] Plugin loaded.");
        }

        public void OnUnload()
        {
            Log.Info("[recroll] Plugin unloading.");
            RemoveOverlayButton();
            Log.Info("[recroll] Plugin unloaded.");
        }

        public void OnButtonPress() => TriggerReconnect();

        public void OnUpdate()
        {
            if (overlayEnabled && overlayButton == null)
                TryAttachOverlayButton();
        }

        private void CreateMenuItem()
        {
            MenuItem = new MenuItem
            {
                Header = "recroll overlay button",
                IsCheckable = true,
                IsChecked = true
            };

            MenuItem.Checked += (sender, args) =>
            {
                overlayEnabled = true;
                TryAttachOverlayButton();

                if (overlayButton != null)
                    overlayButton.Visibility = Visibility.Visible;
            };

            MenuItem.Unchecked += (sender, args) =>
            {
                overlayEnabled = false;

                if (overlayButton != null)
                    overlayButton.Visibility = Visibility.Collapsed;
            };
        }

        private void TryAttachOverlayButton()
        {
            if (!overlayEnabled || overlayButton != null)
                return;

            try
            {
                if (Core.OverlayCanvas == null)
                    return;

                overlayButton = recroll_button.Create();
                overlayButton.Click += OverlayButton_Click;

                OverlayExtensions.SetIsOverlayHitTestVisible(overlayButton, true);

                Canvas.SetLeft(overlayButton, 20);
                Canvas.SetTop(overlayButton, 160);
                Panel.SetZIndex(overlayButton, int.MaxValue);

                Core.OverlayCanvas.Children.Add(overlayButton);

                Log.Info("[recroll] Overlay button attached.");
            }
            catch (Exception ex)
            {
                Log.Error("[recroll] Could not attach overlay button: " + ex);
                overlayButton = null;
            }
        }

        private void RemoveOverlayButton()
        {
            if (overlayButton == null)
                return;

            try
            {
                overlayButton.Click -= OverlayButton_Click;
                OverlayExtensions.SetIsOverlayHitTestVisible(overlayButton, false);

                if (Core.OverlayCanvas != null &&
                    Core.OverlayCanvas.Children.Contains(overlayButton))
                {
                    Core.OverlayCanvas.Children.Remove(overlayButton);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[recroll] Could not remove overlay button: " + ex);
            }
            finally
            {
                overlayButton = null;
            }
        }

        private void OverlayButton_Click(object sender, RoutedEventArgs e)
        {
            TriggerReconnect();
        }

        private async void TriggerReconnect()
        {
            if (busy)
                return;

            busy = true;
            string oldText = null;

            try
            {
                if (overlayButton != null)
                {
                    oldText = Convert.ToString(overlayButton.Content);
                    overlayButton.Content = "...";
                    overlayButton.IsEnabled = false;
                }

                recroll_result result = await Task.Run(() => service.Disconnect());

                if (overlayButton != null)
                    overlayButton.Content = result.Success ? "DONE" : "ERROR";

                if (result.Success)
                    Log.Info("[recroll] " + result.Message);
                else
                    Log.Error("[recroll] " + result.Message);

                await Task.Delay(result.Success ? 700 : 1400);
            }
            catch (Exception ex)
            {
                Log.Error("[recroll] Unexpected reconnect error: " + ex);

                if (overlayButton != null)
                    overlayButton.Content = "ERROR";

                await Task.Delay(1400);
            }
            finally
            {
                if (overlayButton != null)
                {
                    overlayButton.Content =
                        string.IsNullOrEmpty(oldText) ? "RECROLL" : oldText;
                    overlayButton.IsEnabled = true;
                }

                busy = false;
            }
        }
    }
}
