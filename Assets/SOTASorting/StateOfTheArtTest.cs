using GPUSorting.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class StateOfTheArtTest : MonoBehaviour
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
    Vector3 target = Vector3.zero;

    // Testing
    const int minTestLength = 2;
    [SerializeField]
    [Range(minTestLength, minTestLength * 1000000)]
    int testLength = minTestLength;

    Vector3[] positions;
    float[] keys;
    uint[] values;
    Stopwatch stopwatch = new Stopwatch();

    private void Start()
    {
        Init();

        Test();
    }

    private void Init()
    {
        Debug.Log("GPUSorting test initialization, testlength: " + testLength);

        deviceRadixSorter = new(dvr, testLength, ref temp0, ref temp1, ref temp2, ref temp3);

        if (deviceRadixSorter.Valid) Debug.Log("DVR initialization success.");
        else Debug.Log("DVR initialization failed.");

        keys = new float[testLength];
        values = new uint[testLength];
        positions = new Vector3[testLength];

        _keyBuffer = new(testLength, sizeof(float));
        _valueBuffer = new(testLength, sizeof(uint)); 
        _positionBuffer = new(testLength, sizeof(float) * 3);

        GenerateTestData();

        _keyBuffer.SetData(keys);
        _valueBuffer.SetData(values);
        _positionBuffer.SetData(positions);
    }

    void GenerateTestData()
    {
        Debug.Log("Filling array with random floats");
        for (uint i = 0; i < testLength; i++)
        {
            positions[i] = new Vector3(Random.Range(0.00f, 1000.00f), Random.Range(0.00f, 1000.00f), Random.Range(0.00f, 1000.00f));
            values[i] = i;
        }
    }

    void ComputeDistances()
    {
        // Input
        cd.SetBuffer(0, "Positions", _positionBuffer);
        cd.SetVector("Target", target);

        // Output
        cd.SetBuffer(0, "Distances", _keyBuffer);

        int numThreadGroups = Mathf.CeilToInt(testLength / computeDistanceThreadGroupSizeX);
        cd.Dispatch(0, numThreadGroups, 1, 1);
    }

    void Test()
    {
        Debug.Log("Validation:");
        ShowData();
        Debug.Log("Sorting:___________________________________");

        stopwatch.Reset();

        // Start the timer
        stopwatch.Start();

        ComputeDistances();

        deviceRadixSorter.Sort(
            testLength,
            _keyBuffer,
            _valueBuffer,
            temp0,
            temp1,
            temp2,
            temp3,
            typeOfKey,
            typeOfValue,
            shouldAscend);

        _keyBuffer.GetData(keys);
        _valueBuffer.GetData(values);

        // Stop the timer
        stopwatch.Stop();

        // Get the elapsed time
        TimeSpan elapsedTime = stopwatch.Elapsed;

        Debug.Log(keys.Length + " uints sorted");
        Debug.Log("GPUSorting.DeviceRadixSort Execution Time: " + elapsedTime.TotalMilliseconds + " milliseconds");

        Debug.Log("Validation:");
        ShowData();

        // Update errors to result
        Validate();
    }

    void ShowData()
    {
        for (int i = 0; i < testLength; i += testLength / 8)
        {
            Debug.Log("i: " + i + ", sorted index: " + values[i] + ", val: " + keys[i]);
        }
    }

    void Validate()
    {
        int gpuErrors = 0;
        List<uint> gpuErrorIndices = new();

        for (int i = 0; i < testLength; i++)
        {
            // Rounded because of false errors
            if (i + 1 < testLength && TruncateTo100ths(keys[i+1]) < TruncateTo100ths(keys[i]))
            {
                gpuErrorIndices.Add((uint)i + 1);
                gpuErrors++;
            }
        }

        Debug.Log(gpuErrors + " gpu errors, indices: " + string.Join(", ", gpuErrorIndices));
        float errorSum = 0;
        for (int i = 0; i < gpuErrorIndices.Count; i++)
        {
            Debug.Log("At index: " + (gpuErrorIndices[i] - 1) + ": " + keys[gpuErrorIndices[i] - 1]);
            Debug.Log("At index: " + gpuErrorIndices[i] + ": " + keys[gpuErrorIndices[i]]);
            errorSum = keys[gpuErrorIndices[i] - 1] - keys[gpuErrorIndices[i]];
        }
        Debug.Log("Error sum: " + errorSum);
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

    float TruncateTo100ths(float d)
    {
        return (float) Math.Truncate((decimal)d * 100) / 100;
    }
}
