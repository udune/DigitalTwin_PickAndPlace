using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ErrorManager : MonoBehaviour
{
    public static ErrorManager Instance { get; private set; }

    private Dictionary<string, ErrorInfo> errorDict = new Dictionary<string, ErrorInfo>();

    public event Action<ErrorInfo> ErrorRaisedAction;
    public event Action<string> ErrorClearedAction;
    public event Action AllClearedAction;

    public bool HasErrors => errorDict.Values.Any(errorInfo => errorInfo.Type == ErrorType.Error);
    public bool HasWarnings => errorDict.Values.Any(errorInfo => errorInfo.Type == ErrorType.Warning);
    public List<ErrorInfo> ErrorInfoList => errorDict.Values.ToList();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }

    public void RaiseError(ErrorInfo errorInfo)
    {
        if (errorDict.ContainsKey(errorInfo.Id))
        {
            return;
        }

        errorDict[errorInfo.Id] = errorInfo;
        ErrorRaisedAction?.Invoke(errorInfo);
    }

    public void ClearError(string id)
    {
        if (errorDict.ContainsKey(id))
        {
            errorDict.Remove(id);
            ErrorClearedAction?.Invoke(id);

            if (errorDict.Count == 0)
            {
                AllClearedAction?.Invoke();
            }
        }
    }

    public void ClearAllErrors()
    {
        foreach (string id in errorDict.Keys.ToList())
        {
            ClearError(id);
        }
    }
}