using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class DistanceSortTester : MonoBehaviour
{
    [SerializeField] ComputeShader shader;
    GraphicsBuffer indicesBuffer;
    GraphicsBuffer distancesBuffer;
    GraphicsBuffer positionsBuffer;

    const int CALC_THREAD_GROUP_SIZE = 1024;
    const int SORT_WORK_GROUP_SIZE = 32;
    const int BATCHERMERGE_WORK_GROUP_SIZE = 64;

    // Length has to be dividable of 2048
    readonly uint[] Indices = new uint[BATCHERMERGE_WORK_GROUP_SIZE * 2];
    readonly uint[] Distances = new uint[BATCHERMERGE_WORK_GROUP_SIZE * 2];
    readonly Vector3[] Positions = new Vector3[BATCHERMERGE_WORK_GROUP_SIZE * 2];
    readonly Vector3 target = Vector3.zero;

    void Start()
    {
        Debug.Log("Filling array with inverse sort, length: " + Indices.Length);

        // Fill with worst case
        for (uint i = 0; i < Indices.Length; i++)
        {
            Positions[i] = new Vector3(Random.Range(0.0f, Positions.Length * 4), Random.Range(0.0f, Positions.Length * 4), Random.Range(0.0f, Positions.Length * 4));
        }

        // Debug only
        ShowData();

        Debug.Log("Sorting...");

        // Create a Stopwatch instance
        Stopwatch stopwatch = new Stopwatch();

        int calcIndicesKernelIndex = shader.FindKernel("CalcIndices");
        int calcDistancesKernelIndex = shader.FindKernel("CalcDistances");
        int sortKernelIndex = shader.FindKernel("Sort");
        int batcherKernelIndex = shader.FindKernel("BatcherMerge");

        indicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, Indices.Length, sizeof(uint));
        distancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, Distances.Length, sizeof(uint));
        positionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, Positions.Length, sizeof(float) * 3);

        positionsBuffer.SetData(Positions);

        shader.SetBuffer(calcIndicesKernelIndex, "Indices", indicesBuffer);

        shader.SetVector("Target", target);
        shader.SetBuffer(calcDistancesKernelIndex, "Positions", positionsBuffer);
        shader.SetBuffer(calcDistancesKernelIndex, "Indices", indicesBuffer);
        shader.SetBuffer(calcDistancesKernelIndex, "Distances", distancesBuffer);

        shader.SetBuffer(sortKernelIndex, "Indices", indicesBuffer);
        shader.SetBuffer(sortKernelIndex, "Distances", distancesBuffer);

        shader.SetBuffer(batcherKernelIndex, "Indices", indicesBuffer);
        shader.SetBuffer(batcherKernelIndex, "Distances", distancesBuffer);

        shader.SetInt("Count", Indices.Length);

        int numThreadGroups = Mathf.CeilToInt((float) Indices.Length / CALC_THREAD_GROUP_SIZE);

        // CALCULATE INDICES (would happen in init)
        shader.Dispatch(calcIndicesKernelIndex, numThreadGroups, 1, 1);

        // Start the timer
        stopwatch.Start();

        // CALCULATE DISTANCES
        shader.Dispatch(calcDistancesKernelIndex, numThreadGroups, 1, 1);

        // SORT
        shader.Dispatch(sortKernelIndex, numThreadGroups, 1, 1);

        // DEBUG ONLY
        //indicesBuffer.GetData(Indices);
        //distancesBuffer.GetData(Distances);
        //ShowData();

        Debug.Log("Merging...");

        // BATCHER MERGE
        bool isOddDispatch = false;
        int passCount = Indices.Length / SORT_WORK_GROUP_SIZE;
        numThreadGroups = Mathf.CeilToInt((float)Indices.Length / BATCHERMERGE_WORK_GROUP_SIZE);

        Debug.Log("Passcount: " + passCount);
        shader.SetInt("groupCount", numThreadGroups);
        for (int i = 0; i < passCount; i++)
        {
            shader.SetBool("isOddDispatch", isOddDispatch);
            shader.Dispatch(batcherKernelIndex, numThreadGroups, 1, 1);

            // DEBUG ONLY
            //indicesBuffer.GetData(Indices);
            //distancesBuffer.GetData(Distances);
            //ShowData();

            isOddDispatch = !isOddDispatch;
        }

        // Stop the timer
        stopwatch.Stop();

        indicesBuffer.GetData(Indices);
        distancesBuffer.GetData(Distances);

        // Output
        Debug.Log("Result:");
        ShowData();

        // Get the elapsed time
        TimeSpan elapsedTime = stopwatch.Elapsed;

        // Print the duration in seconds
        Debug.Log("Array length: " + Indices.Length);
        Debug.Log("Execution Time: " + elapsedTime.TotalMilliseconds + " milliseconds");
    }

    void ShowData()
    {
        int errors = 0;
        List<uint> errorIndices = new List<uint>();

        for (int i = 0; i < Indices.Length; i++)
        {
            if (i + 1 < Indices.Length && Distances[i] > Distances[i + 1])
            {
                errors++;

                errorIndices.Add((uint)i + 1);
            }
        }

        for (int i = 0; i < Indices.Length; i += 1)
        {
            Debug.Log("i: " + i + ", index: " + Indices[i] + ", distance: " + Distances[i]);
        }

        Debug.Log(errors + " errors, indices: " + string.Join(", ", errorIndices));
    }

    private void OnDestroy()
    {
        indicesBuffer?.Release();
        indicesBuffer = null;
        distancesBuffer?.Release();
        distancesBuffer = null;
        positionsBuffer?.Release();
        positionsBuffer = null;
    }
}
