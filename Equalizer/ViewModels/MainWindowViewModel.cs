using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Equalizer.Models;
using Equalizer.Service;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Equalizer.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        // TODO на будущее сделать шару на несколько объектов вывода
        [ObservableProperty]
        private ObservableCollection<float> _SpectrumValues;
        /// <summary>
        /// Список доступных девайсов
        /// </summary>
        [ObservableProperty]
        public ObservableCollection<MMDevice> _Devices;
        /// <summary>
        /// Выбранный девайс для вывода
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private MMDevice _SelectedDevice;
        /// <summary>
        /// Устройства захвата
        /// </summary>
        private MMDevice _CaptureDevice;
        /// <summary>
        /// Объект процессора обработки звука
        /// </summary>
        [ObservableProperty]
        private DSProcessor _Processor;
        /// <summary>
        /// При изменении выбранного девайса меняем в процессоре девайсы
        /// </summary>
        partial void OnSelectedDeviceChanged(MMDevice value)
        {
            if (value is not null && _CaptureDevice is not null)
                Processor.ChangeDevices(value,_CaptureDevice);
        }
        public MainWindowViewModel()
        {
            Processor = new();
            SpectrumValues = [];
            Processor.SpectrumCalcucatedHandler += (spectrum) =>
            {
                SpectrumValues = [.. spectrum];
            };
            //тестовый спектр
            for (int i = 0; i < 512; i++)
            {
                SpectrumValues.Add((float)new Random().NextDouble());
            }
            Devices = [.. DSProcessor.GetDevices().Where(item => !item.FriendlyName.Contains("Virtual"))];
            SelectedDevice = Devices.First();
            //тестовые полосы
            Processor.FrequencyLines.Add(new FrequencyLine(0, 1500) { Name = " low BASS" });
            Processor.FrequencyLines.Add(new FrequencyLine(1500, 3800) { Name = " mid BASS" });
            Processor.FrequencyLines.Add(new FrequencyLine(3800, 6800) { Name = " high BASS" });
            Processor.FrequencyLines.Add(new FrequencyLine(6800, 9800) { Name = "low mid" });
            Processor.FrequencyLines.Add(new FrequencyLine(9800, 12800) { Name = "mid mid" });
            Processor.FrequencyLines.Add(new FrequencyLine(12800, 16800) { Name = "high mid" });
            Processor.FrequencyLines.Add(new FrequencyLine(16800, 19800) { Name = "low high" });
            Processor.FrequencyLines.Add(new FrequencyLine(19800, 21800) { Name = "mid high" });
            Processor.FrequencyLines.Add(new FrequencyLine(21800, 23800) { Name = "high high" });
        }
        /// <summary>
        /// Запуск обработки звуука
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartOrStop))]
        private void Play()
        {
            if (!Processor.Initialized)
                Processor.Initialize(SelectedDevice, _CaptureDevice);
            Processor.StartCapture();
        }
        /// <summary>
        /// Запуск остановки процессора
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartOrStop))]
        private void Stop()
        {
            Processor.StopCapture();
        }
        /// <summary>
        /// Возвращает булево можно ли стартовать или стопить воспроизведение
        /// </summary>
        private bool CanStartOrStop()
        {
            return SelectedDevice is not null;
        }
        /// <summary>
        /// Скидывает значение на всех полосах в 0
        /// </summary>
        [RelayCommand]
        private void DiscardAllLines()
        {
            foreach (var line in Processor.FrequencyLines)
            {
                line.GainDecibells = 0;
            }
        }
        /// <summary>
        /// Сериализует и сохраняет в файл текущие полосы в приложении
        /// </summary>
        [RelayCommand]
        private async Task SaveLines()
        {
            IStorageFile? file = await (Avalonia.Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                SuggestedStartLocation = await (Avalonia.Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                .MainWindow
                .StorageProvider
                .TryGetFolderFromPathAsync(
                    Environment
                    .GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                DefaultExtension = "lines",
                FileTypeChoices =
                [
                    new FilePickerFileType("Конфиг полос эквалайзера")
                    {
                        Patterns = ["*.lines"]
                    }
                ]
            });
            if (file is not null)
            {
                using Stream stream = await file.OpenWriteAsync();
                await JsonSerializer.SerializeAsync(stream, Processor.FrequencyLines.ToArray());
            }
        }
        /// <summary>
        ///  Загружает сериализованные полосы из файла и перетирает текущие полосы
        /// </summary>
        [RelayCommand]
        private async Task LoadLines()
        {
            //TODO сделать нормальную обработку ошибок
            IReadOnlyList<IStorageFile> file = await (Avalonia.Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                SuggestedStartLocation = await (Avalonia.Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                .MainWindow
                .StorageProvider
                .TryGetFolderFromPathAsync(
                    Environment
                    .GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                AllowMultiple = false,
                FileTypeFilter =
                    [
                    new FilePickerFileType("Конфиг полос эквалайзера (.lines)")
                         {
                             Patterns = [ "*.lines" ]
                         }
                    ]
            });
            if (file is not null && file.Count != 0)
            {
                using Stream stream = await file[0].OpenReadAsync();
                var lines = await JsonSerializer.DeserializeAsync<FrequencyLine[]>(stream);
                if (lines is not null)
                {
                    Processor.FrequencyLines.Clear();
                    foreach (var line in lines)
                        Processor.FrequencyLines.Add(
                            new FrequencyLine(line.From, line.To)
                            {
                                GainDecibells = line.GainDecibells,
                                Name = line.Name
                            });
                }
            }

        }
        /// <summary>
        /// Открываем диалоговое окно удаления полос 
        /// </summary>
        [RelayCommand]
        private async Task OpenDeleteLines()
        {
            ObservableCollection<FrequencyLine>? result = await new DeleteLinesWindow()
            {
                DataContext = new DeleteLinesWindowViewModel(Processor.FrequencyLines),
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
            }
            .ShowDialog<ObservableCollection<FrequencyLine>>((Avalonia.Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow);
            if (result is not null)
            {
                //копируем массив чтобы итератор не сдох 
                ObservableCollection<FrequencyLine> copyedResult = [.. result];
                foreach (FrequencyLine item in copyedResult)
                {
                    Processor.FrequencyLines.Remove(item);
                }
            }
        }
        /// <summary>
        /// Открываем диалоговое окно добавления полос 
        /// </summary>
        [RelayCommand]
        private async Task OpenAddLines()
        {
            FrequencyLine result = await new AddLineWindow()
            {
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
            }
            .ShowDialog<FrequencyLine>((Avalonia.Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow);
            if (result is not null)
            {
                Processor.FrequencyLines.Add(
                    new FrequencyLine(result.From, result.To)
                    {
                        GainDecibells = 0,
                        Name = result.Name
                    });
            }
        }
        [RelayCommand]
        private async Task OpenSettings()
        {
            SettingsChanges result = await new SettingsWindow()
            {
                DataContext = new SettingsWindowViewModel(new Settings()
                {
                    DefaultCaptureDeviceName = App.Settings.DefaultCaptureDeviceName,
                    PathToDefaultPreset = App.Settings.PathToDefaultPreset,
                    UseOnStartupDefaultPreset = App.Settings.UseOnStartupDefaultPreset
                }),
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
            }
            .ShowDialog<SettingsChanges>((Avalonia.Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow);
            //TODO сделать нормальную обработку ошибок
            if (result != SettingsChanges.None)
            {
                using Stream fileStream = new FileStream(Path.Combine(Environment.CurrentDirectory, "default.settings"), FileMode.Create, FileAccess.Write);
                await JsonSerializer.SerializeAsync(fileStream, App.Settings);
                await fileStream.DisposeAsync();
            }
            if (result == SettingsChanges.DefaultCaptureDeviceName)
            {
                bool isStopped = false;
                _CaptureDevice = new MMDeviceEnumerator()
                    .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .First(item => item.FriendlyName == App.Settings.DefaultCaptureDeviceName);
                if (Processor.IsRunning)
                {
                    Processor.StopCapture();
                    isStopped = true;
                }
                Processor.ChangeDevices(SelectedDevice, _CaptureDevice);
                if (isStopped)
                    Processor.StartCapture();

            }
        }
        [RelayCommand]
        private async Task Startup()
        {
            if (App.Settings.UseOnStartupDefaultPreset && App.Settings.PathToDefaultPreset is not null)
            {
                //TODO сделать нормальную обработку ошибок
                try
                {
                    using Stream stream = new FileStream(App.Settings.PathToDefaultPreset, FileMode.Open, FileAccess.Read);
                    var lines = await JsonSerializer.DeserializeAsync<FrequencyLine[]>(stream);
                    if (lines is not null)
                    {
                        Processor.FrequencyLines.Clear();
                        foreach (var line in lines)
                            Processor.FrequencyLines.Add(
                                new FrequencyLine(line.From, line.To)
                                {
                                    GainDecibells = line.GainDecibells,
                                    Name = line.Name
                                });
                    }
                }
                catch
                { }
            }
            MMDeviceCollection devices = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            if (App.Settings.DefaultCaptureDeviceName is null)
                App.Settings.DefaultCaptureDeviceName = devices.First().FriendlyName;
            _CaptureDevice = devices.First(item => item.FriendlyName == App.Settings.DefaultCaptureDeviceName);
        }
        /// <summary>
        /// Диспозит текущий процессор при выходе из приложения
        /// </summary>
        [RelayCommand]
        private void Closing()
        {
            Processor?.Dispose();
        }
    }
}
