using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using static BenchmarkResult;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class TransformSortDebug : MonoBehaviour
{
    const int minTestLength = 1024;
    [SerializeField]
    [Range(minTestLength, minTestLength * 1000)]
    int testLength = 1;

    [SerializeField]
    GameObject prefab;
    [SerializeField]
    TransformSortUtility sortUtility;
    Transform[] array;
    Vector3 target = Vector3.zero;
    Stopwatch stopwatch = new Stopwatch();

    private void Start()
    {
        Init();

        GenerateTestData();

        Test();
    }

    private void Init()
    {
        Debug.Log("Filling arrays with transforms, amount: " + testLength);

        // Delete old instantiated objects
        for (int i = 0; i < array?.Length; i++)
        {
            Destroy(array[i].gameObject);
        }

        array = new Transform[testLength];

        sortUtility.Init(array.Length);
    }

    void GenerateTestData()
    {
        Debug.Log("Setting transforms to random positions");
        Vector3 pos;
        for (int i = 0; i < testLength; i++)
        {
            pos = i == testLength - 1 ? target : new Vector3(Random.Range(0.0f, 1000f), Random.Range(0.0f, 1000f), Random.Range(0.0f, 1000f));
            array[i] = Instantiate(prefab, pos, Quaternion.identity, transform).GetComponent<Transform>();
        }
    }

    void Test()
    {
        stopwatch.Reset();

        // Start the timer
        stopwatch.Start();

        sortUtility.SortByDistance(ref array, target);

        // Stop the timer
        stopwatch.Stop();

        // Get the elapsed time
        TimeSpan elapsedTime = stopwatch.Elapsed;

        Debug.Log(array.Length + " transforms sorted by distance");
        Debug.Log("GPU Execution Time: " + elapsedTime.TotalMilliseconds + " milliseconds");

        Debug.Log("Validation:");
        ShowData();

        // Update errors to result
        Validate();
    }

    void ShowData()
    {
        for (int i = 0; i < testLength; i += testLength / 8)
        {
            Debug.Log("i: " + i + ", GPU sorted pos: " + Vector3.Distance(array[i].position, target));
        }
    }

    void Validate()
    {
        int gpuErrors = 0;
        List<uint> gpuErrorIndices = new ();

        for (int i = 0; i < testLength; i++)
        {
            // Rounded because of false errors
            if (i + 1 < testLength && Math.Round(Vector3.Distance(array[i + 1].position, target), 3) < Math.Round(Vector3.Distance(array[i].position, target), 3))
            {
                gpuErrorIndices.Add((uint)i + 1);
                gpuErrors++;
            }
        }

        Debug.Log(gpuErrors + " gpu errors, indices: " + string.Join(", ", gpuErrorIndices));
        float errorSum = 0;
        for (int i = 0; i < gpuErrorIndices.Count; i++)
        {
            Debug.Log("At index: " + (gpuErrorIndices[i] - 1) + ": " + Vector3.Distance(array[gpuErrorIndices[i] - 1].position, target));
            Debug.Log("At index: " + gpuErrorIndices[i] + ": " + Vector3.Distance(array[gpuErrorIndices[i]].position, target));
            errorSum = Vector3.Distance(array[gpuErrorIndices[i] - 1].position, target) - Vector3.Distance(array[gpuErrorIndices[i]].position, target);
        }
        Debug.Log("Error sum: " + errorSum);
    }
}
