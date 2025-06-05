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
using System.Collections.Concurrent;

namespace CyclerSim.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly ILogger<MainViewModel> _logger;
        private readonly DispatcherTimer _simulationTimer;
        private readonly DispatcherTimer _dataTransmissionTimer;
        private readonly DispatcherTimer _clockTimer;

        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning;
        private int _equipmentId = 1;
        private string _equipmentName = "GPIMS-001";
        private string _serverUrl = "https://localhost:7090";
        private string _statusMessage = "Ready";
        private string _connectionStatus = "Disconnected";
        private Brush _connectionStatusColor = Brushes.Red;
        private int _dataSentCount;
        private DateTime _currentTime = DateTime.Now;
        private int _newAlarmLevel;
        private string _newAlarmMessage = string.Empty;

        // 전송 속도 최적화
        private const int SIMULATION_INTERVAL = 500;     // 500ms - UI 업데이트 간격 증가
        private const int TRANSMISSION_INTERVAL = 2000;  // 2초 - 서버 전송 간격 증가

        // 배치 처리를 위한 큐
        private readonly ConcurrentQueue<ChannelData> _channelDataQueue = new();
        private readonly ConcurrentQueue<CanLinData> _canLinDataQueue = new();
        private readonly ConcurrentQueue<AuxData> _auxDataQueue = new();

        // 연결 실패 처리
        private int _consecutiveFailures = 0;
        private const int MAX_CONSECUTIVE_FAILURES = 3;

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

            // 시뮬레이션 타이머 (UI 업데이트용) - 간격 증가
            _simulationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SIMULATION_INTERVAL)
            };
            _simulationTimer.Tick += SimulationTimer_Tick;

            // 데이터 전송 타이머 (서버 전송용) - 간격 증가 및 배치 처리
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

            // Initialize CAN/LIN data - 개수 줄임
            var canLinItems = new[]
            {
                ("Battery_Voltage", "Battery Voltage"),
                ("Battery_Current", "Battery Current"),
                ("Battery_SOC", "Battery SOC"),
                ("Cell_Voltage_Min", "Cell Voltage Min"),
                ("Cell_Temp_Min", "Cell Temperature Min")
            };

            foreach (var (id, name) in canLinItems)
            {
                CanLinData.Add(new CanLinViewModel(_dataService, name));
            }

            // Initialize AUX sensors - 개수 줄임
            var auxItems = new[]
            {
                ("AUX01", "Ambient Temperature", 1),
                ("AUX02", "Chamber Temperature", 1),
                ("AUX03", "Supply Voltage", 0),
                ("AUX04", "NTC Sensor 1", 2)
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
                _consecutiveFailures = 0;

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

                // 큐 정리
                while (_channelDataQueue.TryDequeue(out _)) { }
                while (_canLinDataQueue.TryDequeue(out _)) { }
                while (_auxDataQueue.TryDequeue(out _)) { }

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
                // UI 데이터만 업데이트하고 큐에 추가
                foreach (var channel in Channels.Where(c => c.AutoUpdate))
                {
                    channel.SimulateData();
                    _channelDataQueue.Enqueue(channel.GetChannelData(EquipmentId));
                }

                foreach (var canLin in CanLinData.Where(c => c.AutoUpdate))
                {
                    canLin.SimulateData();
                    _canLinDataQueue.Enqueue(canLin.GetCanLinData(EquipmentId));
                }

                foreach (var aux in AuxData.Where(a => a.AutoUpdate))
                {
                    aux.SimulateData();
                    _auxDataQueue.Enqueue(aux.GetAuxData(EquipmentId));
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

            // 연속 실패가 많으면 전송 간격 늘리기
            if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
            {
                _dataTransmissionTimer.Interval = TimeSpan.FromMilliseconds(TRANSMISSION_INTERVAL * 2);
                StatusMessage = "Reduced transmission rate due to connection issues";
            }

            try
            {
                var tasks = new List<Task<bool>>();

                // 배치로 큐에서 데이터 처리 (최대 개수 제한)
                var channelsToSend = new List<ChannelData>();
                var canLinToSend = new List<CanLinData>();
                var auxToSend = new List<AuxData>();

                // 큐에서 최대 5개씩만 처리
                for (int i = 0; i < 5 && _channelDataQueue.TryDequeue(out var channelData); i++)
                {
                    channelsToSend.Add(channelData);
                }

                for (int i = 0; i < 3 && _canLinDataQueue.TryDequeue(out var canLinData); i++)
                {
                    canLinToSend.Add(canLinData);
                }

                for (int i = 0; i < 3 && _auxDataQueue.TryDequeue(out var auxData); i++)
                {
                    auxToSend.Add(auxData);
                }

                // 병렬 전송 (제한된 수)
                foreach (var data in channelsToSend)
                {
                    tasks.Add(SendChannelDataSafely(data));
                }

                foreach (var data in canLinToSend)
                {
                    tasks.Add(SendCanLinDataSafely(data));
                }

                foreach (var data in auxToSend)
                {
                    tasks.Add(SendAuxDataSafely(data));
                }

                if (tasks.Any())
                {
                    // 타임아웃을 더 짧게 설정
                    var timeoutTask = Task.Delay(3000);
                    var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        _logger.LogWarning("Data transmission timeout");
                        _consecutiveFailures++;
                        ConnectionStatus = "Timeout";
                        ConnectionStatusColor = Brushes.Orange;
                    }
                    else
                    {
                        var results = await Task.WhenAll(tasks);
                        var successCount = results.Count(r => r);

                        DataSentCount += successCount;

                        if (successCount > 0)
                        {
                            _consecutiveFailures = 0;
                            ConnectionStatus = "Connected";
                            ConnectionStatusColor = Brushes.Green;
                            _dataTransmissionTimer.Interval = TimeSpan.FromMilliseconds(TRANSMISSION_INTERVAL);
                        }
                        else
                        {
                            _consecutiveFailures++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data transmission");
                _consecutiveFailures++;
                ConnectionStatus = "Connection Error";
                ConnectionStatusColor = Brushes.Orange;
                StatusMessage = "Data transmission error";
            }
        }

        // 안전한 데이터 전송 메서드들
        private async Task<bool> SendChannelDataSafely(ChannelData channelData)
        {
            try
            {
                return await _dataService.SendChannelDataAsync(channelData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send channel {Channel} data", channelData.ChannelNumber);
                return false;
            }
        }

        private async Task<bool> SendCanLinDataSafely(CanLinData canLinData)
        {
            try
            {
                return await _dataService.SendCanLinDataAsync(canLinData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send CAN/LIN {Name} data", canLinData.Name);
                return false;
            }
        }

        private async Task<bool> SendAuxDataSafely(AuxData auxData)
        {
            try
            {
                return await _dataService.SendAuxDataAsync(auxData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send AUX {SensorId} data", auxData.SensorId);
                return false;
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
                    EquipmentId = EquipmentId,
                    Message = NewAlarmMessage,
                    Level = NewAlarmLevel
                };

                await _dataService.SendAlarmAsync(alarmData);

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

                // 최대 50개만 유지
                while (AlarmHistory.Count > 50)
                {
                    AlarmHistory.RemoveAt(AlarmHistory.Count - 1);
                }

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
            _dataTransmissionTimer?.Stop();
            _clockTimer?.Stop();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        #endregion
    }
}