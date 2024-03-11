using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class BatcherMergeTest : MonoBehaviour
{
    [SerializeField] ComputeShader shader;
    [SerializeField] GraphicsBuffer resultBuffer;
    // Extra buffer used to enable the odd-even transposition sorting
    [SerializeField] GraphicsBuffer copyBuffer;

    const int BATCHER_WORK_GROUP_SIZE = 32;

    // Length has to be dividable of 1024
    readonly uint[] data = new uint[] { 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

    void Start()
    {
        Debug.Log("Length: " + data.Length);

        ShowData();

        Debug.Log("Merging...");

        // Create a Stopwatch instance
        Stopwatch stopwatch = new Stopwatch();

        // Start the timer
        stopwatch.Start();

        int batcherKernelIndex = shader.FindKernel("BatcherMerge");

        resultBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, data.Length, sizeof(uint));

        resultBuffer.SetData(data);

        shader.SetBuffer(batcherKernelIndex, "Data", resultBuffer);

        // BATCHER MERGE
        bool isOddDispatch = false;
        int passCount = data.Length / BATCHER_WORK_GROUP_SIZE;
        int numThreadGroups = Mathf.CeilToInt((float)data.Length / BATCHER_WORK_GROUP_SIZE);

        //Debug.Log("Passcount: " + passCount);
        shader.SetInt("groupCount", numThreadGroups);
        for (int i = 0; i < passCount; i++)
        {
            shader.SetBool("isOddDispatch", isOddDispatch);
            shader.Dispatch(batcherKernelIndex, numThreadGroups, 1, 1);

            isOddDispatch = !isOddDispatch;
        }

        resultBuffer.GetData(data);

        // Stop the timer
        stopwatch.Stop();

        // Output
        Debug.Log("Result:");
        ShowData();

        // Get the elapsed time
        TimeSpan elapsedTime = stopwatch.Elapsed;

        // Print the duration in seconds
        Debug.Log("Execution Time: " + elapsedTime.TotalMilliseconds + " milliseconds");
    }

    void ShowData()
    {
        for (int i = 0; i < data.Length; i++)
        {
            Debug.Log("i: " + i + ", val: " + data[i]);
        }
    }

    private void OnDestroy()
    {
        resultBuffer?.Release();
        resultBuffer = null;
        copyBuffer?.Release();
        copyBuffer = null;
    }
}
