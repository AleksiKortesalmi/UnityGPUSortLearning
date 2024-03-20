using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using static BenchmarkResult;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class TransformSortBenchmark : MonoBehaviour
{
    int startArrayLength = 64;
    int benchmarkCount = 15;
    int benchmarkPassCount = 10000;

    [SerializeField]
    GameObject prefab;
    [SerializeField]
    TransformSortUtility sortUtility;
    Transform[] array;
    readonly List<Transform> list = new ();
    Vector3 target = Vector3.zero;
    Stopwatch stopwatch = new Stopwatch();
    BenchmarkResult benchmarkResult = new ();
    int benchmarkCounter = 0;
    int counter = 0;

    private void Start()
    {
        Init();
    }

    private void Init()
    {
        Debug.Log("Filling arrays with transforms, amount: " + startArrayLength);

        // Delete old instantiated objects
        for (int i = 0; i < array?.Length; i++)
        {
            Destroy(array[i].gameObject);
            Destroy(list[i].gameObject);
        }

        array = new Transform[startArrayLength];
        list.Clear();

        sortUtility.Init(array.Length);

        benchmarkResult.BatcherWorkGroupSize = GPUDistanceSort.MIN_ARRAY_LENGTH;
        benchmarkResult.GPUName = SystemInfo.graphicsDeviceName;
        benchmarkResult.CPUName = SystemInfo.processorType;
        benchmarkResult.OS = SystemInfo.operatingSystem;
        benchmarkResult.GraphicsMemorySizeGB = SystemInfo.graphicsMemorySize / 1000;
        benchmarkResult.SystemMemorySizeGB = SystemInfo.systemMemorySize / 1000;
        benchmarkResult.UnityVersion = Application.unityVersion;
        benchmarkResult.Passes = new BenchmarkPass[benchmarkPassCount];
    }

    private void Update()
    {
        if(counter < benchmarkPassCount)
        {
            Debug.Log("Test pass " + startArrayLength +  " " + (counter + 1) + ": ");
            GenerateTestData();
            benchmarkResult.Passes[counter] = Test();

            counter++;
        }
        else if(counter == benchmarkPassCount)
        {
            ShowAverages(ref benchmarkResult);

            SaveResults();

            benchmarkCounter++;

            if (benchmarkCounter < benchmarkCount)
            {
                startArrayLength *= 2;
                benchmarkPassCount = Math.Max(benchmarkPassCount / 2, 10);

                Init();

                counter = 0;
            }
            else if (benchmarkCounter == benchmarkCount)
            {
                Application.Quit();
#if UNITY_EDITOR
                AssetDatabase.Refresh();

                EditorApplication.isPlaying = false;
#endif
            }
        }
    }

    void GenerateTestData()
    {
        Debug.Log("Setting transforms to random positions");
        Vector3 pos;
        if(list.Count == 0)
            for (int i = 0; i < startArrayLength; i++)
            {
                pos = new Vector3(Random.Range(0.0f, 1000f), Random.Range(0.0f, 1000f), Random.Range(0.0f, 1000f));
                array[i] = Instantiate(prefab, pos, Quaternion.identity, transform).GetComponent<Transform>();
                list.Add(Instantiate(prefab, pos, Quaternion.identity, transform).GetComponent<Transform>());
            }
        else
            for (int i = 0; i < startArrayLength; i++)
            {
                pos = new Vector3(Random.Range(0.0f, 1000f), Random.Range(0.0f, 1000f), Random.Range(0.0f, 1000f));
                array[i].position = pos; 
                list[i].position = pos;
            }
    }

    BenchmarkPass Test()
    {
        double gpuTime, cpuTime;

        stopwatch.Reset();

        // Start the timer
        stopwatch.Start();

        sortUtility.SortByDistance(ref array, target);

        // Stop the timer
        stopwatch.Stop();

        // Get the elapsed time
        TimeSpan elapsedTime = stopwatch.Elapsed;
        gpuTime = elapsedTime.TotalMilliseconds;

        Debug.Log(array.Length + " transforms sorted by distance");
        Debug.Log("GPU Execution Time: " + elapsedTime.TotalMilliseconds + " milliseconds");

        stopwatch.Reset();

        // Start the timer
        stopwatch.Start();

        SortCPU();

        // Stop the timer
        stopwatch.Stop();

        // Get the elapsed time
        elapsedTime = stopwatch.Elapsed;
        cpuTime = elapsedTime.TotalMilliseconds;

        Debug.Log("CPU Execution Time: " + elapsedTime.TotalMilliseconds + " milliseconds");

        Debug.Log("Validation:");
        
        // CRASHES!!
        // ShowData();

        BenchmarkPass result = new(counter, gpuTime, cpuTime);

        // Update errors to result
        Validate(ref result);

        return result;
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
        for (int i = 0; i < startArrayLength; i += startArrayLength / 4)
        {
            Debug.Log("i: " + i + ", GPU sorted pos: " + Vector3.Distance(array[i].position, target) + ", CPU sorted pos: " + Vector3.Distance(list[i].position, target));
        }
    }

    void Validate(ref BenchmarkPass result)
    {
        int gpuErrors = 0;
        int cpuErrors = 0;
        List<uint> gpuErrorIndices = new ();
        List<int> cpuErrorIndices = new ();

        for (int i = 0; i < startArrayLength; i++)
        {
            // Rounded because of false errors
            if (i + 1 < startArrayLength && Math.Round(Vector3.Distance(array[i + 1].position, target), 3) < Math.Round(Vector3.Distance(array[i].position, target), 3))
            {
                gpuErrorIndices.Add((uint)i + 1);
                gpuErrors++;
            }

            if (i + 1 < startArrayLength && Math.Round(Vector3.Distance(list[i + 1].position, target), 3) < Math.Round(Vector3.Distance(list[i].position, target), 3))
            {
                cpuErrorIndices.Add(i + 1);
                cpuErrors++;
            }
        }

        Debug.Log(gpuErrors + " gpu errors, indices: " + string.Join(", ", gpuErrorIndices));
        /*for (int i = 0; i < gpuErrorIndices.Count; i++)
        {
            Debug.Log("At index: " + (gpuErrorIndices[i] - 1) + ": " + Vector3.Distance(array[gpuErrorIndices[i] - 1].position, target));
            Debug.Log("At index: " + gpuErrorIndices[i] + ": " + Vector3.Distance(array[gpuErrorIndices[i]].position, target));
        }*/

        Debug.Log(cpuErrors + " cpu errors, indices: " + string.Join(", ", cpuErrorIndices));
        /*for (int i = 0; i < cpuErrorIndices.Count; i++)
        {
            Debug.Log("At index: " + (cpuErrorIndices[i] - 1) + ": " + Vector3.Distance(list[cpuErrorIndices[i] - 1].position, target));
            Debug.Log("At index: " + cpuErrorIndices[i] + ": " + Vector3.Distance(list[cpuErrorIndices[i]].position, target));
        }*/

        result.GPUErrors = gpuErrors;
        result.CPUErrors = cpuErrors;
    }

    void ShowAverages(ref BenchmarkResult result)
    {
        double gpuAverage = 0, cpuAverage = 0, gpuMin = double.MaxValue, gpuMax = double.MinValue, cpuMin = double.MaxValue, cpuMax = double.MinValue;

        double gpuTime, cpuTime;
        for (int i = 0; i < benchmarkResult.Passes.Length; i++)
        {
            gpuTime = benchmarkResult.Passes[i].GPUTime;
            cpuTime = benchmarkResult.Passes[i].CPUTime;
            gpuAverage += gpuTime;
            cpuAverage += cpuTime;

            if (gpuTime < gpuMin) gpuMin = gpuTime;
            if (cpuTime < cpuMin) cpuMin = cpuTime;
            if (gpuTime > gpuMax) gpuMax = gpuTime;
            if (cpuTime > cpuMax) cpuMax = cpuTime;
        }

        gpuAverage /= benchmarkResult.Passes.Length;
        cpuAverage /= benchmarkResult.Passes.Length;

        Debug.Log("Results:");
        Debug.Log("GPU average: " + gpuAverage + " milliseconds, CPU average: " + cpuAverage + " milliseconds");

        result.GPUAverage = gpuAverage;
        result.CPUAverage = cpuAverage;
        result.GPUMin = gpuMin;
        result.CPUMin = cpuMin;
        result.GPUMax = gpuMax;
        result.CPUMax = cpuMax;
    }

    void SaveResults()
    {
        string json = JsonUtility.ToJson(benchmarkResult, true);

        // Specify the file path to save JSON data
        string filePath = Path.Combine(Application.dataPath, "Data/" + DateTime.Now.ToString("s").Replace(":", "-") + "_Benchmark_" + startArrayLength.ToString() + ".json");

        try
        {
            // Write JSON data to a file
            File.WriteAllText(filePath, json);
            Debug.Log("JSON data saved to file: " + filePath);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error: " + ex.Message);
        }
    }
}

[Serializable]
public class BenchmarkResult
{
    public double GPUAverage;
    public double CPUAverage;
    public double GPUMin;
    public double GPUMax;
    public double CPUMin;
    public double CPUMax;
    public int BatcherWorkGroupSize;
    public string GPUName;
    public string CPUName;
    public string OS;
    public int GraphicsMemorySizeGB;
    public int SystemMemorySizeGB;
    public string UnityVersion;

    public BenchmarkPass[] Passes;

    [Serializable]
    public struct BenchmarkPass
    {
        public BenchmarkPass(int index, double gPUTime, double cPUTime)
        {
            Index = index;
            GPUTime = gPUTime;
            CPUTime = cPUTime;
            GPUErrors = default;
            CPUErrors = default;
        }

        public int Index;
        public double GPUTime;
        public double CPUTime;
        public int GPUErrors;
        public int CPUErrors;
    }
}

