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
        [ObservableProperty]
        private double _Lowfreqline; 
        private DSProcessor _Processor;
        public MainWindowViewModel()
        {
            _Processor = new();
             Devices = [.. DSProcessor.GetDevices()];
        }
        partial void OnLowfreqlineChanged(double value)
        {
            _Processor.lowfreqline = (int)unchecked(value);
        }
        [RelayCommand]
        private void Play()
        {
            if (!_Processor.Initialized)
                _Processor.Initialize(SelectedDevice);
            _Processor.StartCapture();
        }
        [RelayCommand]
        private void Stop()
        {
            _Processor.StopCapture();
        }
    }
}
