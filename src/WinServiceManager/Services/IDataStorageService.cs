using System.Collections.Generic;
using System.Threading.Tasks;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    public interface IDataStorageService
    {
        Task<List<ServiceItem>> LoadServicesAsync();
        Task SaveServicesAsync(List<ServiceItem> services);
        Task<ServiceItem?> GetServiceAsync(string id);
        Task AddServiceAsync(ServiceItem service);
        Task UpdateServiceAsync(ServiceItem service);
        Task DeleteServiceAsync(string id);
    }
}