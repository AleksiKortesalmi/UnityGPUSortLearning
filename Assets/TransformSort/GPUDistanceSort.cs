using System;
using UnityEngine;

public class GPUDistanceSort : MonoBehaviour
{
    [SerializeField] ComputeShader shader;
    GraphicsBuffer indexBuffer;
    GraphicsBuffer valueBuffer;

    public const int MIN_ARRAY_LENGTH = BATCHERMERGE_WORK_GROUP_SIZE;
    const int SORT_THREAD_GROUP_SIZE = 16;
    const int SORT_WORK_GROUP_SIZE = 2 * SORT_THREAD_GROUP_SIZE;
    const int BATCHERMERGE_WORK_GROUP_SIZE = 2 * SORT_WORK_GROUP_SIZE;

    const string SORT_KERNEL_NAME = "Sort";
    const string BATCHERMERGE_KERNEL_NAME = "BatcherMerge";
    const string INDEX_BUFFER_NAME = "Indices";
    const string VALUE_BUFFER_NAME = "Values";
    const string TARGET_VARIABLE_NAME = "Target";
    const string GROUPCOUNT_VARIABLE_NAME = "groupCount";
    const string ISODDDISPATCH_VARIABLE_NAME = "isOddDispatch";

    public uint[] SortedIndices { get => indices; }
    uint[] indices;

    int sortKernelIndex, batcherKernelIndex;

    public virtual void Init(int arrayLength)
    {
        indices = new uint[arrayLength];
        // Populate indices
        for (uint i = 0; i < arrayLength; i++)
        {
            indices[i] = i;
        }

        sortKernelIndex = shader.FindKernel(SORT_KERNEL_NAME);
        batcherKernelIndex = shader.FindKernel(BATCHERMERGE_KERNEL_NAME);

        ReleaseBuffers();
        indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, arrayLength, sizeof(uint));
        valueBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, arrayLength, sizeof(float) * 3);

        indexBuffer.SetData(indices);
    }

    protected uint[] ComputeNonAlloc(ref Vector3[] posArray, Vector3 target)
    {
        if (indices == null)
            throw new Exception("GPUDistanceSort instance not initialized. Make sure to call Init() before.");

        valueBuffer.SetData(posArray);

        // Set buffers for both SORT and MERGE kernels
        shader.SetBuffer(sortKernelIndex, INDEX_BUFFER_NAME, indexBuffer);
        shader.SetBuffer(batcherKernelIndex, INDEX_BUFFER_NAME, indexBuffer);

        shader.SetBuffer(sortKernelIndex, VALUE_BUFFER_NAME, valueBuffer);
        shader.SetBuffer(batcherKernelIndex, VALUE_BUFFER_NAME, valueBuffer);

        shader.SetVector(TARGET_VARIABLE_NAME, target);

        int numThreadGroups = Mathf.CeilToInt((float) indices.Length / SORT_WORK_GROUP_SIZE);

        // SORT
        shader.Dispatch(sortKernelIndex, numThreadGroups, 1, 1);

        // BATCHER MERGE
        bool isOddDispatch = false;
        int passCount = indices.Length / SORT_WORK_GROUP_SIZE;
        numThreadGroups = Mathf.CeilToInt((float)indices.Length / BATCHERMERGE_WORK_GROUP_SIZE);

        shader.SetInt(GROUPCOUNT_VARIABLE_NAME, numThreadGroups);
        for (int i = 0; i < passCount; i++)
        {
            shader.SetBool(ISODDDISPATCH_VARIABLE_NAME, isOddDispatch);
            shader.Dispatch(batcherKernelIndex, numThreadGroups, 1, 1);

            isOddDispatch = !isOddDispatch;
        }

        indexBuffer.GetData(indices);

        return indices;
    }

    void ReleaseBuffers()
    {
        indexBuffer?.Release();
        indexBuffer = null;
        valueBuffer?.Release();
        valueBuffer = null;
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }
}
