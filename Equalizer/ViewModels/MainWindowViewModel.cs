using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Equalizer.Service;
using NAudio.CoreAudioApi;
using System.Collections.ObjectModel;
using System.Linq;

namespace Equalizer.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        public ObservableCollection<MMDevice> _Devices;
        [ObservableProperty]
        private MMDevice _SelectedDevice;
        [ObservableProperty]
        private DSProcessor _Processor;
        public MainWindowViewModel()
        {
            Processor = new();
            Devices = [.. DSProcessor.GetDevices()];
            //тестовые полосы
            Processor.FrequencyLines.Add(new Models.FrequencyLine(50,800));
            Processor.FrequencyLines.Add(new Models.FrequencyLine(800,1800));
        }
        [RelayCommand]
        private void Play()
        {
            if (!Processor.Initialized)
                Processor.Initialize(SelectedDevice);
            Processor.StartCapture();
        }
        [RelayCommand]
        private void Stop()
        {
            Processor.StopCapture();
        }
    }
}
