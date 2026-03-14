using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System.Collections;

public class ControlMenu : MonoBehaviour
{
    /// <summary>
    /// Добавлять в том же порядке, что и ENUM 
    /// View Пустой, у него менюшек нет!
    /// </summary>
    [SerializeField] private GameObject[] MenuList;

    [SerializeField] private Image preview;

    [SerializeField] private TMP_Text countCoin;

    [SerializeField] private TMP_Dropdown lineMetroDrop;
    [SerializeField] private TMP_Dropdown categoryDrop;
    [SerializeField] private TMP_InputField reportTextField;

    [Header("Валидация полей")]
    [SerializeField] private Color errorColor = new Color(1f, 0.3f, 0.3f, 1f); // Красный
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeAmount = 10f;

    [SerializeField] private TMP_InputField reportNumberCarriageField;
    [SerializeField] private TMP_InputField reportNumberTrainField;

    [SerializeField] private TMP_FontAsset emojiFont;

    [Header("Отправка репорта")]
    [SerializeField] private Button submitButton;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private int coinsPerReport = 500;

    [Header("Мусор")]
    [Tooltip("Какой процент мусора убирать при успешном репорте (0–100)")]
    [SerializeField][Range(0, 100)] private int garbageRemovePercent = 20;

    [Header("Колбэки отправки репорта")]
    [Tooltip("Вызывается когда репорт начал отправляться")]
    public UnityEvent OnReportSendStarted;

    [Tooltip("Вызывается при успешной отправке. Передаёт количество монет после начисления")]
    public UnityEvent<int> OnReportSendSuccess;

    [Tooltip("Вызывается при ошибке отправки. Передаёт текст ошибки")]
    public UnityEvent<string> OnReportSendFailed;

    [Header("Ссылка на камеру")]
    [SerializeField] private PhotoCamera photoCamera;

    private bool _permissionsGranted = false;

    /// <summary>
    /// Вызывается когда репорт сохранён оффлайн.
    /// Можно использовать для показа тоста «Репорт сохранён, будет отправлен при появлении сети».
    /// </summary>
    public UnityEvent OnReportSavedOffline;

    public enum StateMenu
    {
        View,
        MainMenu,
        Shop,
        MenuPhoto
    }

    [SerializeField] private StateMenu state;

    // Закешированные данные
    private List<LineData> _lines = new List<LineData>();
    private List<CategoryData> _categories = new List<CategoryData>();

    private readonly IGPSService _gps = AndroidGPSService.Instance;

    private void Start()
    {
        foreach (var menu in MenuList)
            menu.SetActive(false);

        MenuList[(int)state].SetActive(true);

        LoadDropdowns();
    }

    // ── Данные юзера для вьюхи ─────────────────────

    public void UpdateCountCoin(int count)
    {
        Debug.Log($"[MetroService] Монеты с сервера для UI: {count}");
        countCoin.text = $"{count}$";
    }

    // ── Загрузка дропдаунов ───────────────────────────────────────────────────

    private async void LoadDropdowns()
    {
        await LoadLines();
        await LoadCategories();
    }

    private async Task LoadLines()
    {
        _lines = await MetroService.GetLinesAsync();
        lineMetroDrop.ClearOptions();

        var options = _lines.Select(line => new TMP_Dropdown.OptionData
        {
            text = $"<color=#{line.hex}>■</color> {line.name}/{line.number}"
        }).ToList();

        lineMetroDrop.AddOptions(options);
        ConfigureFonts(lineMetroDrop);
    }

    private async Task LoadCategories()
    {
        _categories = await MetroService.GetCategoriesAsync();
        categoryDrop.ClearOptions();

        var options = _categories.Select(cat => new TMP_Dropdown.OptionData
        {
            text = cat.name
        }).ToList();

        categoryDrop.AddOptions(options);
    }

