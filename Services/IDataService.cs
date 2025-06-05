using System.Threading.Tasks;
using CyclerSim.Models;

namespace CyclerSim.Services
{
    public interface IDataService
    {
        void SetServerUrl(string serverUrl);
        Task<bool> TestConnectionAsync();
        Task<bool> SendChannelDataAsync(ChannelData channelData);
        Task<bool> SendCanLinDataAsync(CanLinData canLinData);
        Task<bool> SendAuxDataAsync(AuxData auxData);
        Task<bool> SendAlarmAsync(AlarmData alarmData);
    }
}