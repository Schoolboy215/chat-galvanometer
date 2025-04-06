using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace ChatGalvanometer
{
    public class Settings : INotifyPropertyChanged
    {
        private string _clientId;
        public string? ClientId { get => _clientId; set { _clientId = value; OnPropertyChanged(nameof(ClientId)); BearerToken = ""; OnPropertyChanged(nameof(BearerToken)); OnPropertyChanged(nameof(CredentialsPopulated)); OnPropertyChanged(nameof(UserIdLookupEnabled)); OnPropertyChanged(nameof(ConnectEnabled)); } }

        private string _bearerToken;
        public string? BearerToken { get => _bearerToken; set { _bearerToken = value; OnPropertyChanged(nameof(BearerToken)); OnPropertyChanged(nameof(CredentialsPopulated)); OnPropertyChanged(nameof(UserIdLookupEnabled)); OnPropertyChanged(nameof(ConnectEnabled)); } }

        private string _userName;
        public string? UserName { get => _userName; set { _userName = value; OnPropertyChanged(nameof(UserName)); OnPropertyChanged(nameof(UserNamePopulated)); UserId = ""; OnPropertyChanged(nameof(UserId)); OnPropertyChanged(nameof(UserIdPopulated)); OnPropertyChanged(nameof(UserIdLookupEnabled)); OnPropertyChanged(nameof(ConnectEnabled)); } }

        private string _userId;
        public string? UserId { get => _userId; set { _userId = value; OnPropertyChanged(nameof(UserId)); OnPropertyChanged(nameof(UserIdPopulated)); OnPropertyChanged(nameof(UserIdLookupEnabled)); OnPropertyChanged(nameof(ConnectEnabled)); } }


        private string _broadcasterName;
        public string? BroadcasterName { get => _broadcasterName; set { _broadcasterName = value; OnPropertyChanged(nameof(BroadcasterName)); OnPropertyChanged(nameof(BroadcasterNamePopulated)); BroadcasterId = ""; OnPropertyChanged(nameof(BroadcasterId)); OnPropertyChanged(nameof(BroadcasterIdLookupEnabled)); OnPropertyChanged(nameof(ConnectEnabled)); } }

        private string _broadcasterId;
        public string? BroadcasterId { get => _broadcasterId; set { _broadcasterId = value; OnPropertyChanged(nameof(BroadcasterId)); OnPropertyChanged(nameof(BroadcasterIdPopulated)); OnPropertyChanged(nameof(BroadcasterIdLookupEnabled)); OnPropertyChanged(nameof(ConnectEnabled)); } }

        private int? _evaluationWindowLength;
        public int? EvaluationWindowLength { get => _evaluationWindowLength; set { _evaluationWindowLength = value; OnPropertyChanged(nameof(EvaluationWindowLength)); } }

        private int? _maxSentiment;
        public int? MaxSentiment { get => _maxSentiment; set { _maxSentiment = value; OnPropertyChanged(nameof(MaxSentiment)); } }

        private string? _comPort;
        public string? ComPort { get => _comPort; set { _comPort = value; OnPropertyChanged(nameof(ComPort)); } }

        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }

        public ObservableCollection<string> GoodItems { get; set; } = new ObservableCollection<string>();

        public ObservableCollection<string> BadItems { get; set; } = new ObservableCollection<string>();

        public ObservableCollection<string> COMPorts
        {
            get
            {
                ObservableCollection<string> ret = new ObservableCollection<string>();
                foreach (string p in SerialPort.GetPortNames())
                {
                    ret.Add(p);
                }
                return ret;
            }
        }



        [JsonIgnore]
        public bool CredentialsPopulated => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(BearerToken);

        [JsonIgnore]
        public bool UserNamePopulated => !string.IsNullOrWhiteSpace(UserName);

        [JsonIgnore]
        public bool UserIdPopulated => !string.IsNullOrWhiteSpace(UserId);

        [JsonIgnore]
        public bool UserIdLookupEnabled => CredentialsPopulated && UserNamePopulated && !UserIdPopulated;

        [JsonIgnore]
        public bool BroadcasterNamePopulated => !string.IsNullOrWhiteSpace(BroadcasterName);
        
        [JsonIgnore]
        public bool BroadcasterIdPopulated => !string.IsNullOrWhiteSpace(BroadcasterId);

        [JsonIgnore]
        public bool BroadcasterIdLookupEnabled => CredentialsPopulated && BroadcasterNamePopulated && !BroadcasterIdPopulated;

        [JsonIgnore]
        public bool ConnectEnabled => CredentialsPopulated && UserIdPopulated && BroadcasterIdPopulated;

        private int? _rawSentiment;
        [JsonIgnore]
        public int? RawSentiment { get => _rawSentiment; set { _rawSentiment = value; OnPropertyChanged(nameof(RawSentiment)); } }

        private decimal? _percentSentiment;

        [JsonIgnore]
        public decimal? PercentSentiment { get => _percentSentiment; set { _percentSentiment = value; OnPropertyChanged(nameof(PercentSentiment)); } }

        public static string SettingsFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public Settings()
        {
            WindowWidth = 640;
            WindowHeight = 480;
            GoodItems = ["+2"];
            BadItems = ["-2"];
            EvaluationWindowLength = 10;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Load settings from file
        public static Settings Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
            return new Settings(); // Default settings
        }

        // Save settings to file
        public void Save()
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }

        private void AlertRestartRequired()
        {
            string messageBoxText = "You need to close and open the program again to take new changes";
            string caption = "Restart required";
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.Warning;
            MessageBoxResult result;

            MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.OK);
        }
    }
}
