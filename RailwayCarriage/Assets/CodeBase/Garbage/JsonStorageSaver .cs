using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class JsonStorageSaver : IStorageSaver
{
    public void Save(StorageGarbage.StorageData storage)
    {
        string path = Path.Combine(Application.persistentDataPath, "garbage_data.json");
        string json = JsonUtility.ToJson(storage, true);
        File.WriteAllText(path, json);
    }

    public void Load(StorageGarbage.StorageData storage)
    {
        string path = Path.Combine(Application.persistentDataPath, "garbage_data.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            JsonUtility.FromJsonOverwrite(json, storage);
        }
    }
}
