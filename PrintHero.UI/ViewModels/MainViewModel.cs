using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PrintHero.Core.Models;

namespace PrintHero.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private int _filesProcessedToday;
        private string? _defaultPrinter;
        private bool _isServiceRunning;
        private string? _licenseKey;
        private string _paperSize;

        public MainViewModel()
        {
            MonitoredFolders = new ObservableCollection<MonitoredFolder>();

            // Initialize with default values
            FilesProcessedToday = 0;
            DefaultPrinter = "No printer selected";
            IsServiceRunning = false;
            LicenseKey = "**************";

            StartServiceCommand = new RelayCommand(StartService);
            StopServiceCommand = new RelayCommand(StopService);
        }

        public int FilesProcessedToday
        {
            get => _filesProcessedToday;
            set => SetProperty(ref _filesProcessedToday, value);
        }

        public string? DefaultPrinter
        {
            get => _defaultPrinter;
            set => SetProperty(ref _defaultPrinter, value);
        }

        public string? PaperSize
        {
            get => _paperSize;
            set
            {
                if (SetProperty(ref _paperSize, value))
                {
                    OnPropertyChanged(nameof(PaperSize));
                }
            }
        }

        public bool IsServiceRunning
        {
            get => _isServiceRunning;
            set => SetProperty(ref _isServiceRunning, value);
        }

        public string? LicenseKey
        {
            get => _licenseKey;
            set => SetProperty(ref _licenseKey, value);
        }

        public ObservableCollection<MonitoredFolder> MonitoredFolders { get; }

        public ICommand StartServiceCommand { get; }
        public ICommand StopServiceCommand { get; }

        private void StartService()
        {
            IsServiceRunning = true;
        }

        private void StopService()
        {
            IsServiceRunning = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute();
        }
    }
}
