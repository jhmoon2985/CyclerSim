using System;
using System.Threading.Tasks;

namespace CyclerSim.Services
{
    public interface IHttpService
    {
        void SetServerUrl(string serverUrl);
        Task<bool> PostAsync<T>(string endpoint, T data);
        Task<bool> TestConnectionAsync();
    }
}