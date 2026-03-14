using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewControll : MonoBehaviour
{
    [Header("Настройки камеры")]
    [SerializeField] private float sensitivity = 0.5f; // Чувствительность
    [SerializeField] private float minX = -35f; // Минимальный угол по X
    [SerializeField] private float maxX = 35f;  // Максимальный угол по X
    [SerializeField] private float minY = -90f; // Минимальный угол по Y
    [SerializeField] private float maxY = 90f;  // Максимальный угол по Y

    private Vector2 touchStartPos;
    private Vector2 rotation;
    private bool isTouching = false;

    private Transform cam;

    void Start()
    {
        cam = Camera.main.transform;
        rotation = new Vector2(
            cam.eulerAngles.x > 180 ? cam.eulerAngles.x - 360 : cam.eulerAngles.x,
            cam.eulerAngles.y > 180 ? cam.eulerAngles.y - 360 : cam.eulerAngles.y
        );

        rotation.x = Mathf.Clamp(rotation.x, minX, maxX);
        rotation.y = Mathf.Clamp(rotation.y, minY, maxY);

        cam.rotation = Quaternion.Euler(rotation.x, rotation.y, 0);
    }

    void Update()
    {
        HandleTouch();
    }

    private void HandleTouch()
    {
        // Начало касания
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    touchStartPos = touch.position;
                    isTouching = true;
                    break;

                case TouchPhase.Moved:
                    if (isTouching)
                    {
                        Vector2 delta = touch.position - touchStartPos;

                        rotation.x -= delta.y * sensitivity;
                        rotation.y += delta.x * sensitivity;

                        rotation.x = Mathf.Clamp(rotation.x, minX, maxX);
                        rotation.y = Mathf.Clamp(rotation.y, minY, maxY);

                        cam.rotation = Quaternion.Euler(rotation.x, rotation.y, 0);

                        touchStartPos = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isTouching = false;
                    break;
            }
        }
    }
}
