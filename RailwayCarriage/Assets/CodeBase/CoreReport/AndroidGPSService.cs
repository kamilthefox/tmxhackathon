using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Android;
using UnityEngine;

public class AndroidGPSService : IGPSService
{
    private static AndroidGPSService _instance;
    public static AndroidGPSService Instance
    {
        get
        {
            if (_instance == null)
                _instance = new AndroidGPSService();
            return _instance;
        }
    }

    private LocationData _lastLocation;
    private bool _isTracking;
    private Action<LocationData> _singleRequestCallback;


    /// <summary>
    /// Запрашивает одноразовое получение локации
    /// </summary>
    public void RequestLocation(Action<LocationData> callback)
    {
        _singleRequestCallback = callback;
        TimeStateGarbage.Instance._coroutineRunner.StartCoroutine(GetLocationCoroutine(false));
    }

    /// <summary>
    /// Запускает постоянное отслеживание позиции
    /// </summary>
    public void StartTracking(float updateDistance = 10f, float desiredAccuracy = 10f)
    {
        if (_isTracking) return;

        _isTracking = true;
        TimeStateGarbage.Instance._coroutineRunner.StartCoroutine(GetLocationCoroutine(true, desiredAccuracy, updateDistance));
    }

    /// <summary>
    /// Останавливает отслеживание
    /// </summary>
    public void StopTracking()
    {
        _isTracking = false;
        Input.location.Stop();
    }

    /// <summary>
    /// Возвращает последнюю известную локацию
    /// </summary>
    public LocationData GetLastLocation()
    {
        return _lastLocation;
    }

    /// <summary>
    /// Проверяет, включена ли геолокация на устройстве
    /// </summary>
    public bool IsEnabled()
    {
        return Input.location.isEnabledByUser;
    }

    private IEnumerator GetLocationCoroutine(bool continuous, float desiredAccuracy = 10f, float updateDistance = 10f)
    {
        // Проверяем и запрашиваем разрешения на Android
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);

            // Ждем ответа пользователя
            float timeout = 5f;
            while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && timeout > 0)
            {
                yield return new WaitForSeconds(0.5f);
                timeout -= 0.5f;
            }

            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                var errorData = new LocationData
                {
                    isSuccess = false,
                    errorMessage = "Permission denied by user"
                };

                if (!continuous && _singleRequestCallback != null)
                    _singleRequestCallback(errorData);

                yield break;
            }
        }
#endif

        if (!Input.location.isEnabledByUser)
        {
            var errorData = new LocationData
            {
                isSuccess = false,
                errorMessage = "Location service is disabled on device"
            };

            if (!continuous && _singleRequestCallback != null)
                _singleRequestCallback(errorData);

            yield break;
        }

        Input.location.Start(desiredAccuracy, updateDistance);

        // Ждем инициализации (макс 20 секунд)
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (maxWait < 1)
        {
            var errorData = new LocationData
            {
                isSuccess = false,
                errorMessage = "Location service initialization timeout"
            };

            if (!continuous && _singleRequestCallback != null)
                _singleRequestCallback(errorData);

            Input.location.Stop();
            yield break;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            var errorData = new LocationData
            {
                isSuccess = false,
                errorMessage = "Unable to determine device location"
            };

            if (!continuous && _singleRequestCallback != null)
                _singleRequestCallback(errorData);

            Input.location.Stop();
            yield break;
        }

        UpdateLastLocation();

        if (!continuous && _singleRequestCallback != null)
        {
            _singleRequestCallback(_lastLocation);
            Input.location.Stop();
            yield break;
        }

        while (continuous && _isTracking)
        {
            UpdateLastLocation();
            yield return new WaitForSeconds(1f);
        }
    }

    private void UpdateLastLocation()
    {
        var data = Input.location.lastData;

        _lastLocation = new LocationData
        {
            latitude = data.latitude,
            longitude = data.longitude,
            altitude = data.altitude,
            horizontalAccuracy = data.horizontalAccuracy,
            timestamp = data.timestamp,
            isSuccess = true,
            errorMessage = null
        };
    }

}
