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
        private readonly DispatcherTimer _dataTransmissionTimer; // 별도 타이머 추가
        private readonly DispatcherTimer _clockTimer;

        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning;
        private int _equipmentId = 1; // Equipment ID 추가
        private string _equipmentName = "GPIMS-001";
        private string _serverUrl = "https://localhost:7090";
        private string _statusMessage = "Ready";
        private string _connectionStatus = "Disconnected";
        private Brush _connectionStatusColor = Brushes.Red;
        private int _dataSentCount;
        private DateTime _currentTime = DateTime.Now;
        private int _newAlarmLevel;
        private string _newAlarmMessage = string.Empty;

        // 데이터 전송 주기 조정
        private const int SIMULATION_INTERVAL = 100;     // 100ms - UI 업데이트용
        private const int TRANSMISSION_INTERVAL = 1000;  // 1초 - 서버 전송용

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

            // 시뮬레이션 타이머 (UI 업데이트용)
            _simulationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SIMULATION_INTERVAL)
            };
            _simulationTimer.Tick += SimulationTimer_Tick;

            // 데이터 전송 타이머 (서버 전송용)
            _dataTransmissionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(TRANSMISSION_INTERVAL)
            };
            _dataTransmissionTimer.Tick += DataTransmissionTimer_Tick;

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

        public int EquipmentId
        {
            get => _equipmentId;
            set
            {
                if (SetProperty(ref _equipmentId, value))
                {
                    // Equipment ID가 변경되면 Equipment Name도 업데이트
                    EquipmentName = $"GPIMS-{value:D3}";
                }
            }
        }

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

                // 연결 테스트
                var connectionTest = await _dataService.TestConnectionAsync();
                if (connectionTest)
                {
                    ConnectionStatus = "Connected";
                    ConnectionStatusColor = Brushes.Green;
                    StatusMessage = "Simulation running";

                    // 두 개의 타이머 모두 시작
                    _simulationTimer.Start();
                    _dataTransmissionTimer.Start();

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
                _dataTransmissionTimer.Stop();
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
                // UI 데이터만 업데이트 (서버 전송 안함)
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during simulation tick");
            }
        }

        private async void DataTransmissionTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsRunning || _cancellationTokenSource?.IsCancellationRequested == true)
                return;

            try
            {
                var tasks = new List<Task>();

                // 배치로 데이터 전송 (AutoUpdate 활성화된 것만)
                var activeChannels = Channels.Where(c => c.AutoUpdate).ToList();
                var activeCanLin = CanLinData.Where(c => c.AutoUpdate).ToList();
                var activeAux = AuxData.Where(a => a.AutoUpdate).ToList();

                // 모든 데이터를 병렬로 전송
                foreach (var channel in activeChannels)
                {
                    tasks.Add(SendChannelDataSafely(channel));
                }

                foreach (var canLin in activeCanLin)
                {
                    tasks.Add(SendCanLinDataSafely(canLin));
                }

                foreach (var aux in activeAux)
                {
                    tasks.Add(SendAuxDataSafely(aux));
                }

                // 모든 전송 완료 대기 (타임아웃 설정)
                var timeoutTask = Task.Delay(5000); // 5초 타임아웃
                var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("Data transmission timeout");
                    ConnectionStatus = "Timeout";
                    ConnectionStatusColor = Brushes.Orange;
                }
                else
                {
                    DataSentCount += tasks.Count;

                    // 연결 상태 업데이트
                    if (ConnectionStatus != "Connected")
                    {
                        ConnectionStatus = "Connected";
                        ConnectionStatusColor = Brushes.Green;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data transmission");
                ConnectionStatus = "Connection Error";
                ConnectionStatusColor = Brushes.Orange;
                StatusMessage = "Data transmission error";
            }
        }

        // 안전한 데이터 전송 메서드들
        private async Task SendChannelDataSafely(ChannelViewModel channel)
        {
            try
            {
                await _dataService.SendChannelDataAsync(channel.GetChannelData(EquipmentId));
                channel.LastSentTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send channel {Channel} data", channel.ChannelNumber);
            }
        }

        private async Task SendCanLinDataSafely(CanLinViewModel canLin)
        {
            try
            {
                await _dataService.SendCanLinDataAsync(canLin.GetCanLinData(EquipmentId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send CAN/LIN {Name} data", canLin.Name);
            }
        }

        private async Task SendAuxDataSafely(AuxViewModel aux)
        {
            try
            {
                await _dataService.SendAuxDataAsync(aux.GetAuxData(EquipmentId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send AUX {SensorId} data", aux.SensorId);
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
                    EquipmentId = EquipmentId, // Equipment ID 사용
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
