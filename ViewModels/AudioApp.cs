using NAudio.CoreAudioApi;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AudioNode.ViewModels
{
    public class AudioApp : INotifyPropertyChanged
    {
        private AudioSessionControl _session;
        private SimpleAudioVolume _volumeControl;

        public string AppName { get; set; } = string.Empty;

        public AudioApp(AudioSessionControl session)
        {
            _session = session;
            _volumeControl = session.SimpleAudioVolume;
            AppName = GetProcessName(session.GetProcessID);
        }

        public string VolumeText => $"{(int)Volume}%";
        public double Volume
        {
            get => _volumeControl.Volume * 100;
            set
            {
                _volumeControl.Volume = (float)(value / 100);
                OnPropertyChanged();
                OnPropertyChanged(nameof(VolumeText));
            }
        }

        // Helper to turn the Process ID into a readable name
        private string GetProcessName(uint processId)
        {
            if (processId == 0) return "System Sounds";

            try
            {
                var process = Process.GetProcessById((int)processId);
                // MainWindowTitle gets "Spotify Premium", ProcessName gets "Spotify"
                return !string.IsNullOrWhiteSpace(process.MainWindowTitle)
                    ? process.MainWindowTitle
                    : process.ProcessName;
            }
            catch
            {
                return "Unknown App";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}