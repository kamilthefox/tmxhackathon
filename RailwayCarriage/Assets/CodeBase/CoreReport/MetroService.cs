using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public static class MetroService
{
    private static IAPIClient _apiClient;
    private static string _baseUrl = "http://IP:Port";

    public static void Initialize(IAPIClient apiClient)
    {
        _apiClient = apiClient;
    }

    private static IAPIClient GetClient()
    {
        if (_apiClient == null)
            _apiClient = new APIClient(_baseUrl);
        return _apiClient;
    }

    // ── Справочники ───────────────────────────────────────────────────────────

    public static async Task<List<LineData>> GetLinesAsync()
    {
        try
        {
            var response = await GetClient().GetAsync<LinesResponse>("get_lines_from_db");
            if (response != null && response.success)
            {
                Debug.Log($"[MetroService] Загружено линий: {response.count}");
                return response.lines ?? new List<LineData>();
            }
            Debug.LogError("[MetroService] GetLines: success = false");
            return new List<LineData>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MetroService] GetLines: {ex.Message}");
            return new List<LineData>();
        }
    }

    public static async Task<List<CategoryData>> GetCategoriesAsync()
    {
        try
        {
            var response = await GetClient().GetAsync<CategoriesResponse>("get_categories");
            if (response != null && response.success)
            {
                Debug.Log($"[MetroService] Загружено категорий: {response.count}");
                return response.categories ?? new List<CategoryData>();
            }
            Debug.LogError("[MetroService] GetCategories: success = false");
            return new List<CategoryData>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MetroService] GetCategories: {ex.Message}");
            return new List<CategoryData>();
        }
    }

    // ── Пользователь ──────────────────────────────────────────────────────────

    /// <summary>
    /// POST /create_user
    /// Возвращает ID созданного пользователя или -1 при ошибке.
    /// </summary>
    public static async Task<int> CreateUserAsync()
    {
        try
        {
            var response = await GetClient().PostAsync<object, CreateUserResponse>("create_user", null);

            if (response != null && response.success)
            {
                Debug.Log($"[MetroService] Пользователь создан, ID: {response.user_id}");
                return response.user_id;
            }

            Debug.LogError($"[MetroService] CreateUser: success = false, message: {response?.message}");
            return -1;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MetroService] CreateUser: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// GET /get_coin?id_user={userId}
    /// Возвращает актуальное количество монет с сервера или -1 при ошибке.
    /// </summary>
    public static async Task<int> GetCoinAsync(int userId)
    {
        try
        {
            var response = await GetClient().GetAsync<GetCoinResponse>($"get_coin?id_user={userId}");

            if (response != null && response.success)
            {
                Debug.Log($"[MetroService] Монеты с сервера для user {userId}: {response.coin}");
                return response.coin;
            }

            Debug.LogError($"[MetroService] GetCoin: success = false для user {userId}");
            return -1;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MetroService] GetCoin: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// POST /buying?user_id={userId}&sell_id={sellId}
    /// sell_id: 1 = -500 монет (цвет), 2 = -1000 монет (граффити)
    /// Сервер сам проверяет баланс. 200 OK = успех, иначе — не хватило монет.
    /// </summary>
    public static async Task<bool> BuyAsync(int userId, int sellId)
    {
        try
        {
            await GetClient().PostAsync<object>("buying?user_id=" + userId + "&sell_id=" + sellId, null);
            Debug.Log($"[MetroService] BuyAsync: user={userId}, sell_id={sellId} — успешно");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MetroService] BuyAsync: {ex.Message}");
            return false;
        }
    }
    // ── Отправка репорта ──────────────────────────────────────────────────────

    /// <summary>
    /// POST /post_report — сервер возвращает 200 OK без тела.
    /// </summary>
    public static async Task<bool> PostReportAsync(Report report, byte[] photoBytes)
    {
        try
        {
            string url = $"{_baseUrl}/post_report" +
                         $"?user_id={report.user_id}" +
                         $"&number_train={report.number_train}" +
                         $"&number_carriage={report.number_carriage}" +
                         $"&id_cat={report.id_cat}" +
                         $"&id_line={report.id_line}";

            if (!string.IsNullOrEmpty(report.geom))
                url += $"&geom={UnityWebRequest.EscapeURL(report.geom)}";

            if (!string.IsNullOrEmpty(report.report_text))
                url += $"&report_text={UnityWebRequest.EscapeURL(report.report_text)}";

            var form = new WWWForm();
            form.AddBinaryData("file", photoBytes, "photo.jpg", "image/jpeg");

            using var request = UnityWebRequest.Post(url, form);
            request.timeout = 30;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[MetroService] Репорт успешно отправлен");
                return true;
            }

            Debug.LogError($"[MetroService] PostReport ошибка: {request.error}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MetroService] PostReport исключение: {ex.Message}");
            return false;
        }
    }
}