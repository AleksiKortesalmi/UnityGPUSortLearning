using UnityEngine;
using System.Collections;

public class MyLog : MonoBehaviour
{
    static string myLog;
    static Queue myLogQueue = new Queue();
    public string output = "";
    public string stack = "";
    private Vector2 scrollPos;
    public int maxLines = 100;

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        output = logString;
        stack = stackTrace;
        string newString = "\n [" + type + "] : " + output;
        myLogQueue.Enqueue(newString);
        if (type == LogType.Exception)
        {
            newString = "\n" + stackTrace;
            myLogQueue.Enqueue(newString);
        }

        while (myLogQueue.Count > maxLines)
        {
            myLogQueue.Dequeue();
        }

        myLog = string.Empty;
        foreach (string s in myLogQueue)
        {
            myLog += s;
        }
    }

    void OnGUI()
    {
        GUI.TextArea(new Rect(0, 0, Screen.width / 2, Screen.height), myLog);
    }
}