using System;
using System.Windows;
using System.Windows.Input;
using CyclerSim.Models;
using CyclerSim.Services;

namespace CyclerSim.ViewModels
{
    public class AuxViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private string _sensorId = string.Empty;
        private string _name = string.Empty;
        private int _type;
        private double _value;
        private bool _autoUpdate = true;

        public AuxViewModel(IDataService dataService, string sensorId, string name, int type)
        {
            _dataService = dataService;
            _sensorId = sensorId;
            _name = name;
            _type = type;

            // Initialize with default values based on type
            _value = type switch
            {
                0 => 3.3,    // Voltage
                1 => 25.0,   // Temperature
                2 => 1000.0, // NTC
                _ => 0.0
            };

            SendNowCommand = new RelayCommand(SendNow);
        }

        public string SensorId
        {
            get => _sensorId;
            set => SetProperty(ref _sensorId, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    // Reset value based on new type
                    Value = value switch
                    {
                        0 => 3.3,    // Voltage
                        1 => 25.0,   // Temperature
                        2 => 1000.0, // NTC
                        _ => 0.0
                    };
                }
            }
        }

        public double Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
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

            switch (Type)
            {
                case 0: // Voltage (0-5V)
                    Value += (random.NextDouble() - 0.5) * 0.2;
                    Value = Math.Max(0, Math.Min(5.0, Value));
                    break;
                case 1: // Temperature (-40 to 85°C)
                    Value += (random.NextDouble() - 0.5) * 2.0;
                    Value = Math.Max(-40, Math.Min(85, Value));
                    break;
                case 2: // NTC (100-10000 Ohm)
                    Value += (random.NextDouble() - 0.5) * 100;
                    Value = Math.Max(100, Math.Min(10000, Value));
                    break;
            }
        }

        public AuxData GetAuxData(int equipmentId)
        {
            return new AuxData
            {
                EquipmentId = equipmentId,
                SensorId = SensorId,
                Name = Name,
                Type = Type,
                Value = Value
            };
        }

        private async void SendNow()
        {
            var mainViewModel = Application.Current.MainWindow?.DataContext as MainViewModel;
            int equipmentId = mainViewModel?.EquipmentId ?? 1;

            await _dataService.SendAuxDataAsync(GetAuxData(equipmentId));
        }
    }
}
