using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Equalizer.ViewModels
{
    public partial class AddLineWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private int? _From;
        [ObservableProperty]
        private int? _To;
        [ObservableProperty]
        private string? _Name;
        [RelayCommand]
        private void AddCommand(Window window) => window.Close(new Models.FrequencyLine(From ?? 0, To ?? 0) { Name = Name });
        [RelayCommand]
        private static void CancelCommand(Window window) => window.Close(null);
    }
}
