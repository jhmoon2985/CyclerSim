using System;
using System.Windows.Input;
using CyclerSim.Models;
using CyclerSim.Services;

namespace CyclerSim.ViewModels
{
    public class CanLinViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private string _name = string.Empty;
        private double _minValue;
        private double _maxValue;
        private double _currentValue;
        private bool _autoUpdate = true;

        public CanLinViewModel(IDataService dataService, string name)
        {
            _dataService = dataService;
            _name = name;

            // Initialize with default values
            _minValue = 0.0;
            _maxValue = 100.0;
            _currentValue = 50.0;

            SendNowCommand = new RelayCommand(SendNow);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public double MinValue
        {
            get => _minValue;
            set => SetProperty(ref _minValue, value);
        }

        public double MaxValue
        {
            get => _maxValue;
            set => SetProperty(ref _maxValue, value);
        }

        public double CurrentValue
        {
            get => _currentValue;
            set => SetProperty(ref _currentValue, value);
        }

        public bool AutoUpdate
        {
            get => _autoUpdate;
            set => SetProperty(ref _autoUpdate, value);
        }

        public ICommand SendNowCommand { get; }

        public void SimulateData()
        {
            if (!AutoUpdate) return;

            var random = new Random();
            var range = MaxValue - MinValue;
            var variation = range * 0.1; // 10% variation

            CurrentValue += (random.NextDouble() - 0.5) * variation;
            CurrentValue = Math.Max(MinValue, Math.Min(MaxValue, CurrentValue));
        }

        public CanLinData GetCanLinData(int equipmentId)
        {
            return new CanLinData
            {
                EquipmentId = equipmentId,
                Name = Name,
                MinValue = MinValue,
                MaxValue = MaxValue,
                CurrentValue = CurrentValue
            };
        }

        private async void SendNow()
        {
            await _dataService.SendCanLinDataAsync(GetCanLinData(1)); // Default equipment ID = 1
        }
    }
}
