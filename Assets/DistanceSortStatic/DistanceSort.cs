using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class DistanceSort : MonoBehaviour
{
    [SerializeField] ComputeShader shader;
    GraphicsBuffer indicesBuffer;
    GraphicsBuffer distancesBuffer;

    const int SORT_WORK_GROUP_SIZE = 64;
    const int BATCHERMERGE_WORK_GROUP_SIZE = 128;

    // Length has to be dividable of 2048
    readonly uint[] Indices = new uint[BATCHERMERGE_WORK_GROUP_SIZE * 1000];
    readonly uint[] Distances = new uint[BATCHERMERGE_WORK_GROUP_SIZE * 1000];

    void Compute()
    {
        int sortKernelIndex = shader.FindKernel("Sort");
        int batcherKernelIndex = shader.FindKernel("BatcherMerge");

        indicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, Indices.Length, sizeof(uint));
        distancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, Distances.Length, sizeof(uint));

        indicesBuffer.SetData(Indices);
        distancesBuffer.SetData(Distances);

        shader.SetBuffer(sortKernelIndex, "Indices", indicesBuffer);
        shader.SetBuffer(batcherKernelIndex, "Indices", indicesBuffer);

        shader.SetBuffer(sortKernelIndex, "Distances", distancesBuffer);
        shader.SetBuffer(batcherKernelIndex, "Distances", distancesBuffer);

        shader.SetInt("Count", Indices.Length);

        int numThreadGroups = Mathf.CeilToInt((float) Indices.Length / SORT_WORK_GROUP_SIZE);

        // SORT
        shader.Dispatch(sortKernelIndex, numThreadGroups, 1, 1);

        // BATCHER MERGE
        bool isOddDispatch = false;
        int passCount = Indices.Length / SORT_WORK_GROUP_SIZE;
        numThreadGroups = Mathf.CeilToInt((float)Indices.Length / BATCHERMERGE_WORK_GROUP_SIZE);

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

        indicesBuffer.GetData(Indices);
        distancesBuffer.GetData(Distances);
    }

    private void OnDestroy()
    {
        indicesBuffer?.Release();
        indicesBuffer = null;
        distancesBuffer?.Release();
        distancesBuffer = null;
    }
}
