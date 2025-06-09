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
                    _logger.LogDebug("Channel data sent - Equipment: {EquipmentId}, Channel: {ChannelNumber}",
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

        // 새로운 배치 전송 메서드
        public async Task<bool> SendChannelDataBatchAsync(List<ChannelData> channelDataList)
        {
            try
            {
                var success = await _httpService.PostAsync("/api/ClientData/channels/batch", channelDataList);
                if (success)
                {
                    _logger.LogDebug("Channel batch data sent - {Count} channels for Equipment {EquipmentId}",
                        channelDataList.Count, channelDataList.Count > 0 ? channelDataList[0].EquipmentId : 0);
                }
                else
                {
                    _logger.LogWarning("Failed to send channel batch data - {Count} channels",
                        channelDataList.Count);
                }
                return success;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error sending channel batch data - {Count} channels",
                    channelDataList.Count);
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
                    _logger.LogDebug("CAN/LIN data sent - Equipment: {EquipmentId}, Name: {Name}",
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
                    _logger.LogDebug("AUX data sent - Equipment: {EquipmentId}, Sensor: {SensorId}",
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
                    _logger.LogInformation("Alarm sent - Equipment: {EquipmentId}, Level: {Level}, Message: {Message}",
                        alarmData.EquipmentId, alarmData.Level, alarmData.Message);
                }
                return success;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error sending alarm for Equipment {EquipmentId}",
                    alarmData.EquipmentId);
                return false;
            }
        }
    }
}