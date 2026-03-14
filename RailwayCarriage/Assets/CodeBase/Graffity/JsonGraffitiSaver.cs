using System.IO;
using UnityEngine;

public class JsonGraffitiSaver : IGraffitiSaver
{
    private readonly string _path;

    public JsonGraffitiSaver()
    {
        _path = Path.Combine(Application.persistentDataPath, "graffiti_data.json");
    }

    public void Save(StorageGraffiti.StorageData storage)
    {
        string json = JsonUtility.ToJson(storage, true);
        File.WriteAllText(_path, json);
        Debug.Log($"[JsonGraffitiSaver] Сохранено {storage.graffitiList.Count} граффити");
    }

    public void Load(StorageGraffiti.StorageData storage)
    {
        if (!File.Exists(_path))
        {
            Debug.Log("[JsonGraffitiSaver] Файл сохранения не найден — начинаем с чистого листа");
            return;
        }

        string json = File.ReadAllText(_path);
        JsonUtility.FromJsonOverwrite(json, storage);
        Debug.Log($"[JsonGraffitiSaver] Загружено {storage.graffitiList.Count} граффити");
    }
}