using AudioNode.ViewModels;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace AudioNode.Views
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<AudioApp> ActiveApps { get; set; } = new ObservableCollection<AudioApp>();

        // Store the master device so it doesn't get garbage collected
        private MMDevice _defaultDevice;

        // --- GLOBAL VOLUME PROPERTIES ---
        public double GlobalVolume
        {
            get
            {
                if (_defaultDevice == null) return 0;
                return _defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
            }
            set
            {
                if (_defaultDevice != null)
                {
                    // Update Windows Master Volume
                    _defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(value / 100);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GlobalVolumeText));
                }
            }
        }

        public string GlobalVolumeText => $"{(int)GlobalVolume}%";

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            LoadAudioSessions();
            AppListControl.ItemsSource = ActiveApps;
        }

        private void LoadAudioSessions()
        {
            using var enumerator = new MMDeviceEnumerator();
            // Get the Master Audio Device and save it to the class level variable
            _defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            // LISTEN FOR OUTSIDE VOLUME CHANGES
            _defaultDevice.AudioEndpointVolume.OnVolumeNotification += OnSystemVolumeChanged;

            // Load Individual Apps
            var sessionManager = _defaultDevice.AudioSessionManager;
            for (int i = 0; i < sessionManager.Sessions.Count; i++)
            {
                var session = sessionManager.Sessions[i];
                if (session.State == AudioSessionState.AudioSessionStateActive ||
                    session.State == AudioSessionState.AudioSessionStateInactive)
                {
                    ActiveApps.Add(new AudioApp(session));
                }
            }
        }

        // Triggered when you use your keyboard/headset to change Windows volume
        private void OnSystemVolumeChanged(AudioVolumeNotificationData data)
        {
            // Using the Dispatcher to update the UI thread, otherwise the app will crash
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(GlobalVolume));
                OnPropertyChanged(nameof(GlobalVolumeText));
            });
        }

        // --- WINDOW CONTROLS ---
        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }

        private void OnMinimizeClicked(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            // Clean up the event listener before closing to prevent memory leaks
            if (_defaultDevice != null)
            {
                _defaultDevice.AudioEndpointVolume.OnVolumeNotification -= OnSystemVolumeChanged;
                _defaultDevice.Dispose();
            }
            Application.Current.Shutdown();
        }

        // --- INOTIFY BOILERPLATE ---
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}