using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// UI-скрипт магазина. Прикрепи к объекту Shop-меню.
/// 
/// В Inspector:
///   elementsMaterial  — материал для цветных элементов вагона (стены, сиденья и т.д.)
///   floorMaterial     — материал пола
///   graffitiRemoveCost — цена удаления одного граффити (по умолчанию 1000)
/// 
/// Цвета редактируются прямо в Inspector через ColorConfig.
/// </summary>
public class ShopMenu : MonoBehaviour
{
    private const int SellIdColor = 1; // -500 монет
    private const int SellIdGraffiti = 2; // -1000 монет
    [Header("Материалы вагона")]
    [SerializeField] private Material elementsMaterial;   // цвет элементов (стены, сиденья)
    [SerializeField] private Material floorMaterial;      // цвет пола

    [Header("Цвета элементов")]
    [SerializeField]
    private ColorOption[] elementColorOptions = new ColorOption[]
    {
        new ColorOption { label = "Красный",  primary = new Color(0.85f, 0.18f, 0.18f), secondary = new Color(0.60f, 0.10f, 0.10f) },
        new ColorOption { label = "Зелёный",  primary = new Color(0.18f, 0.72f, 0.35f), secondary = new Color(0.10f, 0.50f, 0.22f) },
        new ColorOption { label = "Синий",    primary = new Color(0.18f, 0.45f, 0.85f), secondary = new Color(0.10f, 0.28f, 0.60f) },
    };

    [Header("Цвета пола")]
    [SerializeField]
    private ColorOption[] floorColorOptions = new ColorOption[]
    {
        new ColorOption { label = "Красный",  primary = new Color(0.75f, 0.15f, 0.15f), secondary = new Color(0.50f, 0.08f, 0.08f) },
        new ColorOption { label = "Зелёный",  primary = new Color(0.15f, 0.60f, 0.28f), secondary = new Color(0.08f, 0.40f, 0.18f) },
        new ColorOption { label = "Синий",    primary = new Color(0.15f, 0.38f, 0.75f), secondary = new Color(0.08f, 0.22f, 0.50f) },
    };

    [Header("Колбэки")]
    [Tooltip("Вызывается после любой покупки. Передаёт актуальный баланс монет.")]
    public UnityEvent<int> OnPurchaseSuccess;

    [Tooltip("Вызывается при нехватке монет. Передаёт сколько не хватает.")]
    public UnityEvent<int> OnNotEnoughCoins;

    // ── Цвета элементов ───────────────────────────────────────────────────────

    /// <summary>Привяжи к кнопкам смены цвета элементов (0=красный, 1=зелёный, 2=синий)</summary>
    public async void PayElementColor(int colorIndex)
    {
        await ProcessPurchaseAsync(SellIdColor, async () =>
        {
            ApplyColor(elementsMaterial, elementColorOptions, colorIndex, "элементов");
        });
    }

    /// <summary>Привяжи к кнопкам смены цвета пола (0=красный, 1=зелёный, 2=синий)</summary>
    public async void PayFloorColor(int colorIndex)
    {
        await ProcessPurchaseAsync(SellIdColor, async () =>
        {
            ApplyColor(floorMaterial, floorColorOptions, colorIndex, "пола");
        });
    }

    // Удобные прямые методы для кнопок в Inspector (без параметров)
    public async void PayElementColorRed() => await ProcessPurchaseAsync(SellIdColor, async () =>
    {
        ApplyColor(elementsMaterial, elementColorOptions, 0, "элементов");
        ApplyColor(floorMaterial, floorColorOptions, 0, "пола");
    });

    public async void PayElementColorGreen() => await ProcessPurchaseAsync(SellIdColor, async () =>
    {
        ApplyColor(elementsMaterial, elementColorOptions, 1, "элементов");
        ApplyColor(floorMaterial, floorColorOptions, 1, "пола");
    });
    public async void PayElementColorBlue() => await ProcessPurchaseAsync(SellIdColor, async () =>
    {
        ApplyColor(elementsMaterial, elementColorOptions, 2, "элементов");
        ApplyColor(floorMaterial, floorColorOptions, 2, "пола");
    });


    // ── Граффити ──────────────────────────────────────────────────────────────

    /// <summary>Привяжи к кнопке "Удалить граффити" в магазине.</summary>
    public async void PayRemoveGraffiti()
    {
        var graffitiStorage = TimeStateGarbage.Instance.GetGraffitiStorage();

        if (graffitiStorage.Count == 0)
        {
            Debug.Log("[ShopMenu] Граффити нет — вагон уже чист");
            return;
        }

        await ProcessPurchaseAsync(SellIdGraffiti, async () =>
        {
            graffitiStorage.RemoveRandom();
            new JsonGraffitiSaver().Save(graffitiStorage.graffiti);
            Debug.Log($"[ShopMenu] Граффити удалено. Осталось: {graffitiStorage.Count}");
        });
    }

    // ── Общая логика покупки ──────────────────────────────────────────────────

    private async Task ProcessPurchaseAsync(int sellId, System.Func<Task> onSuccess)
    {
        var user = TimeStateGarbage.Instance.GetUser();

        // Отправляем покупку на сервер
        bool bought = await MetroService.BuyAsync(user.id, sellId);
        if (!bought)
        {
            Debug.LogError($"[ShopMenu] Сервер отклонил покупку sell_id={sellId}");
            // Сервер сам проверяет баланс — если отказал, значит не хватило монет
            OnNotEnoughCoins?.Invoke(0);
            return;
        }

        // Выполняем игровое действие
        await onSuccess();

        // Синхронизируем баланс с сервером
        int freshCoins = await MetroService.GetCoinAsync(user.id);
        if (freshCoins >= 0)
        {
            user.coin = freshCoins;
            new MockUserSaver().Save(user);
            Debug.Log($"[ShopMenu] Баланс обновлён с сервера: {user.coin}");
        }

        OnPurchaseSuccess?.Invoke(user.coin);
    }

    // ── Приватные утилиты ─────────────────────────────────────────────────────

    private void ApplyColor(Material mat, ColorOption[] options, int index, string label)
    {
        if (mat == null)
        {
            Debug.LogError($"[ShopMenu] Материал {label} не назначен в Inspector");
            return;
        }

        if (index < 0 || index >= options.Length)
        {
            Debug.LogError($"[ShopMenu] Индекс цвета {index} выходит за пределы массива {label}");
            return;
        }

        var option = options[index];

        // _Color — основной цвет материала (Standard Shader / URP Lit)
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", option.primary);

        // _BaseColor — для URP
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", option.primary);

        // _SecondaryColor — кастомное свойство, если есть в шейдере
        if (mat.HasProperty("_SecondaryColor"))
            mat.SetColor("_SecondaryColor", option.secondary);

        Debug.Log($"[ShopMenu] Цвет {label} изменён на «{option.label}» " +
                  $"(primary: {option.primary}, secondary: {option.secondary})");
    }

}

/// <summary>
/// Пара цветов для одного варианта окраски.
/// primary   — основной цвет (_Color / _BaseColor)
/// secondary — дополнительный цвет (_SecondaryColor) если шейдер поддерживает
/// </summary>
[System.Serializable]
public class ColorOption
{
    public string label;
    public Color primary = Color.white;
    public Color secondary = Color.gray;
}