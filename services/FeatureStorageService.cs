using System.Collections.Concurrent;
using System.Text.Json;

namespace FaceSimilarityService.Services
{
    public class FeatureStorageService
    {
        private readonly ConcurrentDictionary<string, float[]> _featureStore = new();
        private readonly string _filePath;

        public FeatureStorageService(string filePath)
        {
            _filePath = filePath;
            LoadData();
        }

        public float[]? GetFeature(string userKey)
        {
            _featureStore.TryGetValue(userKey, out var feature);
            return feature;
        }

        public void RegisterFeature(string userKey, float[] feature)
        {
            _featureStore[userKey] = feature;
            SaveData();
        }

        public void UnregisterFeature(string userKey)
        {
            _featureStore.TryRemove(userKey, out _);
            SaveData();
        }

        public IEnumerable<KeyValuePair<string, float[]>> GetAllFeatures()
        {
            return _featureStore;
        }

        public IEnumerable<string> GetAllFeatureKeys()
        {
            return _featureStore.Keys;
        }

        private void SaveData()
        {
            try
            {
                var jsonData = JsonSerializer.Serialize(_featureStore, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_filePath, jsonData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存数据失败: {ex.Message}");
                // 尝试备份原文件
                try
                {
                    if (File.Exists(_filePath))
                    {
                        var backupPath = $"{_filePath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
                        File.Copy(_filePath, backupPath);
                        Console.WriteLine($"已备份原文件到: {backupPath}");
                    }
                }
                catch (Exception backupEx)
                {
                    Console.WriteLine($"备份文件失败: {backupEx.Message}");
                }
            }
        }

        private void LoadData()
        {
            if (!File.Exists(_filePath))
            {
                Console.WriteLine($"存储文件不存在: {_filePath}");
                return;
            }

            try
            {
                var jsonData = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(jsonData))
                {
                    Console.WriteLine("存储文件为空");
                    return;
                }

                // 尝试解析为新的格式（直接存储用户特征）
                try
                {
                    var loadedData = JsonSerializer.Deserialize<ConcurrentDictionary<string, float[]>>(jsonData);
                    if (loadedData != null)
                    {
                        foreach (var kvp in loadedData)
                        {
                            _featureStore[kvp.Key] = kvp.Value;
                        }
                        Console.WriteLine($"成功加载 {_featureStore.Count} 个用户特征");
                        return;
                    }
                }
                catch (JsonException)
                {
                    // 如果新格式解析失败，尝试旧格式（IP区分的格式）
                    Console.WriteLine("尝试解析旧格式数据...");
                }

                // 尝试解析旧格式（IP区分的格式）
                try
                {
                    var oldFormatData = JsonSerializer.Deserialize<ConcurrentDictionary<string, ConcurrentDictionary<string, float[]>>>(jsonData);
                    if (oldFormatData != null)
                    {
                        int migratedCount = 0;
                        foreach (var ipGroup in oldFormatData)
                        {
                            foreach (var userFeature in ipGroup.Value)
                            {
                                _featureStore[userFeature.Key] = userFeature.Value;
                                migratedCount++;
                            }
                        }
                        Console.WriteLine($"成功迁移 {migratedCount} 个用户特征从旧格式");
                        
                        // 迁移完成后保存为新格式
                        SaveData();
                        return;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"解析旧格式数据失败: {ex.Message}");
                }

                Console.WriteLine("无法解析存储文件，将使用空数据");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载数据失败: {ex.Message}");
                // 尝试备份损坏的文件
                try
                {
                    var backupPath = $"{_filePath}.corrupted.{DateTime.Now:yyyyMMddHHmmss}";
                    File.Copy(_filePath, backupPath);
                    Console.WriteLine($"已备份损坏文件到: {backupPath}");
                }
                catch (Exception backupEx)
                {
                    Console.WriteLine($"备份损坏文件失败: {backupEx.Message}");
                }
            }
        }
    }
}
