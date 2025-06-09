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
using System.Collections.Generic;

namespace CyclerSim.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly ILogger<MainViewModel> _logger;
        private readonly DispatcherTimer _simulationTimer;
        private readonly DispatcherTimer _batchTransmissionTimer;
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

        // 고성능 설정: 100ms 시뮬레이션, 100ms 배치 전송
        private const int SIMULATION_INTERVAL = 100;      // 100ms
        private const int BATCH_TRANSMISSION_INTERVAL = 100;  // 100ms 배치 전송
        private const int MAX_CHANNELS = 128;             // 최대 128채널 지원

        // 고성능 배치 데이터 저장소
        private readonly List<ChannelData> _channelDataBatch = new();
        private readonly List<CanLinData> _canLinDataBatch = new();
        private readonly List<AuxData> _auxDataBatch = new();

        // 스레드 안전한 락
        private readonly object _batchLock = new object();

        // 성능 모니터링
        private int _batchesSent = 0;
        private DateTime _lastPerformanceReport = DateTime.Now;
        private long _totalDataPointsSent = 0;

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

            // 고성능 시뮬레이션 타이머 (100ms)
            _simulationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SIMULATION_INTERVAL)
            };
            _simulationTimer.Tick += SimulationTimer_Tick;

            // 배치 전송 타이머 (100ms)
            _batchTransmissionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(BATCH_TRANSMISSION_INTERVAL)
            };
            _batchTransmissionTimer.Tick += BatchTransmissionTimer_Tick;

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now;
            _clockTimer.Start();

            // Initialize data with more channels
            InitializeData();

            // Set server URL
            _dataService.SetServerUrl(_serverUrl);

            _logger.LogInformation("High-performance CyclerSim initialized for {MaxChannels} channels", MAX_CHANNELS);
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
            // 128채널 초기화 (UI 표시용은 제한)
            var uiChannelCount = Math.Min(16, MAX_CHANNELS); // UI에는 16개만 표시
            for (int i = 1; i <= uiChannelCount; i++)
            {
                Channels.Add(new ChannelViewModel(_dataService, i));
            }

            // CAN/LIN 데이터 최소화
            var canLinItems = new[]
            {
                "Battery_Voltage",
                "Battery_Current",
                "Battery_SOC"
            };

            foreach (var name in canLinItems)
            {
                CanLinData.Add(new CanLinViewModel(_dataService, name));
            }

            // AUX 센서 최소화
            var auxItems = new[]
            {
                ("AUX01", "Temperature", 1),
                ("AUX02", "Voltage", 0)
            };

            foreach (var (sensorId, name, type) in auxItems)
            {
                AuxData.Add(new AuxViewModel(_dataService, sensorId, name, type));
            }

            _logger.LogInformation("Initialized {UIChannels} UI channels for {TotalChannels} total channels",
                uiChannelCount, MAX_CHANNELS);
        }

        private async Task StartSimulation()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                IsRunning = true;
                StatusMessage = "Starting high-performance simulation...";
                _batchesSent = 0;
                _totalDataPointsSent = 0;
                _lastPerformanceReport = DateTime.Now;

                // 연결 테스트
                var connectionTest = await _dataService.TestConnectionAsync();
                if (connectionTest)
                {
                    ConnectionStatus = "Connected";
                    ConnectionStatusColor = Brushes.Green;
                    StatusMessage = "High-performance simulation running (100ms intervals)";

                    // 배치 데이터 초기화
                    lock (_batchLock)
                    {
                        _channelDataBatch.Clear();
                        _canLinDataBatch.Clear();
                        _auxDataBatch.Clear();
                    }

                    // 타이머 시작
                    _simulationTimer.Start();
                    _batchTransmissionTimer.Start();

                    _logger.LogInformation("High-performance simulation started - 128 channels at 100ms intervals");
                }
                else
                {
                    ConnectionStatus = "Connection Failed";
                    ConnectionStatusColor = Brushes.Red;
                    StatusMessage = "Failed to connect to server";
                    IsRunning = false;
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
                _batchTransmissionTimer.Stop();
                _cancellationTokenSource?.Cancel();
                IsRunning = false;
                ConnectionStatus = "Disconnected";
                ConnectionStatusColor = Brushes.Red;

                // 최종 성능 리포트
                var totalTime = DateTime.Now - _lastPerformanceReport;
                var avgBatchesPerSec = _batchesSent / totalTime.TotalSeconds;
                var avgDataPointsPerSec = _totalDataPointsSent / totalTime.TotalSeconds;

                StatusMessage = $"Simulation stopped. Avg: {avgBatchesPerSec:F1} batches/sec, {avgDataPointsPerSec:F0} data points/sec";

                lock (_batchLock)
                {
                    _channelDataBatch.Clear();
                    _canLinDataBatch.Clear();
                    _auxDataBatch.Clear();
                }

                _logger.LogInformation("High-performance simulation stopped. Total batches: {Batches}, Total data points: {DataPoints}",
                    _batchesSent, _totalDataPointsSent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping simulation");
            }
        }

        private void SimulationTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsRunning || _cancellationTokenSource?.IsCancellationRequested == true)
                return;

            try
            {
                // 128채널 모든 데이터 생성 (실제 운영 시뮬레이션)
                var channelDataList = new List<ChannelData>();
                var canLinDataList = new List<CanLinData>();
                var auxDataList = new List<AuxData>();

                // 모든 128채널 데이터 생성
                for (int i = 1; i <= MAX_CHANNELS; i++)
                {
                    channelDataList.Add(GenerateChannelData(i));
                }

                // CAN/LIN 데이터 생성
                foreach (var canLin in CanLinData)
                {
                    canLin.SimulateData();
                    canLinDataList.Add(canLin.GetCanLinData(EquipmentId));
                }

                // AUX 데이터 생성
                foreach (var aux in AuxData)
                {
                    aux.SimulateData();
                    auxDataList.Add(aux.GetAuxData(EquipmentId));
                }

                // UI 업데이트 (처음 16개 채널만)
                for (int i = 0; i < Math.Min(Channels.Count, 16); i++)
                {
                    if (Channels[i].AutoUpdate)
                    {
                        Channels[i].SimulateData();
                    }
                }

                // 배치에 데이터 추가
                lock (_batchLock)
                {
                    _channelDataBatch.AddRange(channelDataList);
                    _canLinDataBatch.AddRange(canLinDataList);
                    _auxDataBatch.AddRange(auxDataList);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during simulation tick");
            }
        }

        private ChannelData GenerateChannelData(int channelNumber)
        {
            var random = new Random(channelNumber + Environment.TickCount);
            var isActive = channelNumber <= 64; // 절반만 활성화

            return new ChannelData
            {
                EquipmentId = EquipmentId,
                ChannelNumber = channelNumber,
                Status = isActive ? 1 : 0, // Active or Idle
                Mode = random.Next(0, 5), // Random mode
                Voltage = isActive ? 3.7 + (random.NextDouble() * 0.6) : 0,
                Current = isActive ? 1.0 + (random.NextDouble() * 2.0) : 0,
                Capacity = 50.0 + (channelNumber * 2),
                Power = 0, // Will be calculated by server
                Energy = isActive ? random.NextDouble() * 100 : 0,
                ScheduleName = isActive ? $"Schedule_{channelNumber}" : ""
            };
        }

        private async void BatchTransmissionTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsRunning || _cancellationTokenSource?.IsCancellationRequested == true)
                return;

            try
            {
                List<ChannelData> channelsToSend;
                List<CanLinData> canLinToSend;
                List<AuxData> auxToSend;

                // 배치 데이터 가져오기
                lock (_batchLock)
                {
                    channelsToSend = new List<ChannelData>(_channelDataBatch);
                    canLinToSend = new List<CanLinData>(_canLinDataBatch);
                    auxToSend = new List<AuxData>(_auxDataBatch);

                    _channelDataBatch.Clear();
                    _canLinDataBatch.Clear();
                    _auxDataBatch.Clear();
                }

                if (channelsToSend.Count == 0 && canLinToSend.Count == 0 && auxToSend.Count == 0)
                    return;

                // 배치 전송
                var success = await SendBatchData(channelsToSend, canLinToSend, auxToSend);

                if (success)
                {
                    var totalDataPoints = channelsToSend.Count + canLinToSend.Count + auxToSend.Count;
                    DataSentCount += totalDataPoints;
                    _batchesSent++;
                    _totalDataPointsSent += totalDataPoints;

                    ConnectionStatus = "Connected";
                    ConnectionStatusColor = Brushes.Green;

                    // 성능 모니터링 (5초마다)
                    if (DateTime.Now - _lastPerformanceReport > TimeSpan.FromSeconds(5))
                    {
                        var batchesPerSec = _batchesSent / 5.0;
                        var dataPointsPerSec = (_totalDataPointsSent - _dataSentCount) / 5.0;

                        StatusMessage = $"Running: {batchesPerSec:F1} batches/sec, {dataPointsPerSec:F0} points/sec, {channelsToSend.Count} channels";

                        _lastPerformanceReport = DateTime.Now;
                        _batchesSent = 0;
                    }
                }
                else
                {
                    ConnectionStatus = "Transmission Failed";
                    ConnectionStatusColor = Brushes.Orange;
                    _logger.LogWarning("Batch transmission failed for {Channels} channels", channelsToSend.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during batch transmission");
                ConnectionStatus = "Error";
                ConnectionStatusColor = Brushes.Red;
            }
        }

        private async Task<bool> SendBatchData(List<ChannelData> channels, List<CanLinData> canLin, List<AuxData> aux)
        {
            try
            {
                var tasks = new List<Task<bool>>();

                // 채널 데이터를 배치로 전송
                if (channels.Count > 0)
                {
                    tasks.Add(_dataService.SendChannelDataBatchAsync(channels));
                }

                // CAN/LIN 데이터 개별 전송 (수가 적으므로)
                foreach (var data in canLin)
                {
                    tasks.Add(_dataService.SendCanLinDataAsync(data));
                }

                // AUX 데이터 개별 전송 (수가 적으므로)
                foreach (var data in aux)
                {
                    tasks.Add(_dataService.SendAuxDataAsync(data));
                }

                if (tasks.Count == 0) return true;

                // 모든 전송 작업 완료 대기 (타임아웃 1초)
                var completedTask = await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(1000));

                if (completedTask is Task<bool[]> resultTask)
                {
                    var results = await resultTask;
                    return results.All(r => r);
                }
                else
                {
                    _logger.LogWarning("Batch transmission timeout");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending batch data");
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

                while (AlarmHistory.Count > 50)
                {
                    AlarmHistory.RemoveAt(AlarmHistory.Count - 1);
                }

                NewAlarmMessage = string.Empty;
                StatusMessage = "Alarm sent successfully";
                DataSentCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending alarm");
                StatusMessage = $"Failed to send alarm: {ex.Message}";
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _simulationTimer?.Stop();
            _batchTransmissionTimer?.Stop();
            _clockTimer?.Stop();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        #endregion
    }
}