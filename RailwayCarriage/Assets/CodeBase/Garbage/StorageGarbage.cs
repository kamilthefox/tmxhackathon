using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public class StorageGarbage
{
    [SerializeField] private GameObject[] garbagePool;
    [SerializeField] private Transform[] pointAtPosition;

    public int GetLengthAllPoints => pointAtPosition.Length;

    [NonSerialized] public StorageData garbage = new StorageData();

    [Serializable]
    public class StorageData : IEnumerable<Garbage>
    {
        public List<Garbage> garbageList = new List<Garbage>();

        public void Add(Garbage garbage)
        {
            garbageList.Add(garbage);
        }
        public void Remove(Garbage garbage)
        {
            garbageList.Remove(garbage);
        }

        public IEnumerator<Garbage> GetEnumerator()
        {
            return garbageList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return garbageList.GetEnumerator();
        }
    }

    public GameObject GetGarbageRandom()
    {
        int rnd = UnityEngine.Random.Range(0, garbagePool.Length);
        int rndPosition = randomPointAtPosition();
        if (rndPosition < 0) return null;

        garbage.Add(new Garbage() { id = rnd, positionAtPoint = rndPosition });

        return SpawnAt(rnd, rndPosition);
    }

    public GameObject GetGarbageToID(int id, int positionId)
    {
        return SpawnAt(id, positionId);
    }

    private GameObject SpawnAt(int id, int positionId)
    {
        GameObject instance = GameObject.Instantiate(garbagePool[id]);
        if (pointAtPosition != null && positionId < pointAtPosition.Length)
        {
            instance.transform.SetParent(pointAtPosition[positionId]);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localEulerAngles = Vector3.zero;
        }
        return instance;
    }

    public int randomPointAtPosition()
    {
        var occupiedPositions = garbage.Select(g => g.positionAtPoint).ToList();
        var freePositions = new List<int>();

        for (int i = 0; i < pointAtPosition.Length; i++)
        {
            if (!occupiedPositions.Contains(i))
            {
                freePositions.Add(i);
            }
        }

        if (freePositions.Count == 0)
        {
            Debug.LogError("Нет свободных позиций!");
            return -1;
        }

        return freePositions[UnityEngine.Random.Range(0, freePositions.Count)];
    }

    public void RemoveGarbage(int positionId)
    {
        var garbageToRemove = garbage.FirstOrDefault(g => g.positionAtPoint == positionId);
        if (garbageToRemove != null)
            garbage.Remove(garbageToRemove);

        // Уничтожаем дочерний GameObject точки (там живёт инстанс префаба)
        if (pointAtPosition != null && positionId < pointAtPosition.Length)
        {
            Transform point = pointAtPosition[positionId];
            if (point.childCount > 0)
                GameObject.Destroy(point.GetChild(0).gameObject);
        }
    }


}