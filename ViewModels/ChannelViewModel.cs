using System;
using System.Windows.Input;
using System.Windows.Media;
using CyclerSim.Models;
using CyclerSim.Services;

namespace CyclerSim.ViewModels
{
    public class ChannelViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private int _channelNumber;
        private int _status;
        private int _mode;
        private double _voltage;
        private double _current;
        private double _capacity;
        private double _power;
        private double _energy;
        private string _scheduleName = string.Empty;
        private bool _autoUpdate = true;
        private DateTime _lastSentTime;

        public ChannelViewModel(IDataService dataService, int channelNumber)
        {
            _dataService = dataService;
            _channelNumber = channelNumber;

            // Initialize with default values
            _status = 0; // Idle
            _mode = 0; // Rest
            _voltage = 3.7 + (channelNumber * 0.1);
            _current = 0.0;
            _capacity = 50.0 + (channelNumber * 5);
            _power = 0.0;
            _energy = 0.0;
            _scheduleName = $"Schedule_{channelNumber}";

            SendNowCommand = new RelayCommand(SendNow);
        }

        public int ChannelNumber
        {
            get => _channelNumber;
            set => SetProperty(ref _channelNumber, value);
        }

        public string HeaderText => $"Channel {ChannelNumber:D2}";

        public int Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(StatusColor));
                    UpdatePowerCalculation();
                }
            }
        }

        public int Mode
        {
            get => _mode;
            set
            {
                if (SetProperty(ref _mode, value))
                {
                    UpdatePowerCalculation();
                }
            }
        }

        public double Voltage
        {
            get => _voltage;
            set
            {
                if (SetProperty(ref _voltage, value))
                {
                    UpdatePowerCalculation();
                }
            }
        }

        public double Current
        {
            get => _current;
            set
            {
                if (SetProperty(ref _current, value))
                {
                    UpdatePowerCalculation();
                }
            }
        }

        public double Capacity
        {
            get => _capacity;
            set => SetProperty(ref _capacity, value);
        }

        public double Power
        {
            get => _power;
            private set => SetProperty(ref _power, value);
        }

        public double Energy
        {
            get => _energy;
            set => SetProperty(ref _energy, value);
        }

        public string ScheduleName
        {
            get => _scheduleName;
            set => SetProperty(ref _scheduleName, value);
        }

        public bool AutoUpdate
        {
            get => _autoUpdate;
            set => SetProperty(ref _autoUpdate, value);
        }

        public DateTime LastSentTime
        {
            get => _lastSentTime;
            set => SetProperty(ref _lastSentTime, value);
        }

        public Brush StatusColor
        {
            get
            {
                return Status switch
                {
                    0 => Brushes.Gray,      // Idle
                    1 => Brushes.Green,     // Active
                    2 => Brushes.Red,       // Error
                    3 => Brushes.Orange,    // Pause
                    _ => Brushes.Gray
                };
            }
        }

        public ICommand SendNowCommand { get; }

        private void UpdatePowerCalculation()
        {
            Power = Status == 1 ? Math.Abs(Voltage * Current) : 0.0; // Active 상태일 때만 전력 계산
        }

        public void SimulateData()
        {
            if (!AutoUpdate || Status != 1) return; // Active 상태일 때만 시뮬레이션

            var random = new Random();

            // 전압 변동 (±0.1V)
            Voltage += (random.NextDouble() - 0.5) * 0.2;
            Voltage = Math.Max(0, Math.Min(5.0, Voltage));

            // 전류 변동 (모드에 따라)
            if (Mode == 1 || Mode == 2) // Charge or Discharge
            {
                Current += (random.NextDouble() - 0.5) * 0.4;
                Current = Math.Max(0, Math.Min(5.0, Current));
            }

            // 에너지 누적 (Active 상태일 때)
            if (Status == 1 && Power > 0)
            {
                Energy += Power * (0.1 / 3600); // 100ms 간격으로 누적
            }
        }

        public ChannelData GetChannelData(int equipmentId)
        {
            return new ChannelData
            {
                EquipmentId = equipmentId,
                ChannelNumber = ChannelNumber,
                Status = Status,
                Mode = Mode,
                Voltage = Voltage,
                Current = Current,
                Capacity = Capacity,
                Power = Power,
                Energy = Energy,
                ScheduleName = ScheduleName
            };
        }

        private async void SendNow()
        {
            await _dataService.SendChannelDataAsync(GetChannelData(1)); // Default equipment ID = 1
            LastSentTime = DateTime.Now;
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

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();
    }
}
