using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    public class JsonDataStorageService : IDataStorageService, IDisposable
    {
        private readonly string _storagePath;
        private readonly ILogger<JsonDataStorageService> _logger;
        private readonly SemaphoreSlim _semaphore = new(1, 1); // Control concurrent access
        private bool _disposed = false;

        public JsonDataStorageService(ILogger<JsonDataStorageService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                var appDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

                // Validate and create directory safely
                if (!PathValidator.IsValidPath(appDataDir))
                {
                    throw new InvalidOperationException($"Invalid storage directory path: {appDataDir}");
                }

                Directory.CreateDirectory(appDataDir);
                _storagePath = Path.Combine(appDataDir, "services.json");

                _logger.LogInformation("JsonDataStorageService initialized with storage path: {StoragePath}", _storagePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize JsonDataStorageService");
                throw new InvalidOperationException("Failed to initialize storage service", ex);
            }
        }

        public async Task<List<ServiceItem>> LoadServicesAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Loading services from storage");

                if (!File.Exists(_storagePath))
                {
                    _logger.LogInformation("Storage file does not exist, returning empty list");
                    return new List<ServiceItem>();
                }

                var json = await File.ReadAllTextAsync(_storagePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning("Storage file is empty, returning empty list");
                    return new List<ServiceItem>();
                }

                var data = JsonConvert.DeserializeObject<StorageData>(json);
                var services = data?.Services ?? new List<ServiceItem>();

                _logger.LogInformation("Loaded {Count} services from storage", services.Count);
                return services;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load services from storage");
                return new List<ServiceItem>();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SaveServicesAsync(List<ServiceItem> services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            await _semaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Saving {Count} services to storage", services.Count);

                var data = new StorageData { Services = services };
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);

                // Create backup before overwriting
                if (File.Exists(_storagePath))
                {
                    string backupPath = _storagePath + ".backup";
                    File.Copy(_storagePath, backupPath, overwrite: true);
                    _logger.LogDebug("Created backup of storage file");
                }

                await File.WriteAllTextAsync(_storagePath, json);
                _logger.LogInformation("Successfully saved {Count} services to storage", services.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save services to storage");

                // Try to restore from backup if it exists
                string backupPath = _storagePath + ".backup";
                if (File.Exists(backupPath))
                {
                    try
                    {
                        File.Copy(backupPath, _storagePath, overwrite: true);
                        _logger.LogInformation("Restored storage file from backup");
                    }
                    catch (Exception restoreEx)
                    {
                        _logger.LogError(restoreEx, "Failed to restore storage file from backup");
                    }
                }

                throw new InvalidOperationException("Failed to save services", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ServiceItem?> GetServiceAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Service ID cannot be null or empty", nameof(id));

            try
            {
                var services = await LoadServicesAsync();
                var service = services.Find(s => s.Id == id);

                if (service != null)
                {
                    _logger.LogDebug("Found service with ID: {ServiceId}", id);
                }
                else
                {
                    _logger.LogDebug("Service not found with ID: {ServiceId}", id);
                }

                return service;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get service with ID: {ServiceId}", id);
                return null;
            }
        }

        public async Task AddServiceAsync(ServiceItem service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            try
            {
                _logger.LogDebug("Adding service with ID: {ServiceId}", service.Id);

                var services = await LoadServicesAsync();

                // Check for duplicate
                if (services.Exists(s => s.Id == service.Id))
                {
                    throw new InvalidOperationException($"Service with ID '{service.Id}' already exists");
                }

                services.Add(service);
                await SaveServicesAsync(services);

                _logger.LogInformation("Successfully added service with ID: {ServiceId}", service.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add service with ID: {ServiceId}", service?.Id);
                throw new InvalidOperationException("Failed to add service", ex);
            }
        }

        public async Task UpdateServiceAsync(ServiceItem service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            try
            {
                _logger.LogDebug("Updating service with ID: {ServiceId}", service.Id);

                var services = await LoadServicesAsync();
                var index = services.FindIndex(s => s.Id == service.Id);

                if (index >= 0)
                {
                    services[index] = service;
                    await SaveServicesAsync(services);
                    _logger.LogInformation("Successfully updated service with ID: {ServiceId}", service.Id);
                }
                else
                {
                    _logger.LogWarning("Service not found for update with ID: {ServiceId}", service.Id);
                    throw new KeyNotFoundException($"Service with ID '{service.Id}' not found");
                }
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Failed to update service with ID: {ServiceId}", service?.Id);
                throw new InvalidOperationException("Failed to update service", ex);
            }
        }

        public async Task DeleteServiceAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Service ID cannot be null or empty", nameof(id));

            try
            {
                _logger.LogDebug("Deleting service with ID: {ServiceId}", id);

                var services = await LoadServicesAsync();
                var removed = services.RemoveAll(s => s.Id == id);

                if (removed > 0)
                {
                    await SaveServicesAsync(services);
                    _logger.LogInformation("Successfully deleted service with ID: {ServiceId}", id);
                }
                else
                {
                    _logger.LogWarning("Service not found for deletion with ID: {ServiceId}", id);
                    throw new KeyNotFoundException($"Service with ID '{id}' not found");
                }
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Failed to delete service with ID: {ServiceId}", id);
                throw new InvalidOperationException("Failed to delete service", ex);
            }
        }

        private class StorageData
        {
            public List<ServiceItem> Services { get; set; } = new List<ServiceItem>();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _logger.LogDebug("Disposing JsonDataStorageService");

                _semaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}