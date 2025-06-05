using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyclerSim.Models;
using CyclerSim.Services;
using Microsoft.Extensions.Logging;

namespace CyclerSim.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly ILogger<MainViewModel> _logger;
        private readonly DispatcherTimer _simulationTimer;
        private readonly DispatcherTimer _clockTimer;

        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning;
        private string _equipmentName = "GPIMS-001";
        private string _serverUrl = "https://localhost:7090";
        private string _statusMessage = "Ready";
        private string _connectionStatus = "Disconnected";
        private Brush _connectionStatusColor = Brushes.Red;
        private int _dataSentCount;
        private DateTime _currentTime = DateTime.Now;
        private int _newAlarmLevel;
        private string _newAlarmMessage = string.Empty;

        public MainViewModel(IDataService dataService, ILogger<MainViewModel> logger)
        {
            _dataService = dataService;
            _logger = logger;

            // Initialize collections
            Channels = new ObservableCollection<ChannelViewModel>();
            CanLinData = new ObservableCollection<CanLinViewModel>();
            AuxData = new ObservableCollection<AuxViewModel>();
            AlarmHistory = new ObservableCollection<AlarmHistoryItem>();

            // Initialize commands
            StartCommand = new RelayCommand(async () => await StartSimulation(), () => CanStart);
            StopCommand = new RelayCommand(StopSimulation, () => IsRunning);
            SendAlarmCommand = new RelayCommand(async () => await SendAlarm(), () => !string.IsNullOrWhiteSpace(NewAlarmMessage));

            // Initialize timers
            _simulationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // 100ms interval
            };
            _simulationTimer.Tick += SimulationTimer_Tick;

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now;
            _clockTimer.Start();

            // Initialize data
            InitializeData();

            // Set server URL in data service
            _dataService.SetServerUrl(_serverUrl);
        }

        #region Properties

        public ObservableCollection<ChannelViewModel> Channels { get; }
        public ObservableCollection<CanLinViewModel> CanLinData { get; }
        public ObservableCollection<AuxViewModel> AuxData { get; }
        public ObservableCollection<AlarmHistoryItem> AlarmHistory { get; }

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    OnPropertyChanged(nameof(CanStart));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanStart => !IsRunning;

        public string EquipmentName
        {
            get => _equipmentName;
            set => SetProperty(ref _equipmentName, value);
        }

        public string ServerUrl
        {
            get => _serverUrl;
            set
            {
                if (SetProperty(ref _serverUrl, value))
                {
                    _dataService.SetServerUrl(value);
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public Brush ConnectionStatusColor
        {
            get => _connectionStatusColor;
            set => SetProperty(ref _connectionStatusColor, value);
        }

        public int DataSentCount
        {
            get => _dataSentCount;
            set => SetProperty(ref _dataSentCount, value);
        }

        public DateTime CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        public int NewAlarmLevel
        {
            get => _newAlarmLevel;
            set => SetProperty(ref _newAlarmLevel, value);
        }

        public string NewAlarmMessage
        {
            get => _newAlarmMessage;
            set
            {
                if (SetProperty(ref _newAlarmMessage, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SendAlarmCommand { get; }

        #endregion

        #region Private Methods

        private void InitializeData()
        {
            // Initialize 8 channels
            for (int i = 1; i <= 8; i++)
            {
                Channels.Add(new ChannelViewModel(_dataService, i));
            }

            // Initialize CAN/LIN data
            var canLinItems = new[]
            {
                ("Battery_Voltage", "Battery Voltage"),
                ("Battery_Current", "Battery Current"),
                ("Battery_SOC", "Battery SOC"),
                ("Battery_SOH", "Battery SOH"),
                ("Cell_Voltage_Min", "Cell Voltage Min"),
                ("Cell_Voltage_Max", "Cell Voltage Max"),
                ("Cell_Temp_Min", "Cell Temperature Min"),
                ("Cell_Temp_Max", "Cell Temperature Max")
            };

            foreach (var (id, name) in canLinItems)
            {
                CanLinData.Add(new CanLinViewModel(_dataService, name));
            }

            // Initialize AUX sensors
            var auxItems = new[]
            {
                ("AUX01", "Ambient Temperature", 1), // Temperature
                ("AUX02", "Chamber Temperature", 1), // Temperature
                ("AUX03", "Supply Voltage", 0),      // Voltage
                ("AUX04", "NTC Sensor 1", 2),       // NTC
                ("AUX05", "NTC Sensor 2", 2),       // NTC
                ("AUX06", "Humidity Sensor", 0)     // Voltage (representing humidity)
            };

            foreach (var (sensorId, name, type) in auxItems)
            {
                AuxData.Add(new AuxViewModel(_dataService, sensorId, name, type));
            }
        }

        private async Task StartSimulation()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                IsRunning = true;
                StatusMessage = "Starting simulation...";

                // Test connection
                var connectionTest = await _dataService.TestConnectionAsync();
                if (connectionTest)
                {
                    ConnectionStatus = "Connected";
                    ConnectionStatusColor = Brushes.Green;
                    StatusMessage = "Simulation running";

                    // Start simulation timer
                    _simulationTimer.Start();

                    _logger.LogInformation("Simulation started successfully");
                }
                else
                {
                    ConnectionStatus = "Connection Failed";
                    ConnectionStatusColor = Brushes.Red;
                    StatusMessage = "Failed to connect to server";
                    IsRunning = false;

                    _logger.LogWarning("Failed to connect to server");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting simulation");
                StatusMessage = $"Error: {ex.Message}";
                IsRunning = false;
                ConnectionStatus = "Error";
                ConnectionStatusColor = Brushes.Red;
            }
        }

        private void StopSimulation()
        {
            try
            {
                _simulationTimer.Stop();
                _cancellationTokenSource?.Cancel();
                IsRunning = false;
                ConnectionStatus = "Disconnected";
                ConnectionStatusColor = Brushes.Red;
                StatusMessage = "Simulation stopped";

                _logger.LogInformation("Simulation stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping simulation");
                StatusMessage = $"Error stopping: {ex.Message}";
            }
        }

        private async void SimulationTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsRunning || _cancellationTokenSource?.IsCancellationRequested == true)
                return;

            try
            {
                // Update simulation data
                foreach (var channel in Channels)
                {
                    channel.SimulateData();
                }

                foreach (var canLin in CanLinData)
                {
                    canLin.SimulateData();
                }

                foreach (var aux in AuxData)
                {
                    aux.SimulateData();
                }

                // Send data to server (only auto-update enabled items)
                var tasks = new List<Task>();

                // Send channel data
                foreach (var channel in Channels.Where(c => c.AutoUpdate))
                {
                    tasks.Add(_dataService.SendChannelDataAsync(channel.GetChannelData(1)));
                    channel.LastSentTime = DateTime.Now;
                }

                // Send CAN/LIN data
                foreach (var canLin in CanLinData.Where(c => c.AutoUpdate))
                {
                    tasks.Add(_dataService.SendCanLinDataAsync(canLin.GetCanLinData(1)));
                }

                // Send AUX data
                foreach (var aux in AuxData.Where(a => a.AutoUpdate))
                {
                    tasks.Add(_dataService.SendAuxDataAsync(aux.GetAuxData(1)));
                }

                // Wait for all sends to complete
                await Task.WhenAll(tasks);

                DataSentCount += tasks.Count;

                // Update connection status
                if (ConnectionStatus != "Connected")
                {
                    ConnectionStatus = "Connected";
                    ConnectionStatusColor = Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during simulation tick");
                ConnectionStatus = "Connection Error";
                ConnectionStatusColor = Brushes.Orange;
                StatusMessage = "Data transmission error";
            }
        }

        private async Task SendAlarm()
        {
            if (string.IsNullOrWhiteSpace(NewAlarmMessage))
                return;

            try
            {
                var alarmData = new AlarmData
                {
                    EquipmentId = 1,
                    Message = NewAlarmMessage,
                    Level = NewAlarmLevel
                };

                await _dataService.SendAlarmAsync(alarmData);

                // Add to history
                var levelText = NewAlarmLevel switch
                {
                    0 => "Info",
                    1 => "Warning",
                    2 => "Error",
                    3 => "Critical",
                    _ => "Unknown"
                };

                AlarmHistory.Insert(0, new AlarmHistoryItem
                {
                    Timestamp = DateTime.Now,
                    Level = levelText,
                    Message = NewAlarmMessage,
                    Status = "Sent"
                });

                // Keep only last 100 alarms
                while (AlarmHistory.Count > 100)
                {
                    AlarmHistory.RemoveAt(AlarmHistory.Count - 1);
                }

                // Clear input
                NewAlarmMessage = string.Empty;
                NewAlarmLevel = 0;

                StatusMessage = "Alarm sent successfully";
                DataSentCount++;

                _logger.LogInformation("Alarm sent: {Level} - {Message}", levelText, alarmData.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending alarm");
                StatusMessage = $"Failed to send alarm: {ex.Message}";

                // Add failed status to history
                AlarmHistory.Insert(0, new AlarmHistoryItem
                {
                    Timestamp = DateTime.Now,
                    Level = "Error",
                    Message = NewAlarmMessage,
                    Status = "Failed"
                });
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _simulationTimer?.Stop();
            _clockTimer?.Stop();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        #endregion
    }
}
