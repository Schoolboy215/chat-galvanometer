using System.Windows;
using System.Windows.Controls;

namespace ChatGalvanometer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Handler handler;

        public MainWindow()
        {
            InitializeComponent();

            handler = new Handler();
            DataContext = handler.Settings;

            this.Width = handler.Settings.WindowWidth;
            this.Height = handler.Settings.WindowHeight;

            this.COMList.SelectedValue = handler.Settings.ComPort;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            handler.DumpMessages();

            handler.Settings.WindowWidth = (int)this.Width;
            handler.Settings.WindowHeight = (int)this.Height;
            handler.Settings.Save();
        }

        private async void connectButton_Click(object sender, RoutedEventArgs e)
        {
            if (handler.Settings.IsConnected)
                await handler.DisconnectFromWebSocket();
            else
                await handler.ConnectToWebSocket();
        }

        private async void getTokenButton_Click(object sender, RoutedEventArgs e)
        {
            await handler.GetToken();
        }

        private async void UserIdLookupButton_Click(object sender, RoutedEventArgs e)
        {
            handler.Settings.UserId = await handler.GetUserId(handler.Settings.UserName);
        }

        private async void BroadcasterIdLookup_Click(object sender, RoutedEventArgs e)
        {
            handler.Settings.BroadcasterId = await handler.GetUserId(handler.Settings.BroadcasterName);
        }

        private void COMList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 0)
            {
                handler.Settings.ComPort = e.AddedItems?[0].ToString();
            }
        }

        private void COMTestButton_Click(object sender, RoutedEventArgs e)
        {
            handler.TestCOMPort();
        }

        private void AddBadItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Bad word or phrase:") { Owner = this };
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.Value)
                && !handler.Settings.BadItems.Contains(dialog.Value))
                handler.Settings.BadItems.Add(dialog.Value);
        }

        private void RemoveBadItem_Click(object sender, RoutedEventArgs e)
        {
            if (BadList.SelectedItem is string item)
                handler.Settings.BadItems.Remove(item);
        }

        private void AddGoodItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Good word or phrase:") { Owner = this };
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.Value)
                && !handler.Settings.GoodItems.Contains(dialog.Value))
                handler.Settings.GoodItems.Add(dialog.Value);
        }

        private void RemoveGoodItem_Click(object sender, RoutedEventArgs e)
        {
            if (GoodList.SelectedItem is string item)
                handler.Settings.GoodItems.Remove(item);
        }

        private void replayFilePickerButton_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new System.Windows.Forms.OpenFileDialog();
            var result = fileDialog.ShowDialog();
            switch (result)
            {
                case System.Windows.Forms.DialogResult.OK:
                    var file = fileDialog.FileName;
                    replayFileNameBox.Text = fileDialog.SafeFileName;
                    handler.Settings.ReplayFilename = file;
                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                default:
                    replayFileNameBox.Text = null;
                    break;
            }
        }

        private void replayStartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (handler.Settings.IsReplaying)
            {
                handler.StopReplay();
            }
            else
            {
                handler.StartReplay(handler.Settings.ReplayFilename!, startTimeBox.Text);
            }
        }
    }
}
