using System.Threading.Tasks;
using CyclerSim.Models;

namespace CyclerSim.Services
{
    public interface IDataService
    {
        void SetServerUrl(string serverUrl);
        Task<bool> TestConnectionAsync();
        Task<bool> SendChannelDataAsync(ChannelData channelData);
        Task<bool> SendChannelDataBatchAsync(List<ChannelData> channelDataList); // 새로운 배치 메서드
        Task<bool> SendCanLinDataAsync(CanLinData canLinData);
        Task<bool> SendAuxDataAsync(AuxData auxData);
        Task<bool> SendAlarmAsync(AlarmData alarmData);
    }
}