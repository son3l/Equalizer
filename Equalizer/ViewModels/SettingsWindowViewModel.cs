using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Equalizer.Models;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Equalizer.ViewModels
{
    public partial class SettingsWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        public Settings _Settings;
        [ObservableProperty]
        private ObservableCollection<MMDevice> _Devices;
        [ObservableProperty]
        private MMDevice _SelectedDevice;
        partial void OnSelectedDeviceChanged(MMDevice value)
        {
            if (value is not null)
                Settings.DefaultCaptureDeviceName = value.FriendlyName;
        }
        public SettingsWindowViewModel(Settings settings)
        {
            Settings = settings;
            Devices = [.. new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)];
            SelectedDevice = Devices.First(item => item.FriendlyName == Settings.DefaultCaptureDeviceName);
        }
        public SettingsWindowViewModel()
        {
            Settings = new();
        }
        [RelayCommand]
        private void Edit(Window window)
        {
            string[] properties = new string[4];
            int i = 0;
            if (App.Settings.DefaultCaptureDeviceName != Settings.DefaultCaptureDeviceName)
            {
                properties[i++] = nameof(App.Settings.DefaultCaptureDeviceName);
                App.Settings.DefaultCaptureDeviceName = Settings.DefaultCaptureDeviceName;
            }
            if (App.Settings.UseOnStartupDefaultPreset != Settings.UseOnStartupDefaultPreset)
            {
                properties[i++] = nameof(App.Settings.UseOnStartupDefaultPreset);
                App.Settings.UseOnStartupDefaultPreset = Settings.UseOnStartupDefaultPreset;
            }
            if (App.Settings.PathToDefaultPreset != Settings.PathToDefaultPreset)
            {
                properties[i++] = nameof(App.Settings.PathToDefaultPreset);
                App.Settings.PathToDefaultPreset = Settings.PathToDefaultPreset;
            }
            if (App.Settings.UseSmoothing != Settings.UseSmoothing)
            {
                properties[i++] = nameof(App.Settings.UseSmoothing);
                App.Settings.UseSmoothing = Settings.UseSmoothing;
            }
            App.SettingsChangedHandler?.Invoke(properties);
            window.Close();

        }
        [RelayCommand]
        private void Cancel(Window window) => window.Close();
        [RelayCommand]
        private async Task OpenExplorer()
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
                Settings.PathToDefaultPreset = file[0].Path.LocalPath;
            }
        }
    }
}