    private void ConfigureFonts(TMP_Dropdown dropdown)
    {
        if (dropdown.captionText != null)
        {
            dropdown.captionText.richText = true;
            if (emojiFont != null)
                dropdown.captionText.font = emojiFont;
        }

        var itemTexts = dropdown.GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in itemTexts)
        {
            text.richText = true;
            if (emojiFont != null)
                text.font = emojiFont;
        }
    }

    // ── Кнопки навигации ─────────────────────────────────────────────────────

    public void LoadPrewiewImage()
    {
        preview.sprite = Sprite.Create(
            PhotoStorage.LastPhotoTexture,
            new Rect(0, 0, PhotoStorage.LastPhotoTexture.width, PhotoStorage.LastPhotoTexture.height),
            new Vector2(0.5f, 0.5f)
        );
    }

    public void ClickBackButton()
    {
        if (state == StateMenu.MainMenu)
        {
            ChangeStateMenu(StateMenu.View);
            return;
        }
        ChangeStateMenu(StateMenu.MainMenu);
    }

    public void ClickBShopButton()
    {
        if (state == StateMenu.Shop)
        {
            ChangeStateMenu(StateMenu.View);
            return;
        }
        ChangeStateMenu(StateMenu.Shop);
    }

    public void ClickPhotoButton()
    {
        ChangeStateMenu(StateMenu.MenuPhoto);
        if (!_permissionsGranted)
        {
            StartCoroutine(RequestPermissionsSequentially());
        }
    }

    private void ChangeStateMenu(StateMenu new_state)
    {
        MenuList[(int)state].SetActive(false);
        MenuList[(int)new_state].SetActive(true);
        state = new_state;
    }

    // ── Отправка репорта ──────────────────────────────────────────────────────

    /// <summary>
    /// Привяжи к кнопке Submit в MenuPhoto через Inspector.
    /// async void — точка входа из UI, Task-ошибки логируются внутри.
    /// </summary>
    public async void ClickSubmitButton()
    {
        await SendReportAsync();
    }

    /// <summary>
    /// Последовательно запрашивает разрешения: сначала GPS, потом камеру
    /// </summary>
    private IEnumerator RequestPermissionsSequentially()
    {
        Debug.Log("[ControlMenu] Начинаем последовательный запрос разрешений");

        // ── Шаг 1: GPS разрешение ─────────────────────────────────────────────

#if UNITY_ANDROID
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.FineLocation))
        {
            Debug.Log("[ControlMenu] Запрашиваем GPS разрешение...");
            UnityEngine.Android.Permission.RequestUserPermission(
                UnityEngine.Android.Permission.FineLocation);

            // Ждём ответа пользователя (обычно диалог появляется сразу)
            yield return new WaitForSeconds(0.5f);

            // Ждём пока пользователь не ответит на диалог
            while (UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                       UnityEngine.Android.Permission.FineLocation) == false)
            {
                yield return new WaitForSeconds(0.1f);
            }

            Debug.Log("[ControlMenu] GPS разрешение получено");
            _gps.StartTracking();
        }
        else
        {
            Debug.Log("[ControlMenu] GPS разрешение уже есть");
            _gps.StartTracking();
        }
