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
        private DSProcessor _Processor;
        partial void OnSelectedDeviceChanged(MMDevice value)
        {
            if (value is not null)
                Processor.ChangeDevices(value);
        }
        public MainWindowViewModel()
        {
            Processor = new();
            Devices = [.. DSProcessor.GetDevices()];
            //тестовые полосы
            Processor.FrequencyLines.Add(new Models.FrequencyLine(0, 1500));
            Processor.FrequencyLines.Add(new Models.FrequencyLine(1500, 3800));
            Processor.FrequencyLines.Add(new Models.FrequencyLine(3800, 6800));
            Processor.FrequencyLines.Add(new Models.FrequencyLine(6800, 9800));
            Processor.FrequencyLines.Add(new Models.FrequencyLine(9800, 12800));
            Processor.FrequencyLines.Add(new Models.FrequencyLine(12800, 16800));
            Processor.FrequencyLines.Add(new Models.FrequencyLine(16800, 19800));
            Processor.FrequencyLines.Add(new Models.FrequencyLine(19800, 21800));
            Processor.FrequencyLines.Add(new Models.FrequencyLine(21800, 23800));
            Processor.FrequencyLines.Add(new Models.FrequencyLine(23800, 33900));
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
