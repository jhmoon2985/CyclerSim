using System.Threading.Tasks;
using CyclerSim.Models;
using Microsoft.Extensions.Logging;

namespace CyclerSim.Services
{
    public class DataService : IDataService
    {
        private readonly IHttpService _httpService;
        private readonly ILogger<DataService> _logger;

        public DataService(IHttpService httpService, ILogger<DataService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

        public void SetServerUrl(string serverUrl)
        {
            _httpService.SetServerUrl(serverUrl);
            _logger.LogInformation("DataService server URL updated to: {ServerUrl}", serverUrl);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var result = await _httpService.TestConnectionAsync();
                if (result)
                {
                    _logger.LogInformation("Connection test successful");
                }
                else
                {
                    _logger.LogWarning("Connection test failed");
                }
                return result;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error during connection test");
                return false;
            }
        }

        public async Task<bool> SendChannelDataAsync(ChannelData channelData)
        {
            try
            {
                var success = await _httpService.PostAsync("/api/ClientData/channel", channelData);
                if (success)
                {
                    _logger.LogDebug("Channel data sent successfully - Equipment: {EquipmentId}, Channel: {ChannelNumber}, Status: {Status}, Voltage: {Voltage}V, Current: {Current}A",
                        channelData.EquipmentId, channelData.ChannelNumber, channelData.Status, channelData.Voltage, channelData.Current);
                }
                else
                {
                    _logger.LogWarning("Failed to send channel data - Equipment: {EquipmentId}, Channel: {ChannelNumber}",
                        channelData.EquipmentId, channelData.ChannelNumber);
                }
                return success;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error sending channel data for Equipment {EquipmentId}, Channel {ChannelNumber}",
                    channelData.EquipmentId, channelData.ChannelNumber);
                return false;
            }
        }

        public async Task<bool> SendCanLinDataAsync(CanLinData canLinData)
        {
            try
            {
                var success = await _httpService.PostAsync("/api/ClientData/canlin", canLinData);
                if (success)
                {
                    _logger.LogDebug("CAN/LIN data sent successfully - Equipment: {EquipmentId}, Name: {Name}, Value: {CurrentValue}",
                        canLinData.EquipmentId, canLinData.Name, canLinData.CurrentValue);
                }
                else
                {
                    _logger.LogWarning("Failed to send CAN/LIN data - Equipment: {EquipmentId}, Name: {Name}",
                        canLinData.EquipmentId, canLinData.Name);
                }
                return success;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error sending CAN/LIN data for Equipment {EquipmentId}, Name {Name}",
                    canLinData.EquipmentId, canLinData.Name);
                return false;
            }
        }

        public async Task<bool> SendAuxDataAsync(AuxData auxData)
        {
            try
            {
                var success = await _httpService.PostAsync("/api/ClientData/aux", auxData);
                if (success)
                {
                    _logger.LogDebug("AUX data sent successfully - Equipment: {EquipmentId}, Sensor: {SensorId}, Name: {Name}, Type: {Type}, Value: {Value}",
                        auxData.EquipmentId, auxData.SensorId, auxData.Name, auxData.Type, auxData.Value);
                }
                else
                {
                    _logger.LogWarning("Failed to send AUX data - Equipment: {EquipmentId}, Sensor: {SensorId}",
                        auxData.EquipmentId, auxData.SensorId);
                }
                return success;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error sending AUX data for Equipment {EquipmentId}, Sensor {SensorId}",
                    auxData.EquipmentId, auxData.SensorId);
                return false;
            }
        }

        public async Task<bool> SendAlarmAsync(AlarmData alarmData)
        {
            try
            {
                var success = await _httpService.PostAsync("/api/ClientData/alarm", alarmData);
                if (success)
                {
                    var levelText = alarmData.Level switch
                    {
                        0 => "Info",
                        1 => "Warning",
                        2 => "Error",
                        3 => "Critical",
                        _ => "Unknown"
                    };

                    _logger.LogInformation("Alarm sent successfully - Equipment: {EquipmentId}, Level: {Level} ({LevelText}), Message: {Message}",
                        alarmData.EquipmentId, alarmData.Level, levelText, alarmData.Message);
                }
                else
                {
                    _logger.LogWarning("Failed to send alarm - Equipment: {EquipmentId}, Message: {Message}",
                        alarmData.EquipmentId, alarmData.Message);
                }
                return success;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error sending alarm for Equipment {EquipmentId}, Message: {Message}",
                    alarmData.EquipmentId, alarmData.Message);
                return false;
            }
        }
    }
}