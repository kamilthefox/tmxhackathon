using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class TimeStateGarbage : MonoBehaviour
{
    private static TimeStateGarbage _instance;
    public static TimeStateGarbage Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TimeStateGarbage>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("TimeStateGarbage");
                    _instance = go.AddComponent<TimeStateGarbage>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [Header("Хранилища")]
    [SerializeField] private StorageGarbage _storageGarbage;
    [SerializeField] private StorageGraffiti _storageGraffiti;

    public UnityEvent<int> OnCompletedLoadInfo = new UnityEvent<int>();

    private IStorageSaver _storageSaver;
    private IGraffitiSaver _graffitiSaver;

    private User _currentUser;
    private IUserSaver _userSaver;

    public MonoBehaviour _coroutineRunner;

    private const string FirstLaunchKey = "FirstLaunchCompleted";

    private void Start()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        StartCoroutine(InitializeCoroutine());
    }

    private IEnumerator InitializeCoroutine()
    {
        // ── 1. Синхронная часть — локальные данные ────────────────────────────

        _storageSaver = new JsonStorageSaver();
        _graffitiSaver = new JsonGraffitiSaver();
        _userSaver = new MockUserSaver();
        OnInitGPS();

        // Загружаем мусор
        _storageSaver.Load(_storageGarbage.garbage);
        foreach (Garbage garbage in _storageGarbage.garbage)
            _storageGarbage.GetGarbageToID(garbage.id, garbage.positionAtPoint);

        // Загружаем граффити
        _graffitiSaver.Load(_storageGraffiti.graffiti);
        foreach (Graffiti g in _storageGraffiti.graffiti)
            _storageGraffiti.GetGraffitiToID(g.id, g.positionAtPoint);

        Debug.Log("[TimeStateGarbage] Локальные данные загружены");

        // ── 2. Загружаем / создаём пользователя ──────────────────────────────

        var userTask = LoadOrCreateUserAsync();
        yield return new WaitUntil(() => userTask.IsCompleted);

        if (userTask.IsFaulted)
            Debug.LogError($"[TimeStateGarbage] Ошибка загрузки пользователя: {userTask.Exception}");

        // ── 3. Переотправка накопленных оффлайн-репортов ──────────────────────
        //      Делаем ДО CheckFirstLaunch / CheckTimeAndGenerate,
        //      чтобы данные на сервере были актуальны как можно раньше.

        int pendingCount = PendingReportQueue.Count;
        if (pendingCount > 0)
        {
            Debug.Log($"[TimeStateGarbage] Найдено {pendingCount} оффлайн-репортов, пробуем отправить...");

            var flushTask = ControlMenu.FlushPendingReportsAsync();
            yield return new WaitUntil(() => flushTask.IsCompleted);

            if (flushTask.IsFaulted)
                Debug.LogError($"[TimeStateGarbage] Ошибка при переотправке: {flushTask.Exception}");
            else
                Debug.Log($"[TimeStateGarbage] Переотправлено репортов: {flushTask.Result}");
        }
        else
        {
            Debug.Log("[TimeStateGarbage] Оффлайн-очередь пуста");
        }

        // ── 4. Первый запуск и генерация ─────────────────────────────────────

        CheckFirstLaunch();
        CheckTimeAndGenerate();

        // ── 5. Сохраняем состояние ────────────────────────────────────────────

        _storageSaver.Save(_storageGarbage.garbage);
        _graffitiSaver.Save(_storageGraffiti.graffiti);

        Debug.Log("[TimeStateGarbage] Инициализация завершена");
        OnCompletedLoadInfo.Invoke(_currentUser.coin);
    }

    private void OnInitGPS()
    {
        GameObject runnerGO = new GameObject("[GPS Service Runner]");
        _coroutineRunner = runnerGO.AddComponent<GPSRunner>();
        UnityEngine.Object.DontDestroyOnLoad(runnerGO);
    }

    private class GPSRunner : MonoBehaviour { }

    private async System.Threading.Tasks.Task LoadOrCreateUserAsync()
    {
        if (_userSaver.HasSavedData())
        {
            _currentUser = _userSaver.Load();
            Debug.Log($"[TimeStateGarbage] Загружен пользователь ID: {_currentUser.id}");

            int serverCoins = await MetroService.GetCoinAsync(_currentUser.id);
            if (serverCoins >= 0)
            {
                Debug.Log($"[TimeStateGarbage] Монеты синхронизированы: локально {_currentUser.coin} → сервер {serverCoins}");
                _currentUser.coin = serverCoins;
                _userSaver.Save(_currentUser);
            }
        }
        else
        {
            Debug.Log("[TimeStateGarbage] Новый пользователь — создаём на сервере...");

            int userId = await MetroService.CreateUserAsync();

            if (userId < 0)
            {
                Debug.LogWarning("[TimeStateGarbage] Сервер недоступен, используем временный локальный ID");
                userId = UnityEngine.Random.Range(10000, 99999);
            }

            _currentUser = new User { id = userId, coin = 0 };
            _userSaver.Save(_currentUser);

            Debug.Log($"[TimeStateGarbage] Создан пользователь ID: {userId}");
        }
    }

    private void CheckTimeAndGenerate()
    {
        bool has24HoursPassed = ServiceRemoteStateGame.CheckIf24HoursPassed();
        if (has24HoursPassed)
        {
            GenerateRandomGarbage();
        }
    }

    private void GenerateRandomGarbage()
    {
        int count = UnityEngine.Random.Range(2, 7);
        Debug.Log($"[TimeStateGarbage] Прошло 24 часа. Генерируем {count} мусора");
        for (int i = 0; i < count; i++)
            _storageGarbage.GetGarbageRandom();
    }

    private void CheckFirstLaunch()
    {
        if (!PlayerPrefs.HasKey(FirstLaunchKey))
        {
            Debug.Log("[TimeStateGarbage] Первый запуск — заполняем вагон по максимуму");

            PlayerPrefs.SetInt(FirstLaunchKey, 1);
            PlayerPrefs.Save();

            for (int i = 0; i < _storageGarbage.GetLengthAllPoints; i++)
                _storageGarbage.GetGarbageRandom();

            for (int i = 0; i < _storageGraffiti.GetLengthAllPoints; i++)
                _storageGraffiti.GetGraffitiRandom();
        }
    }

    // ── Публичные методы ──────────────────────────────────────────────────────

    public StorageGarbage GetStorage() => _storageGarbage;
    public StorageGraffiti GetGraffitiStorage() => _storageGraffiti;
    public User GetUser() => _currentUser;
    public int GetCoinStorage() => _currentUser?.coin ?? 0;

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}