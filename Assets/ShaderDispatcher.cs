using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ShaderDispatcher : MonoBehaviour
{
    [SerializeField] ComputeShader shader;
    [SerializeField] GraphicsBuffer resultBuffer;
    // Extra buffer used to enable the odd-even transposition sorting
    [SerializeField] GraphicsBuffer copyBuffer;

    const int SORT_WORK_GROUP_SIZE = 1024;
    const int MERGE_THREAD_GROUP_SIZE = 1024;
    const int BATCHERMERGE_WORK_GROUP_SIZE = 2048;

    // Length has to be dividable of 2048
    readonly uint[] data = new uint[BATCHERMERGE_WORK_GROUP_SIZE * 3];

    void Start()
    {
        Debug.Log("Filling array with inverse sort, length: " + data.Length);

        // Fill with worst case
        for (uint i = 0; i < data.Length; i++)
        {
            data[i] = (uint)data.Length - i - 1;
        }

        // Debug only
        //ShowData();

        Debug.Log("Sorting...");

        // Create a Stopwatch instance
        Stopwatch stopwatch = new Stopwatch();

        // Start the timer
        stopwatch.Start();

        int sortKernelIndex = shader.FindKernel("Sort");
        int batcherKernelIndex = shader.FindKernel("BatcherMerge");

        resultBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, data.Length, sizeof(uint));

        resultBuffer.SetData(data);

        shader.SetBuffer(sortKernelIndex, "Data", resultBuffer);
        shader.SetBuffer(batcherKernelIndex, "Data", resultBuffer);

        shader.SetInt("Count", data.Length);

        int numThreadGroups = Mathf.CeilToInt((float) data.Length / SORT_WORK_GROUP_SIZE);

        // SORT
        shader.Dispatch(sortKernelIndex, numThreadGroups, 1, 1);

        // DEBUG ONLY
        /*resultBuffer.GetData(data);
        ShowData();*/

        Debug.Log("Merging...");

        // BATCHER MERGE
        bool isOddDispatch = false;
        int passCount = data.Length / SORT_WORK_GROUP_SIZE;
        numThreadGroups = Mathf.CeilToInt((float)data.Length / BATCHERMERGE_WORK_GROUP_SIZE);

        //Debug.Log("Passcount: " + passCount);
        shader.SetInt("groupCount", numThreadGroups);
        for (int i = 0; i < passCount; i++)
        {
            shader.SetBool("isOddDispatch", isOddDispatch);
            shader.Dispatch(batcherKernelIndex, numThreadGroups, 1, 1);

            // DEBUG ONLY
            /*resultBuffer.GetData(data);
            Debug.Log("Merge pass " +  i + ":");
            ShowData();*/

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
        for (int i = 0; i < data.Length; i += 1)
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
