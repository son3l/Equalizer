using CommunityToolkit.Mvvm.ComponentModel;
using NAudio.CoreAudioApi;

namespace Equalizer.Models
{
    public partial class Settings : ObservableObject
    {
        [ObservableProperty]
        private bool _UseOnStartupDefaultPreset;
        [ObservableProperty]
        private string? _PathToDefaultPreset;
        [ObservableProperty]
        private string? _DefaultCaptureDeviceName;
        public Settings(MMDevice captureDevice)
        {
            DefaultCaptureDeviceName = captureDevice.DeviceFriendlyName;
        }
        public Settings()
        {

        }

        public override string ToString()
        {
            return string.Join(';', DefaultCaptureDeviceName, PathToDefaultPreset);
        }
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            return obj is Settings
                && (obj as Settings)?.DefaultCaptureDeviceName == DefaultCaptureDeviceName
                && (obj as Settings)?.UseOnStartupDefaultPreset == UseOnStartupDefaultPreset
                && (obj as Settings)?.PathToDefaultPreset == PathToDefaultPreset;
        }
    }
    public enum SettingsChanges
    {
        DefaultCaptureDeviceName,
        All,
        None,
        Others
    }
}
