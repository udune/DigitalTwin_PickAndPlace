using System;

public enum ErrorType
{
    Warning,
    Error
}

public enum ErrorSource
{
    XAxis,
    YAxis,
    ZAxis
}

[Serializable]
public struct ErrorInfo
{
    public string Id;
    public ErrorType Type;
    public ErrorSource Source;
    public string Location;
    public string Message;
    public float Timestamp;
}