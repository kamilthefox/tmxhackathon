using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Хранилище граффити. Структура идентична StorageGarbage.
/// Граффити не возобновляются — убрать можно только через магазин.
/// </summary>
[Serializable]
public class StorageGraffiti
{
    [SerializeField] private GameObject[] graffitiPool;
    [SerializeField] private Transform[] pointAtPosition;

    public int GetLengthAllPoints => pointAtPosition.Length;

    [NonSerialized] public StorageData graffiti = new StorageData();

    [Serializable]
    public class StorageData : IEnumerable<Graffiti>
    {
        public List<Graffiti> graffitiList = new List<Graffiti>();

        public void Add(Graffiti g) => graffitiList.Add(g);
        public void Remove(Graffiti g) => graffitiList.Remove(g);

        public IEnumerator<Graffiti> GetEnumerator() => graffitiList.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => graffitiList.GetEnumerator();
    }

    /// <summary>Спавним случайное граффити на свободной позиции.</summary>
    public GameObject GetGraffitiRandom()
    {
        int rndPosition = RandomFreePosition();
        if (rndPosition < 0) return null;

        int rnd = UnityEngine.Random.Range(0, graffitiPool.Length);

        graffiti.Add(new Graffiti { id = rnd, positionAtPoint = rndPosition });

        return SpawnAt(rnd, rndPosition);
    }

    /// <summary>Восстанавливаем граффити по сохранённым данным при загрузке.</summary>
    public GameObject GetGraffitiToID(int id, int positionId)
    {
        return SpawnAt(id, positionId);
    }

    /// <summary>
    /// Убирает одно случайное граффити.
    /// Возвращает true если убрали, false если граффити нет.
    /// </summary>
    public bool RemoveRandom()
    {
        if (graffiti.graffitiList.Count == 0)
        {
            Debug.Log("[StorageGraffiti] Граффити нет — вагон чист");
            return false;
        }

        int rndIndex = UnityEngine.Random.Range(0, graffiti.graffitiList.Count);
        int posId = graffiti.graffitiList[rndIndex].positionAtPoint;

        RemoveAt(posId);
        return true;
    }

    /// <summary>Убирает граффити на конкретной позиции.</summary>
    public void RemoveAt(int positionId)
    {
        var target = graffiti.graffitiList.FirstOrDefault(g => g.positionAtPoint == positionId);
        if (target != null)
            graffiti.Remove(target);

        // Уничтожаем дочерний GameObject точки
        if (pointAtPosition != null && positionId < pointAtPosition.Length)
        {
            Transform point = pointAtPosition[positionId];
            if (point.childCount > 0)
                UnityEngine.Object.Destroy(point.GetChild(0).gameObject);
        }
    }

    public int Count => graffiti.graffitiList.Count;

    // ── Приватные утилиты ─────────────────────────────────────────────────────

    private GameObject SpawnAt(int id, int positionId)
    {
        var instance = UnityEngine.Object.Instantiate(graffitiPool[id]);
        if (pointAtPosition != null && positionId < pointAtPosition.Length)
        {
            instance.transform.SetParent(pointAtPosition[positionId]);

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localEulerAngles = Vector3.zero;
        }
        return instance;
    }

    private int RandomFreePosition()
    {
        var occupied = graffiti.graffitiList.Select(g => g.positionAtPoint).ToHashSet();
        var free = new List<int>();

        for (int i = 0; i < pointAtPosition.Length; i++)
            if (!occupied.Contains(i))
                free.Add(i);

        if (free.Count == 0)
        {
            Debug.LogWarning("[StorageGraffiti] Нет свободных позиций для граффити");
            return -1;
        }

        return free[UnityEngine.Random.Range(0, free.Count)];
    }
}