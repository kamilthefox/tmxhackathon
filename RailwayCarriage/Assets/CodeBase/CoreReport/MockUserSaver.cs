using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class MockUserSaver : IUserSaver
{
    private string path;

    public MockUserSaver()
    {
        path = Path.Combine(Application.persistentDataPath, "user_data.json");
    }

    public void Save(User user)
    {
        string json = JsonUtility.ToJson(user, true);
        File.WriteAllText(path, json);
    }

    public User Load()
    {
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<User>(json);
        }
        return null;
    }

    public bool HasSavedData()
    {
        return File.Exists(path);
    }
}
