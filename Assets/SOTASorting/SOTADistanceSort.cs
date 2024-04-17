using GPUSorting.Runtime;
using System;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class SOTADistanceSort : MonoBehaviour
{
    // GPUSorting variables
    [SerializeField]
    ComputeShader dvr;
    DeviceRadixSort deviceRadixSorter;
    ComputeBuffer _keyBuffer;        // Pre-computed distances
    ComputeBuffer _valueBuffer;      // Indices
    ComputeBuffer temp0;
    ComputeBuffer temp1;
    ComputeBuffer temp2;
    ComputeBuffer temp3;
    Type typeOfKey = typeof(float);
    Type typeOfValue = typeof(uint);
    bool shouldAscend = true;

    // Distance computing
    const float computeDistanceThreadGroupSizeX = 1024;
    [SerializeField]
    ComputeShader cd;
    ComputeBuffer _positionBuffer;

    float[] keys;
    uint[] values;

    public uint[] SortedIndices { get => values; }

    public virtual void Init(int arrayLength)
    {
        deviceRadixSorter = new(dvr, arrayLength, ref temp0, ref temp1, ref temp2, ref temp3);

        if (deviceRadixSorter.Valid) Debug.Log("DVR initialization success.");
        else Debug.LogError("DVR initialization failed.");

        keys = new float[arrayLength];
        values = new uint[arrayLength];

        _keyBuffer = new(arrayLength, sizeof(float));
        _valueBuffer = new(arrayLength, sizeof(uint));
        _positionBuffer = new(arrayLength, sizeof(float) * 3);

        _keyBuffer.SetData(keys);
        _valueBuffer.SetData(values);
    }

    void ComputeDistancesAndResetIndices(ref Vector3[] posArray, Vector3 target)
    {
        _positionBuffer.SetData(posArray);

        // Input init
        cd.SetBuffer(0, "Positions", _positionBuffer);
        cd.SetVector("Target", target);

        // Output init
        cd.SetBuffer(0, "Indices", _valueBuffer);
        cd.SetBuffer(0, "Distances", _keyBuffer);

        int numThreadGroups = Mathf.CeilToInt(_positionBuffer.count / computeDistanceThreadGroupSizeX);
        cd.Dispatch(0, numThreadGroups, 1, 1);
    }

    protected uint[] ComputeNonAlloc(ref Vector3[] posArray, Vector3 target)
    {
        ComputeDistancesAndResetIndices(ref posArray, target);

        deviceRadixSorter.Sort(
            posArray.Length,
            _keyBuffer,
            _valueBuffer,
            temp0,
            temp1,
            temp2,
            temp3,
            typeOfKey,
            typeOfValue,
            shouldAscend);

        _valueBuffer.GetData(values);

        return values;
    }

    private void OnDestroy()
    {
        _positionBuffer?.Release();
        _positionBuffer = null;
        _keyBuffer?.Release();
        _keyBuffer = null;
        _valueBuffer?.Release();
        _valueBuffer = null;
        temp0?.Release();
        temp0 = null;
        temp1?.Release();
        temp1 = null;
        temp2?.Release();
        temp2 = null;
        temp3?.Release();
        temp3 = null;
    }
}
