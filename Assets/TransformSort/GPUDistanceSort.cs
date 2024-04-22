using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class GPUDistanceSort : MonoBehaviour
{
    [SerializeField] ComputeShader shader;
    GraphicsBuffer indicesBuffer;
    GraphicsBuffer distancesBuffer;
    GraphicsBuffer positionsBuffer;

    public const int MIN_ARRAY_LENGTH = BATCHERMERGE_WORK_GROUP_SIZE;

    const int CALC_THREAD_GROUP_SIZE = 1024;
    const int SORT_THREAD_GROUP_SIZE = 16;
    const int SORT_WORK_GROUP_SIZE = 2 * SORT_THREAD_GROUP_SIZE;
    const int BATCHERMERGE_WORK_GROUP_SIZE = 2 * SORT_WORK_GROUP_SIZE;

    const string CALC_IND_KERNEL_NAME = "CalcIndices";
    const string CALC_DIST_KERNEL_NAME = "CalcDistances";
    const string SORT_KERNEL_NAME = "Sort";
    const string BATCHERMERGE_KERNEL_NAME = "BatcherMerge";
    const string INDEX_BUFFER_NAME = "Indices";
    const string DIST_BUFFER_NAME = "Distances";
    const string POS_BUFFER_NAME = "Positions";
    const string TARGET_VARIABLE_NAME = "Target";
    const string GROUPCOUNT_VARIABLE_NAME = "groupCount";
    const string ISODDDISPATCH_VARIABLE_NAME = "isOddDispatch";

    public uint[] SortedIndices { get => Indices; }

    uint[] Indices;
    uint[] Distances;
    Vector3[] Positions;
    readonly Vector3 target = Vector3.zero;

    int sortKernelIndex, batcherKernelIndex, calcDistKernelIndex, calcIndKernelIndex;

    public virtual void Init(int arrayLength)
    {
        Indices = new uint[arrayLength];
        Distances = new uint[arrayLength];
        Positions = new Vector3[arrayLength];

        calcIndKernelIndex = shader.FindKernel(CALC_IND_KERNEL_NAME);
        calcDistKernelIndex = shader.FindKernel(CALC_DIST_KERNEL_NAME);
        sortKernelIndex = shader.FindKernel(SORT_KERNEL_NAME);
        batcherKernelIndex = shader.FindKernel(BATCHERMERGE_KERNEL_NAME);

        ReleaseBuffers();
        indicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, Indices.Length, sizeof(uint));
        distancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, Distances.Length, sizeof(uint));
        positionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, Positions.Length, sizeof(float) * 3);

        int numThreadGroups = Mathf.CeilToInt((float)Indices.Length / CALC_THREAD_GROUP_SIZE);

        // Set buffers for all kernels
        shader.SetBuffer(calcIndKernelIndex, INDEX_BUFFER_NAME, indicesBuffer);

        shader.SetVector(TARGET_VARIABLE_NAME, target);
        shader.SetBuffer(calcDistKernelIndex, POS_BUFFER_NAME, positionsBuffer);
        shader.SetBuffer(calcDistKernelIndex, INDEX_BUFFER_NAME, indicesBuffer);
        shader.SetBuffer(calcDistKernelIndex, DIST_BUFFER_NAME, distancesBuffer);

        shader.SetBuffer(sortKernelIndex, INDEX_BUFFER_NAME, indicesBuffer);
        shader.SetBuffer(sortKernelIndex, DIST_BUFFER_NAME, distancesBuffer);

        shader.SetBuffer(batcherKernelIndex, INDEX_BUFFER_NAME, indicesBuffer);
        shader.SetBuffer(batcherKernelIndex, DIST_BUFFER_NAME, distancesBuffer);

        // INITIALIZE INDICES
        shader.Dispatch(calcIndKernelIndex, numThreadGroups, 1, 1);
    }

    protected uint[] ComputeNonAlloc(ref Vector3[] posArray, Vector3 target)
    {
        positionsBuffer.SetData(posArray);

        shader.SetVector(TARGET_VARIABLE_NAME, target);

        int numThreadGroups = Mathf.CeilToInt((float) Indices.Length / CALC_THREAD_GROUP_SIZE);

        // CALCULATE DISTANCES
        shader.Dispatch(calcDistKernelIndex, numThreadGroups, 1, 1);

        numThreadGroups = Mathf.CeilToInt((float)Indices.Length / SORT_WORK_GROUP_SIZE);

        // SORT
        shader.Dispatch(sortKernelIndex, numThreadGroups, 1, 1);

        // BATCHER MERGE
        bool isOddDispatch = false;
        int passCount = Indices.Length / SORT_WORK_GROUP_SIZE;
        numThreadGroups = Mathf.CeilToInt((float)Indices.Length / BATCHERMERGE_WORK_GROUP_SIZE);

        shader.SetInt(GROUPCOUNT_VARIABLE_NAME, numThreadGroups);
        for (int i = 0; i < passCount; i++)
        {
            shader.SetBool(ISODDDISPATCH_VARIABLE_NAME, isOddDispatch);
            shader.Dispatch(batcherKernelIndex, numThreadGroups, 1, 1);

            isOddDispatch = !isOddDispatch;
        }

        indicesBuffer.GetData(Indices);

        return Indices;
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

    void ReleaseBuffers()
    {
        indicesBuffer?.Release();
        indicesBuffer = null;
        distancesBuffer?.Release();
        distancesBuffer = null;
        positionsBuffer?.Release();
        positionsBuffer = null;
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }
}
