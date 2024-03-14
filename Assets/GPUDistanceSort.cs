using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class GPUDistanceSort : MonoBehaviour
{
    [SerializeField] ComputeShader shader;
    GraphicsBuffer indexBuffer;
    GraphicsBuffer valueBuffer;

    const int SORT_WORK_GROUP_SIZE = 256;
    const int BATCHERMERGE_WORK_GROUP_SIZE = 512;

    public uint[] SortedIndices { get => indices; }
    uint[] indices;

    public void Init(int arrayLength)
    {
        indices = new uint[arrayLength];
        // Populate indices
        for (uint i = 0; i < arrayLength; i++)
        {
            indices[i] = i;
        }
    }

    public uint[] Compute(ref Vector3[] posArray, Vector3 target)
    {
        if (indices == null)
            throw new Exception("GPUDistanceSort instance not initialized. Make sure to call Init() before.");

        int sortKernelIndex = shader.FindKernel("Sort");
        int batcherKernelIndex = shader.FindKernel("BatcherMerge");

        indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, indices.Length, sizeof(uint));
        valueBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, posArray.Length, sizeof(float) * 3);

        indexBuffer.SetData(indices);
        valueBuffer.SetData(posArray);

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

        // BATCHER MERGE
        bool isOddDispatch = false;
        int passCount = indices.Length / SORT_WORK_GROUP_SIZE;
        numThreadGroups = Mathf.CeilToInt((float)indices.Length / BATCHERMERGE_WORK_GROUP_SIZE);

        //Debug.Log("Passcount: " + passCount);
        shader.SetInt("groupCount", numThreadGroups);
        for (int i = 0; i < passCount; i++)
        {
            shader.SetBool("isOddDispatch", isOddDispatch);
            shader.Dispatch(batcherKernelIndex, numThreadGroups, 1, 1);

            isOddDispatch = !isOddDispatch;
        }

        indexBuffer.GetData(indices);

        return indices;
    }

    private void OnDestroy()
    {
        indexBuffer?.Release();
        indexBuffer = null;
        valueBuffer?.Release();
        valueBuffer = null;
    }
}
