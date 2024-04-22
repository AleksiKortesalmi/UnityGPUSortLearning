using System;
using Unity.VisualScripting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Sort Transform array by distance.
/// </summary>
public class GPUTransformSort : GPUDistanceSort
{
    Vector3[] cache;
    Transform[] transformCache;

    int lengthCompensation, originalLength;

    int GetCompensation(int arrayLength) => MIN_ARRAY_LENGTH - arrayLength % MIN_ARRAY_LENGTH - arrayLength == MIN_ARRAY_LENGTH ? MIN_ARRAY_LENGTH : 0;
    int GetCorrectedLength(int arrayLength) => GetCompensation(arrayLength) + arrayLength;
    int CompensateIndex(int index) => index + lengthCompensation;
    int DeCompensateIndex(uint index) => (int)index - lengthCompensation;

    public override void Init(int arrayLength)
    {
        HandleChangedLength(arrayLength);

        base.Init(GetCorrectedLength(arrayLength));
    }

    public void SortByDistance(ref Transform[] transformArray, Vector3 target)
    {
        UpdateCache(ref transformArray, target);

        ComputeNonAlloc(ref cache, target);

        // Swap transform order to correct indices
        transformArray.CopyTo(transformCache, 0);

        for (int i = 0; i < transformArray.Length; i++)
        {
            transformArray[i] = transformCache[DeCompensateIndex(SortedIndices[CompensateIndex(i)])];
        }
    }

    private void UpdateCache(ref Transform[] transformArray, Vector3 target)
    {
        if (cache == null)
            throw new Exception("Instance not initialized. Make sure to call Init() before.");

        // Handle changed length of source array
        if (transformArray.Length != originalLength)
        {
            HandleChangedLength(transformArray.Length);

            Debug.LogWarning("Resizing caches causes GC. Are you sure you don't need another instance?");
        }

        // First fill compensation amount with the target position => distance 0
        for (int i = 0; i < lengthCompensation; i++)
        {
            cache[i] = target;
        }

        // Then fill with actual positions
        for (int i = 0; i < transformArray.Length; i++)
        {
            cache[CompensateIndex(i)] = transformArray[i].position;
        }
    }

    void HandleChangedLength(int newLength)
    {
        originalLength = newLength;
        lengthCompensation = GetCompensation(newLength);

        int correctedLength = GetCorrectedLength(newLength);

        // Position cache init/update
        if (cache != null)
            Array.Resize(ref cache, correctedLength);
        else
            cache = new Vector3[correctedLength];

        // Transform cache init/update
        if (transformCache != null)
            Array.Resize(ref transformCache, newLength);
        else
            transformCache = new Transform[newLength];
    }
}
