using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Equalizer.Service;
using NAudio.CoreAudioApi;
using System.Collections.ObjectModel;

namespace Equalizer.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        public ObservableCollection<MMDevice> _Devices;
        [ObservableProperty]
        private MMDevice _SelectedDevice;
        private AudioCaptureProcessor _Processor;
        public MainWindowViewModel()
        {
            Devices = [.. AudioCaptureProcessor.GetDevices()];
        }
        partial void OnSelectedDeviceChanged(MMDevice value)
        {
            _Processor?.Dispose();
            _Processor = new(SelectedDevice);
        }
        [RelayCommand]
        private void Play()
        {
            _Processor.StartCapture();
        }
    }
}