#endif

        // Небольшая задержка между запросами
        yield return new WaitForSeconds(0.3f);

        // ── Шаг 2: Камера разрешение ──────────────────────────────────────────

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.Log("[ControlMenu] Запрашиваем разрешение на камеру...");
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

            if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.Log("[ControlMenu] Разрешение на камеру получено");
                if (photoCamera != null)
                {
                    photoCamera.InitializeCameraAfterPermission();
                }
            }
            else
            {
                Debug.LogError("[ControlMenu] Разрешение на камеру отклонено");
            }
        }
        else
        {
            Debug.Log("[ControlMenu] Разрешение на камеру уже есть");
            if (photoCamera != null)
            {
                photoCamera.InitializeCameraAfterPermission();
            }
        }

        _permissionsGranted = true;
        Debug.Log("[ControlMenu] Все разрешения получены");
    }

    private async Task SendReportAsync()
    {
        // ── Валидация ─────────────────────────────────────────────────────────

        if (PhotoStorage.LastPhotoBytes == null)
        {
            const string noPhotoMsg = "Нет фото для отправки — сделай снимок перед отправкой";
            Debug.LogWarning($"[ControlMenu] {noPhotoMsg}");
            OnReportSendFailed?.Invoke(noPhotoMsg);
            return;
        }

        var user = TimeStateGarbage.Instance.GetUser();
        if (user == null)
        {
            const string noUserMsg = "Пользователь не найден — перезапусти приложение";
            Debug.LogError($"[ControlMenu] {noUserMsg}");
            OnReportSendFailed?.Invoke(noUserMsg);
            return;
        }

        if (!ValidateRequiredFields())
        {
            return;
        }

        // ── Геолокация ────────────────────────────────────────────────────────

        string geom = string.Empty;
        if (_gps.IsEnabled())
        {
            bool gpsAnswered = false;
            _gps.RequestLocation(loc =>
            {
                if (loc.isSuccess)
                    geom = $"POINT({loc.longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)} {loc.latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
                else
                    Debug.LogWarning($"[ControlMenu] GPS недоступен: {loc.errorMessage}");
                gpsAnswered = true;
            });

            float waited = 0f;
            while (!gpsAnswered && waited < 3f)
            {
                await Task.Delay(100);
                waited += 0.1f;
            }

            if (!gpsAnswered)
                Debug.LogWarning("[ControlMenu] GPS не ответил за 3 секунды — отправляем без геометки");
        }
        else
        {
            Debug.LogWarning("[ControlMenu] GPS отключён на устройстве — geom будет пустым");
        }

        // ── Сборка репорта ────────────────────────────────────────────────────

        string reportText = reportTextField != null ? reportTextField.text : string.Empty;
        if (string.IsNullOrEmpty(reportText))
            reportText = " ";

        var report = new Report
        {
            user_id = user.id,
            id_cat = GetSelectedId(_categories.Select(c => c.id_cat).ToList(), categoryDrop),
            id_line = GetSelectedId(_lines.Select(l => l.id_line).ToList(), lineMetroDrop),
            number_carriage = reportNumberCarriageField != null ? reportNumberCarriageField.text : string.Empty,
            number_train = reportNumberTrainField != null ? reportNumberTrainField.text : string.Empty,
            geom = geom,
            report_text = reportText,
        };

        Debug.Log($"[ControlMenu] ── Отправка репорта ──────────────────────────");
        Debug.Log($"[ControlMenu] user_id         : {report.user_id}");
        Debug.Log($"[ControlMenu] id_cat          : {report.id_cat}  ({GetDropdownLabel(categoryDrop)})");
        Debug.Log($"[ControlMenu] id_line         : {report.id_line} ({GetDropdownLabel(lineMetroDrop)})");
        Debug.Log($"[ControlMenu] geom            : \"{report.geom}\"");
        Debug.Log($"[ControlMenu] report_text     : \"{report.report_text}\"");
        Debug.Log($"[ControlMenu] number_carriage : \"{report.number_carriage}\"");
        Debug.Log($"[ControlMenu] number_train    : \"{report.number_train}\"");
        Debug.Log($"[ControlMenu] photo size      : {PhotoStorage.LastPhotoBytes.Length / 1024} KB");
        Debug.Log($"[ControlMenu] photo time      : {PhotoStorage.LastPhotoTime:HH:mm:ss}");

        // ── Отправка ──────────────────────────────────────────────────────────

        SetLoading(true);
        OnReportSendStarted?.Invoke();

        bool success = await MetroService.PostReportAsync(report, PhotoStorage.LastPhotoBytes);

        SetLoading(false);

        // ── Результат ─────────────────────────────────────────────────────────

        if (success)
        {
            Debug.Log($"[ControlMenu] Сервер вернул 200 OK — репорт принят");
            HandleReport(user, true);
        }
        else
        {
            // Сервер недоступен — сохраняем репорт в оффлайн-очередь
            Debug.LogWarning("[ControlMenu] Сервер недоступен — сохраняем репорт оффлайн");
            SaveReportOffline(report, PhotoStorage.LastPhotoBytes);

            // Монеты и мусор начислим условно сразу, чтобы не ломать геймплей
            HandleReport(user, false);

            // Дополнительно уведомляем UI об оффлайн-сохранении
            OnReportSavedOffline?.Invoke();
        }
    }

    // ── Оффлайн-очередь ──────────────────────────────────────────────────────

    /// <summary>
    /// Сохраняет репорт в персистентную очередь на диск.
    /// </summary>
    private static void SaveReportOffline(Report report, byte[] photoBytes)
    {
        var pending = new PendingReport(report, photoBytes);
        PendingReportQueue.Enqueue(pending);
        Debug.Log($"[ControlMenu] Репорт {pending.guid} сохранён оффлайн. " +
                  $"В очереди: {PendingReportQueue.Count}");
    }

    /// <summary>
    /// Пытается отправить все накопленные оффлайн-репорты.
    /// Вызывается из TimeStateGarbage при старте, когда сеть (вероятно) доступна.
    /// Возвращает количество успешно отправленных репортов.
    /// </summary>
    public static async Task<int> FlushPendingReportsAsync()
    {
        var pending = PendingReportQueue.GetAll();
        if (pending.Count == 0)
        {
            Debug.Log("[ControlMenu] Оффлайн-очередь пуста — нечего отправлять");
            return 0;
        }

        Debug.Log($"[ControlMenu] Начинаем переотправку {pending.Count} оффлайн-репортов...");
        int sent = 0;

        foreach (var item in pending)
        {
            try
            {
                byte[] photoBytes = item.GetPhotoBytes();
                bool ok = await MetroService.PostReportAsync(item.report, photoBytes);

                if (ok)
                {
                    PendingReportQueue.Remove(item.guid);
                    sent++;
                    Debug.Log($"[ControlMenu] Оффлайн-репорт {item.guid} отправлен успешно " +
                              $"(создан {item.createdAt})");
                }
                else
                {
                    // Сервер всё ещё недоступен — прекращаем попытки до следующего запуска
                    Debug.LogWarning($"[ControlMenu] Оффлайн-репорт {item.guid} — сервер снова " +
                                     $"недоступен, останавливаем очередь");
                    break;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ControlMenu] Ошибка при переотправке репорта {item.guid}: {ex.Message}");
                break;
            }
        }

        Debug.Log($"[ControlMenu] Переотправка завершена: отправлено {sent}/{pending.Count}");
        return sent;
    }

    // ── Успешная обработка ────────────────────────────────────────────────────

    private void HandleReport(User user, bool isSuccess)
    {
        // ── Монеты ────────────────────────────────────────────────────────────

        int coinsBefore = user.coin;
        if (isSuccess)
        {
            user.coin += coinsPerReport;
            Debug.Log($"[ControlMenu] Монеты: {coinsBefore} → {user.coin} (+{coinsPerReport})");
        }
        new MockUserSaver().Save(user);

        

        // ── Мусор ─────────────────────────────────────────────────────────────

        var storage = TimeStateGarbage.Instance.GetStorage();
        RemoveGarbageByPercent(storage);

        // ── Чистим фото из ОЗУ ───────────────────────────────────────────────

        Debug.Log($"[ControlMenu] Очищаем фото из памяти");
        PhotoStorage.Clear();

        // ── Уведомляем подписчиков ────────────────────────────────────────────

        Debug.Log($"[ControlMenu] Вызываем OnReportSendSuccess (монеты: {user.coin})");
        OnReportSendSuccess?.Invoke(user.coin);

        // ── Переход в меню ────────────────────────────────────────────────────

        Debug.Log($"[ControlMenu] Переход в MainMenu");
        ChangeStateMenu(StateMenu.MainMenu);
    }

    private void RemoveGarbageByPercent(StorageGarbage storage)
    {
        int total = storage.garbage.garbageList.Count;
        if (total == 0)
        {
            Debug.Log("[ControlMenu] Вагон уже чист!");
            return;
        }

        int countToRemove = Mathf.Max(1, Mathf.RoundToInt(total * garbageRemovePercent / 100f));
        countToRemove = Mathf.Min(countToRemove, total);

        Debug.Log($"[ControlMenu] Убираем {countToRemove} из {total} объектов мусора ({garbageRemovePercent}%)");

        for (int i = 0; i < countToRemove; i++)
        {
            if (storage.garbage.garbageList.Count == 0) break;

            int rndIndex = UnityEngine.Random.Range(0, storage.garbage.garbageList.Count);
            int positionId = storage.garbage.garbageList[rndIndex].positionAtPoint;

            storage.RemoveGarbage(positionId);
        }

        new JsonStorageSaver().Save(storage.garbage);
        Debug.Log($"[ControlMenu] Осталось мусора: {storage.garbage.garbageList.Count}");
    }

    // ── Утилиты ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Проверяет обязательные поля. Возвращает true если всё ОК.
    /// </summary>
    private bool ValidateRequiredFields()
    {
        bool isValid = true;
        List<TMP_InputField> emptyFields = new List<TMP_InputField>();

        // Проверяем номер вагона
        if (reportNumberCarriageField != null &&
            string.IsNullOrWhiteSpace(reportNumberCarriageField.text))
        {
            emptyFields.Add(reportNumberCarriageField);
            isValid = false;
        }

        // Проверяем номер поезда
        if (reportNumberTrainField != null &&
            string.IsNullOrWhiteSpace(reportNumberTrainField.text))
        {
            emptyFields.Add(reportNumberTrainField);
            isValid = false;
        }

        // Если есть незаполненные поля — анимируем их
        if (!isValid)
        {
            foreach (var field in emptyFields)
            {
                StartCoroutine(ShakeAndHighlightField(field));
            }

            const string validationMsg = "Заполни номер вагона и номер поезда!";
            Debug.LogWarning($"[ControlMenu] {validationMsg}");
            OnReportSendFailed?.Invoke(validationMsg);
        }

        return isValid;
    }

    /// <summary>
    /// Трясёт поле и подсвечивает его красным
    /// </summary>
    private IEnumerator ShakeAndHighlightField(TMP_InputField field)
    {
        if (field == null) yield break;

        RectTransform rectTransform = field.GetComponent<RectTransform>();
        Vector3 originalPosition = rectTransform.localPosition;

        // Сохраняем оригинальный цвет
        Image fieldImage = field.GetComponent<Image>();
        Color originalColor = Color.white;

        if (fieldImage != null)
        {
            originalColor = fieldImage.color;
            fieldImage.color = errorColor;
        }

        // Placeholder тоже подсвечиваем
        TMP_Text placeholder = field.placeholder as TMP_Text;
        Color originalPlaceholderColor = Color.gray;
        if (placeholder != null)
        {
            originalPlaceholderColor = placeholder.color;
            placeholder.color = errorColor;
        }

        // ── Анимация тряски ───────────────────────────────────────────────────

        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / shakeDuration;

            // Затухающая синусоида для тряски
            float shake = Mathf.Sin(progress * Mathf.PI * 8) * (1 - progress) * shakeAmount;

            rectTransform.localPosition = originalPosition + new Vector3(shake, 0, 0);

            yield return null;
        }

        // Возвращаем на место
        rectTransform.localPosition = originalPosition;

        // ── Плавное возвращение цвета ─────────────────────────────────────────

        float colorFadeDuration = 0.3f;
        float colorElapsed = 0f;

        while (colorElapsed < colorFadeDuration)
        {
            colorElapsed += Time.deltaTime;
            float t = colorElapsed / colorFadeDuration;

            if (fieldImage != null)
            {
                fieldImage.color = Color.Lerp(errorColor, originalColor, t);
            }

            if (placeholder != null)
            {
                placeholder.color = Color.Lerp(errorColor, originalPlaceholderColor, t);
            }

            yield return null;
        }

        // Финальная установка цвета
        if (fieldImage != null)
            fieldImage.color = originalColor;

        if (placeholder != null)
            placeholder.color = originalPlaceholderColor;
    }

    private int GetSelectedId(List<int> ids, TMP_Dropdown dropdown)
    {
        if (ids == null || ids.Count == 0 || dropdown == null) return 0;
        int idx = Mathf.Clamp(dropdown.value, 0, ids.Count - 1);
        return ids[idx];
    }

    private string GetDropdownLabel(TMP_Dropdown dropdown)
    {
        if (dropdown == null || dropdown.options.Count == 0) return "—";
        return dropdown.options[Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1)].text;
    }

    private void SetLoading(bool isLoading)
    {
        if (submitButton != null) submitButton.interactable = !isLoading;
        if (loadingIndicator != null) loadingIndicator.SetActive(isLoading);
    }
}