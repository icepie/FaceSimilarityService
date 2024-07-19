using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Face.Services
{
    public class FeatureStorageService
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, float[]>> _featureStore = new();
        private readonly string _filePath;

        public FeatureStorageService(string filePath)
        {
            _filePath = filePath;
            LoadData();
        }

        public float[] GetFeature(string ipAddress, string userKey)
        {
            if (_featureStore.TryGetValue(ipAddress, out var userFeatures) && userFeatures.TryGetValue(userKey, out var feature))
            {
                return feature;
            }
            return null;
        }

        public void RegisterFeature(string ipAddress, string userKey, float[] feature)
        {
            var userFeatures = _featureStore.GetOrAdd(ipAddress, new ConcurrentDictionary<string, float[]>());
            userFeatures[userKey] = feature;
            SaveData();
        }

        public void UnregisterFeature(string ipAddress, string userKey)
        {
            if (_featureStore.TryGetValue(ipAddress, out var userFeatures))
            {
                userFeatures.TryRemove(userKey, out _);
                SaveData();
            }
        }

        public IEnumerable<KeyValuePair<string, float[]>> GetAllFeatures(string ipAddress)
        {
            if (_featureStore.TryGetValue(ipAddress, out var userFeatures))
            {
                return userFeatures;
            }
            return Enumerable.Empty<KeyValuePair<string, float[]>>();
        }

        public IEnumerable<string> GetAllFeatureKeys(string ipAddress)
        {
            if (_featureStore.TryGetValue(ipAddress, out var userFeatures))
            {
                return userFeatures.Keys;
            }
            return Enumerable.Empty<string>();
        }

        private void SaveData()
        {
            var jsonData = JsonSerializer.Serialize(_featureStore);
            File.WriteAllText(_filePath, jsonData);
        }

        private void LoadData()
        {
            if (File.Exists(_filePath))
            {
                var jsonData = File.ReadAllText(_filePath);
                var loadedData = JsonSerializer.Deserialize<ConcurrentDictionary<string, ConcurrentDictionary<string, float[]>>>(jsonData);
                if (loadedData != null)
                {
                    foreach (var kvp in loadedData)
                    {
                        _featureStore[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
    }
}
