using System;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Sort Transform array by distance.
/// </summary>
public class SOTATransformSort : SOTADistanceSort
{
    Vector3[] cache;
    Transform[] transformCache;
    int originalLength;

    public override void Init(int arrayLength)
    {
        HandleChangedLength(arrayLength);

        base.Init(arrayLength);
    }

    public void SortByDistance(ref Transform[] transformArray, Vector3 target)
    {
        UpdateCache(ref transformArray);

        ComputeNonAlloc(ref cache, target);

        // Swap transform order to correct indices
        transformArray.CopyTo(transformCache, 0);

        for (int i = 0; i < transformArray.Length; i++)
        {
            transformArray[i] = transformCache[SortedIndices[i]];
        }
    }

    private void UpdateCache(ref Transform[] transformArray)
    {
        if (cache == null)
            throw new Exception("Instance not initialized. Make sure to call Init() before.");

        // Handle changed length of source array
        if (transformArray.Length != originalLength)
        {
            HandleChangedLength(transformArray.Length);

            Debug.LogWarning("Resizing caches causes GC. Are you sure you don't need another instance?");
        }

        // Fill with positions
        for (int i = 0; i < transformArray.Length; i++)
        {
            cache[i] = transformArray[i].position;
        }
    }

    void HandleChangedLength(int newLength)
    {
        originalLength = newLength;

        // Position cache init/update
        if (cache != null)
            Array.Resize(ref cache, newLength);
        else
            cache = new Vector3[newLength];

        // Transform cache init/update
        if (transformCache != null)
            Array.Resize(ref transformCache, newLength);
        else
            transformCache = new Transform[newLength];
    }
}
