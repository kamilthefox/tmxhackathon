using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PhotoCamera : MonoBehaviour
{
    [Header("UI элементы")]
    [SerializeField] private RawImage cameraPreview;
    [SerializeField] private AspectRatioFitter aspectFitter;

    [Header("Настройки")]
    [SerializeField] private int maxPhotoSize = 1024;

    [Header("Отладка поворота камеры (только для настройки)")]
    [Tooltip("Включи чтобы подобрать правильный поворот на устройстве")]
    [SerializeField] private bool debugRotation = false;
    [SerializeField][Range(0, 270)] private int debugAngle = 90;
    [SerializeField] private bool debugMirrorX = false;
    [SerializeField] private bool debugMirrorY = false;

    private WebCamTexture _webCamTexture;
    private WebCamDevice[] _cameraDevices;
    private bool _isCameraReady;
    private int _currentCameraIndex;

    public UnityEvent OnPhotoCaptured;
    public UnityEvent<string> OnUploadSimulated;

    private void Start()
    {
        // InitializeCamera();
    }


    public void SwitchCamera()
    {
        if (_cameraDevices.Length <= 1) return;

        // Переключаем на следующую камеру
        _currentCameraIndex = (_currentCameraIndex + 1) % _cameraDevices.Length;
        StartCamera();
    }

    public void InitializeCameraAfterPermission()
    {
        StartCoroutine(InitializeCameraDevices());
    }

    private IEnumerator InitializeCameraDevices()
    {
        yield return new WaitForEndOfFrame();

        _cameraDevices = WebCamTexture.devices;

        if (_cameraDevices.Length == 0)
        {
            Debug.LogError("[AndroidCameraService] Камеры не найдены");
            yield break;
        }

        StartCamera();
    }

    private void StartCamera()
    {
        if (_webCamTexture != null && _webCamTexture.isPlaying)
        {
            _webCamTexture.Stop();
        }

        string cameraName = _cameraDevices[_currentCameraIndex].name;
        _webCamTexture = new WebCamTexture(cameraName, 720, 1280, 30);

        if (cameraPreview != null)
        {
            cameraPreview.texture = _webCamTexture;
        }

        _webCamTexture.Play();
        _isCameraReady = true;

        StartCoroutine(FixCameraRotation());
    }

    private IEnumerator FixCameraRotation()
    {
        // Ждём пока WebCamTexture реально запустится и отдаст корректные размеры
        yield return new WaitUntil(() => _webCamTexture != null && _webCamTexture.width > 16);

        if (aspectFitter == null)
        {
            aspectFitter = cameraPreview.gameObject.GetComponent<AspectRatioFitter>();
            if (aspectFitter == null)
                aspectFitter = cameraPreview.gameObject.AddComponent<AspectRatioFitter>();
        }

        ApplyCameraRotation();
    }

    private void ApplyCameraRotation()
    {
        // В режиме отладки — берём значения из Inspector вручную
        int angle = debugRotation ? debugAngle : _webCamTexture.videoRotationAngle;
        bool mirrorX = debugRotation ? debugMirrorX : false;
        bool mirrorY = debugRotation ? debugMirrorY : _webCamTexture.videoVerticallyMirrored;
        bool isPortrait = angle == 90 || angle == 270;

        // Поворачиваем сам RawImage
        cameraPreview.rectTransform.localEulerAngles = new Vector3(0, 0, -angle);

        // Получаем размер родительского контейнера
        RectTransform parent = cameraPreview.rectTransform.parent as RectTransform;
        float parentW = parent != null ? parent.rect.width : Screen.width;
        float parentH = parent != null ? parent.rect.height : Screen.height;

        // После поворота на 90/270 нужно явно задать размер RawImage —
        // иначе AspectRatioFitter работает с неповёрнутыми габаритами и растягивает
        if (isPortrait)
        {
            // В портрете RawImage физически повёрнут на 90°, поэтому его
            // width должен равняться высоте родителя и наоборот
            cameraPreview.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, parentH);
            cameraPreview.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, parentW);
        }
        else
        {
            cameraPreview.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, parentW);
            cameraPreview.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, parentH);
        }

        // Aspect ratio считаем от итоговых видимых сторон
        float texW = isPortrait ? _webCamTexture.height : _webCamTexture.width;
        float texH = isPortrait ? _webCamTexture.width : _webCamTexture.height;

        // Отключаем AspectRatioFitter — размер задаём вручную выше
        aspectFitter.enabled = false;

        // Зеркалирование через scale
        float scaleX = mirrorX ? -1f : 1f;
        float scaleY = mirrorY ? -1f : 1f;
        cameraPreview.rectTransform.localScale = new Vector3(scaleX, scaleY, 1f);

        Debug.Log($"[PhotoCamera] deviceAngle={_webCamTexture.videoRotationAngle}, " +
                  $"appliedAngle={angle}, mirrorX={mirrorX}, mirrorY={mirrorY}, " +
                  $"tex={_webCamTexture.width}x{_webCamTexture.height}, " +
                  $"aspect={texW / texH:F2}");
    }

    public void CapturePhoto()
    {
        if (!_isCameraReady || _webCamTexture == null || !_webCamTexture.isPlaying)
        {
            Debug.LogError("[AndroidCameraService] Камера не готова");
            return;
        }

        TimeStateGarbage.Instance.StartCoroutine(CapturePhotoCoroutine());
    }

    private IEnumerator CapturePhotoCoroutine()
    {
        yield return new WaitForEndOfFrame();

        try
        {
            Texture2D photo = new Texture2D(_webCamTexture.width, _webCamTexture.height);
            photo.SetPixels(_webCamTexture.GetPixels());
            photo.Apply();

            // Сжимаем если нужно
            byte[] bytes;
            if (photo.width > maxPhotoSize || photo.height > maxPhotoSize)
            {
                Texture2D resizedPhoto = ResizeTexture(photo, maxPhotoSize);
                bytes = resizedPhoto.EncodeToJPG(85);

                // Сохраняем в ОЗУ
                PhotoStorage.StorePhoto(bytes, resizedPhoto);

                // Оригинал больше не нужен
                Destroy(photo);
            }
            else
            {
                bytes = photo.EncodeToJPG(85);
                PhotoStorage.StorePhoto(bytes, photo);
            }

            OnPhotoCaptured?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AndroidCameraService] Ошибка при съемке: {ex.Message}");
        }
    }

    private Texture2D ResizeTexture(Texture2D source, int maxSize)
    {
        float ratio = (float)source.width / source.height;
        int newWidth = source.width > source.height ? maxSize : (int)(maxSize * ratio);
        int newHeight = source.height > source.width ? maxSize : (int)(maxSize / ratio);

        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        RenderTexture.active = rt;

        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(newWidth, newHeight);
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();

        RenderTexture.ReleaseTemporary(rt);
        return result;
    }


    public void StopCamera()
    {
        if (_webCamTexture != null && _webCamTexture.isPlaying)
        {
            _webCamTexture.Stop();
            _isCameraReady = false;
        }
    }

    private void OnDestroy()
    {
        StopCamera();

        if (_webCamTexture != null)
        {
            Destroy(_webCamTexture);
            _webCamTexture = null;
        }

        PhotoStorage.Clear();
    }
}