using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Equalizer.Models;
using System.Collections.ObjectModel;

namespace Equalizer.ViewModels
{
    public partial class DeleteLinesWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<FrequencyLine> _Lines;
        [ObservableProperty]
        private ObservableCollection<FrequencyLine> _SelectedLines;
        [RelayCommand]
        private void DeleteCommand(Window window) => window.Close(SelectedLines);
        [RelayCommand]
        private static void CancelCommand(Window window) => window.Close(null);
        public DeleteLinesWindowViewModel()
        {
            Lines = [];
            SelectedLines = [];
        }
        public DeleteLinesWindowViewModel(ObservableCollection<FrequencyLine> lines)
        {
            Lines = lines;
            SelectedLines = [];
        }
    }
}
