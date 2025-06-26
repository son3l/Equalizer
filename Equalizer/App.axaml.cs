using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Equalizer.Models;
using Equalizer.ViewModels;
using Equalizer.Views;
using NAudio.CoreAudioApi;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Equalizer
{
    public partial class App : Application
    {
        public static Settings? Settings { get; set; }
        public delegate void SettingsChanged(string[] properties);
        public static SettingsChanged? SettingsChangedHandler { get; set; }
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
        private static async Task LoadSettings()
        {
            try
            {
                using Stream fileStream = new FileStream(Path.Combine(Environment.CurrentDirectory, "default.settings"), FileMode.Open, FileAccess.Read);
                Settings? settings = await JsonSerializer.DeserializeAsync<Settings>(fileStream) ?? throw new JsonException();
                Settings = settings;
            }
            catch (FileNotFoundException)
            {
                new MessageBoxWindow("Внимание", "файл с настройками не найден, был создан новый", Material.Icons.MaterialIconKind.ErrorOutline)
                {
                    Topmost = true,
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
                }.Show();
                await MakeNewSettings();
            }
            catch (JsonException)
            {
                new MessageBoxWindow("Внимание", "файл с настройками недействителен, был создан новый", Material.Icons.MaterialIconKind.ErrorOutline)
                {
                    Topmost = true,
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
                }.Show();
                await MakeNewSettings();
            }
            catch (UnauthorizedAccessException)
            {
                new MessageBoxWindow("Внимание", "файл с настройками занят другим процессом, настройки будут дефолтные", Material.Icons.MaterialIconKind.ErrorOutline)
                {
                    Topmost = true,
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
                }.Show();
                Settings = new()
                {
                    DefaultCaptureDeviceName = new MMDeviceEnumerator()
                        .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                        .First().FriendlyName
                };
            }
        }
        public static async Task MakeNewSettings()
        {
            Settings = new()
            {
                DefaultCaptureDeviceName = new MMDeviceEnumerator()
                        .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                        .First().FriendlyName
            };
            using Stream fileStream = new FileStream(Path.Combine(Environment.CurrentDirectory, "default.settings"), FileMode.OpenOrCreate, FileAccess.Write);
            await JsonSerializer.SerializeAsync(fileStream, Settings);
            await fileStream.FlushAsync();
        }
        public static async Task MakeNewSettings(Settings settings)
        {
            Settings = settings;
            using Stream fileStream = new FileStream(Path.Combine(Environment.CurrentDirectory, "default.settings"), FileMode.OpenOrCreate, FileAccess.Write);
            await JsonSerializer.SerializeAsync(fileStream, Settings);
            await fileStream.FlushAsync();
        }
        public override void OnFrameworkInitializationCompleted()
        {
            var _ = LoadSettings();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindowViewTest//MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}