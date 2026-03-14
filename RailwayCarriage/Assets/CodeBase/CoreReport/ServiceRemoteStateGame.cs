using System;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ServiceRemoteStateGame
{
    private static readonly string SaveFileName = "game_state.dat";
    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    /// <summary>
    /// Проверяет, прошло ли 24 часа с последнего сохранения даты
    /// </summary>
    /// <returns> true - если прошло 24 часа, false - если нет или файл создан впервые</returns>
    public static bool CheckIf24HoursPassed()
    {
        try
        {
            // Проверяем существование файла
            if (!File.Exists(SavePath))
            {
                // Файла нет - создаем с текущей датой и возвращаем false
                SaveCurrentDate();
                Debug.Log($"[ServiceRemoteStateGame] Файл создан впервые: {SavePath}");
                return false;
            }

            // Читаем сохраненную дату из файла
            string dateString = File.ReadAllText(SavePath);

            if (DateTime.TryParse(dateString, out DateTime savedDate))
            {
                // Проверяем, прошло ли 24 часа
                TimeSpan timeDifference = DateTime.Now - savedDate;
                bool has24HoursPassed = timeDifference.TotalHours >= 24;

                if (has24HoursPassed)
                {
                    Debug.Log($"[ServiceRemoteStateGame] Прошло 24 часа с последнего сохранения");
                }
                else
                {
                    double hoursLeft = 24 - timeDifference.TotalHours;
                    Debug.Log($"[ServiceRemoteStateGame] До следующей проверки осталось: {hoursLeft:F1} часов");
                }

                return has24HoursPassed;
            }
            else
            {
                Debug.LogWarning($"[ServiceRemoteStateGame] Не удалось распарсить дату, создаем новую");
                SaveCurrentDate();
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServiceRemoteStateGame] Ошибка при проверке даты: {ex.Message}");

            // В случае ошибки создаем файл заново
            try
            {
                SaveCurrentDate();
            }
            catch { }

            return false;
        }
    }

    /// <summary>
    /// Сохраняет текущую дату в файл
    /// </summary>
    public static void SaveCurrentDate()
    {
        try
        {
            // Убеждаемся, что директория существует
            string directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Сохраняем текущую дату
            File.WriteAllText(SavePath, DateTime.Now.ToString("o")); // ISO 8601 формат
            Debug.Log($"[ServiceRemoteStateGame] Дата сохранена: {DateTime.Now}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServiceRemoteStateGame] Ошибка при сохранении даты: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Получить путь к файлу сохранения (для отладки)
    /// </summary>
    public static string GetSavePath()
    {
        return SavePath;
    }

    /// <summary>
    /// Принудительно сбросить сохраненную дату (для тестирования)
    /// </summary>
    public static void ResetSavedDate()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                Debug.Log($"[ServiceRemoteStateGame] Файл удален: {SavePath}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServiceRemoteStateGame] Ошибка при сбросе даты: {ex.Message}");
        }
    }
}