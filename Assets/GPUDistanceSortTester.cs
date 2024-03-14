using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class GPUDistanceSortTester : MonoBehaviour
{
    [SerializeField] ComputeShader shader;
    [SerializeField] GraphicsBuffer indexBuffer;
    [SerializeField] GraphicsBuffer valueBuffer;

    const int SORT_WORK_GROUP_SIZE = 256;
    const int BATCHERMERGE_WORK_GROUP_SIZE = 512;

    const int TEST_ARRAY_LENGTH_MULTIPLIER = 100;

    [SerializeField]
    bool randomizeVectors = false;

    // Length has to be dividable of 2048
    readonly uint[] indices = new uint[BATCHERMERGE_WORK_GROUP_SIZE * TEST_ARRAY_LENGTH_MULTIPLIER];
    readonly Vector3[] values = new Vector3[BATCHERMERGE_WORK_GROUP_SIZE * TEST_ARRAY_LENGTH_MULTIPLIER];
    readonly Vector3 target = Vector3.zero;

    private void Start()
    {
        Test();
    }

    void Test()
    {
        Debug.Log("Filling array with inverse sort, length: " + indices.Length);

        // Fill data with worst case
        for (uint i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        for (uint i = 0; i < values.Length; i++)
        {
            if(randomizeVectors)
                values[i] = new Vector3(Random.Range(0.0f, 1000f), Random.Range(0.0f, 1000f), Random.Range(0.0f, 1000f));
            else
                values[i] = new Vector3(Random.Range(0, 10) - i - .5f, Random.Range(0, 10) - i - .5f, Random.Range(0, 10) - i -.5f);
        }

        // Debug only
        ShowData();

        Debug.Log("Sorting...");

        // Create a Stopwatch instance
        Stopwatch stopwatch = new Stopwatch();

        // Start the timer
        stopwatch.Start();

        int sortKernelIndex = shader.FindKernel("Sort");
        int batcherKernelIndex = shader.FindKernel("BatcherMerge");

        indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, indices.Length, sizeof(uint));
        valueBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, values.Length, sizeof(float) * 3);

        indexBuffer.SetData(indices);
        valueBuffer.SetData(values);

        // Set buffers for both SORT and MERGE kernels
        shader.SetBuffer(sortKernelIndex, "Indices", indexBuffer);
        shader.SetBuffer(batcherKernelIndex, "Indices", indexBuffer);

        shader.SetBuffer(sortKernelIndex, "Values", valueBuffer);
        shader.SetBuffer(batcherKernelIndex, "Values", valueBuffer);

        shader.SetInt("Count", indices.Length);
        shader.SetVector("Target", target);

        int numThreadGroups = Mathf.CeilToInt((float) indices.Length / SORT_WORK_GROUP_SIZE);

        // SORT
        shader.Dispatch(sortKernelIndex, numThreadGroups, 1, 1);

        // DEBUG ONLY
        //indexBuffer.GetData(indices);
        //ShowData();

        Debug.Log("Merging...");

        // BATCHER MERGE
        bool isOddDispatch = false;
        int passCount = indices.Length / SORT_WORK_GROUP_SIZE;
        numThreadGroups = Mathf.CeilToInt((float)indices.Length / BATCHERMERGE_WORK_GROUP_SIZE);

        Debug.Log("Passcount: " + passCount);
        shader.SetInt("groupCount", numThreadGroups);
        for (int i = 0; i < passCount; i++)
        {
            shader.SetBool("isOddDispatch", isOddDispatch);
            shader.Dispatch(batcherKernelIndex, numThreadGroups, 1, 1);

            // DEBUG ONLY
            //indexBuffer.GetData(indices);
            //Debug.Log("Merge pass " +  i + ":");
            //ShowData();

            isOddDispatch = !isOddDispatch;
        }

        indexBuffer.GetData(indices);

        // Stop the timer
        stopwatch.Stop();

        // Output
        Debug.Log("Result:");
        ShowData();

        // Get the elapsed time
        TimeSpan elapsedTime = stopwatch.Elapsed;

        // Print the duration in seconds
        Debug.Log("Array length: " + indices.Length);
        Debug.Log("Execution Time: " + elapsedTime.TotalMilliseconds + " milliseconds");
    }

    void ShowData()
    {
        int errors = 0;
        List<uint> errorIndices = new List<uint>();

        for (int i = 0; i < indices.Length; i++)
        {
            // Rounded because of false errors
            if (i + 1 < indices.Length && Math.Round(Vector3.Distance(values[indices[i + 1]], target), 3) < Math.Round(Vector3.Distance(values[indices[i]], target), 3))
            {
                errorIndices.Add((uint)i + 1);
                errors++;
            }
        }

        for (int i = 0; i < indices.Length; i += indices.Length / 8)
        {
            Debug.Log("i: " + i + ", value index: " + indices[i] + ", val: " + values[indices[i]] + ", dist: " + Vector3.Distance(values[indices[i]], target));
        }

        Debug.Log(errors + " errors, indices: " + string.Join(", ", errorIndices));
        for (int i = 0; i < errorIndices.Count; i++)
        {
            Debug.Log("At index: " + (errorIndices[i] - 1) + ": " + Vector3.Distance(values[indices[errorIndices[i] - 1]], target));
            Debug.Log("At index: " + errorIndices[i] + ": " + Vector3.Distance(values[indices[errorIndices[i]]], target));
        }
    }

    private void OnDestroy()
    {
        indexBuffer?.Release();
        indexBuffer = null;
        valueBuffer?.Release();
        valueBuffer = null;
    }
}
