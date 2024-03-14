using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class TransformSortTester : MonoBehaviour
{
    const int testLength = 2048 * 5;

    [SerializeField]
    GameObject prefab;
    [SerializeField]
    TransformSortUtility sortUtility;
    Transform[] array = new Transform[testLength];
    readonly List<Transform> list = new ();
    Vector3 target = Vector3.zero;

    private void Start()
    {
        Vector3 pos;
        for (int i = 0; i < testLength; i++)
        {
            pos = new Vector3(Random.Range(0.0f, 1000f), Random.Range(0.0f, 1000f), Random.Range(0.0f, 1000f));
            array[i] = Instantiate(prefab, pos, Quaternion.identity, transform).GetComponent<Transform>();
            list.Add(Instantiate(prefab, pos, Quaternion.identity, transform).GetComponent<Transform>());
        }

        Sort();
    }

    void Sort()
    {
        // Create a Stopwatch instance
        Stopwatch stopwatch = new Stopwatch();

        // Start the timer
        stopwatch.Start();

        sortUtility.Init(array.Length);
        sortUtility.SortByDistance(ref array, target);

        // Stop the timer
        stopwatch.Stop();

        // Get the elapsed time
        TimeSpan elapsedTime = stopwatch.Elapsed;

        Debug.Log(array.Length + " transforms sorted by distance, GPU");
        Debug.Log("GPU Execution Time: " + elapsedTime.TotalMilliseconds + " milliseconds");

        // Create a Stopwatch instance
        stopwatch = new Stopwatch();

        // Start the timer
        stopwatch.Start();

        SortCPU();

        // Stop the timer
        stopwatch.Stop();

        // Get the elapsed time
        elapsedTime = stopwatch.Elapsed;

        Debug.Log(array.Length + " transforms sorted by distance, CPU");
        Debug.Log("CPU Execution Time: " + elapsedTime.TotalMilliseconds + " milliseconds");

        ShowData();
    }

    void SortCPU()
    {
        list.Sort(CompareDistance);
    }

    int CompareDistance(Transform a, Transform b)
    {
        float squaredRangeA = (a.transform.position - target).sqrMagnitude;
        float squaredRangeB = (b.transform.position - target).sqrMagnitude;
        return squaredRangeA.CompareTo(squaredRangeB);
    }

    void ShowData()
    {
        for (int i = 0; i < 8; i += 1)
        {
            Debug.Log("i: " + i + ", GPU sorted pos: " + Vector3.Distance(array[i].position, target) + ", CPU sorted pos: " + Vector3.Distance(list[i].position, target));
        }
    }
}
