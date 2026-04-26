using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Game;

/// <summary>
/// 障碍物配置（服务端简化版，不依赖 Godot）
/// </summary>
public class ServerObstacleConfig
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("w")]
    public float W { get; set; }

    [JsonPropertyName("h")]
    public float H { get; set; }
    [JsonPropertyName("sprite")]
    public string Sprite { get; set; } = "";
}

/// <summary>
/// 地图配置（服务端简化版）
/// </summary>
public class ServerMapConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("background")]
    public string Background { get; set; } = "";

    [JsonPropertyName("obstacles")]
    public List<ServerObstacleConfig> Obstacles { get; set; } = new();
}

/// <summary>
/// 服务端地图加载器（不依赖 Godot）
/// </summary>
public sealed class ServerMapLoader
{
    private static readonly Lazy<ServerMapLoader> _instance = new(() => new ServerMapLoader());
    public static ServerMapLoader Instance => _instance.Value;
    
    private readonly List<ServerMapConfig> _maps = new();
    
    // 提供默认地图作为后备
    private readonly ServerMapConfig _defaultMap = new()
    {
        Id = "default",
        Background = "",
        Obstacles = new List<ServerObstacleConfig>()
    };
    
    private bool _initialized;

    /// <summary>
    /// 初始化地图配置（测试用默认配置，生产环境应从文件加载）
    /// </summary>
    public void Initialize()
    {
        // 生产环境应从 JSON 文件加载
        // 这里使用默认配置
        _maps.Clear();
        _maps.Add(_defaultMap);
        _initialized = true;
    }

    /// <summary>
    /// 加载 JSON 字符串并解析为地图配置
    /// </summary>
    public void LoadFromJson(string json)
    {
        var config = JsonSerializer.Deserialize<ServerMapConfig>(json);
        if (config != null)
        {
            _maps.Clear();
            _maps.Add(config);
            _initialized = true;
        }
    }

    /// <summary>
    /// 从 JSON 文件加载地图
    /// </summary>
    public bool LoadFromFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<ServerMapConfig>(json);
            if (config != null)
            {
                _maps.Add(config);
                _initialized = true;
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMapLoader] Failed to load map from {filePath}: {ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// 获取默认地图
    /// </summary>
    public ServerMapConfig GetDefaultMap()
    {
        return _maps.Count > 0 ? _maps[0] : _defaultMap;
    }

    /// <summary>
    /// 随机选择一个地图
    /// </summary>
    public ServerMapConfig GetRandomMap()
    {
        if (_maps.Count == 0)
            return _defaultMap;
        
        var random = new Random();
        return _maps[random.Next(_maps.Count)];
    }
}
