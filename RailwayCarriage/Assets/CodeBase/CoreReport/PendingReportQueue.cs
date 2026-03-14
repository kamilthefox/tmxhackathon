using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Персистентная очередь отложенных репортов.
/// Сохраняется в Application.persistentDataPath/pending_reports.json.
/// Потокобезопасность не нужна — всё вызывается из main-thread Unity.
/// </summary>
public static class PendingReportQueue
{
    private const string FileName = "pending_reports.json";
    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // ── Внутренний контейнер для JsonUtility ─────────────────────────────────

    [Serializable]
    private class QueueData
    {
        public List<PendingReport> items = new List<PendingReport>();
    }

    // ── Публичный API ─────────────────────────────────────────────────────────

    /// <summary>Добавляет репорт в очередь и сразу сохраняет на диск.</summary>
    public static void Enqueue(PendingReport pending)
    {
        var data = Load();
        data.items.Add(pending);
        Save(data);
        Debug.Log($"[PendingReportQueue] Сохранён отложенный репорт {pending.guid}. " +
                  $"Итого в очереди: {data.items.Count}");
    }

    /// <summary>Возвращает все накопившиеся репорты (копия списка).</summary>
    public static List<PendingReport> GetAll()
    {
        return Load().items;
    }

    /// <summary>Удаляет репорт по guid и сохраняет изменения на диск.</summary>
    public static void Remove(string guid)
    {
        var data = Load();
        int removed = data.items.RemoveAll(r => r.guid == guid);
        if (removed > 0)
        {
            Save(data);
            Debug.Log($"[PendingReportQueue] Репорт {guid} удалён. Осталось: {data.items.Count}");
        }
    }

    /// <summary>Количество отложенных репортов.</summary>
    public static int Count => Load().items.Count;

    // ── Внутренние методы ─────────────────────────────────────────────────────

    private static QueueData Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<QueueData>(json);
                return data ?? new QueueData();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PendingReportQueue] Ошибка загрузки: {ex.Message}");
        }

        return new QueueData();
    }

    private static void Save(QueueData data)
    {
        try
        {
            string dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: false));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PendingReportQueue] Ошибка сохранения: {ex.Message}");
        }
    }
}