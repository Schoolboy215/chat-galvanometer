using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.IO.Ports;
using System.IO;
using System.Runtime;

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
            DataContext = handler._settings;

            // Apply settings
            this.Width = handler._settings.WindowWidth;
            this.Height = handler._settings.WindowHeight;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            // Save window size before closing
            handler._settings.WindowWidth = (int)this.Width;
            handler._settings.WindowHeight = (int)this.Height;
            handler._settings.Save();
        }

        private async void connectButton_Click(object sender, RoutedEventArgs e)
        {
            await handler.ConnectToWebSocket();
        }

        private async void getTokenButton_Click(object sender, RoutedEventArgs e)
        {
            await handler.GetToken();
        }

        private async void UserIdLookupButton_Click(object sender, RoutedEventArgs e)
        {
            handler._settings.UserId = await handler.GetUserId(handler._settings.UserName);
        }

        private async void BroadcasterIdLookup_Click(object sender, RoutedEventArgs e)
        {
            handler._settings.BroadcasterId = await handler.GetUserId(handler._settings.BroadcasterName);
        }

        private void COMList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            handler._settings.ComPort = e.AddedItems?[0].ToString();
        }

        private void COMTestButton_Click(object sender, RoutedEventArgs e)
        {
            handler.TestCOMPort();
        }
    }
}