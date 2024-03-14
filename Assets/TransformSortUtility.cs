using System;
using System.Diagnostics;
using Unity.VisualScripting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class TransformSortUtility : GPUDistanceSort
{
    // Length has to be dividable of 2048
    Vector3[] cache;
    Transform[] transformCache;

    void UpdateCache(ref Transform[] array)
    {
        if (cache == null)
            cache = new Vector3[array.Length];

        for (int i = 0; i < array.Length; i++)
        {
            cache[i] = array[i].position;
        }
    }

    public void SortByDistance(ref Transform[] array, Vector3 target)
    {
        UpdateCache(ref array);

        Compute(ref cache, target);

        transformCache = new Transform[array.Length];
        array.CopyTo(transformCache, 0);

        for (int i = 0; i < array.Length; i++)
        {
            array[i] = transformCache[SortedIndices[i]];
        }
    }
}
