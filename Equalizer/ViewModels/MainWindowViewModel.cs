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
        /// <summary>
        /// Список доступных девайсов
        /// </summary>
        [ObservableProperty]
        public ObservableCollection<MMDevice> _Devices;
        /// <summary>
        /// Выбранный девайс для вывода
        /// </summary>
        [ObservableProperty]
        private MMDevice _SelectedDevice;
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
            if (value is not null)
                Processor.ChangeDevices(value);
        }
        public MainWindowViewModel()
        {
            Processor = new();
            Devices = [.. DSProcessor.GetDevices().Where(item => !item.FriendlyName.Contains("Virtual"))];
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
        [RelayCommand]
        private void Play()
        {
            if (!Processor.Initialized)
                Processor.Initialize(SelectedDevice);
            Processor.StartCapture();
        }
        /// <summary>
        /// Запуск остановки процессора
        /// </summary>
        [RelayCommand]
        private void Stop()
        {
            Processor.StopCapture();
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
                await JsonSerializer.SerializeAsync(stream, Processor.FrequencyLines);
            }
        }
        /// <summary>
        ///  Загружает сериализованные полосы из файла и перетирает текущие полосы
        /// </summary>
        [RelayCommand]
        private async Task LoadLines()
        {
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
                var lines = await JsonSerializer.DeserializeAsync<ObservableCollection<FrequencyLine>>(stream);
                if (lines is not null)
                {
                    Processor.FrequencyLines.Clear();
                    foreach (var line in lines)
                        Processor.FrequencyLines.Add(line);
                }
            }

        }
        [RelayCommand]
        private async Task OpenDeleteLines()
        {
            ObservableCollection<FrequencyLine>? result = await new DeleteLinesWindow() 
            { 
                DataContext = new DeleteLinesWindowViewModel(Processor.FrequencyLines) 
            }
            .ShowDialog<ObservableCollection<FrequencyLine>>((Avalonia.Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow);
            if (result is not null)
            {
                //копируем массив чтобы итератор не сдох при очередном закрытии окна
                ObservableCollection<FrequencyLine> copyedResult = [..result];
                foreach (FrequencyLine item in copyedResult)
                {
                    Processor.FrequencyLines.Remove(item);
                }
            }
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
