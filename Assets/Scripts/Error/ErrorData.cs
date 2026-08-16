using System;

public enum ErrorType
{
    Warning,
    Error
}

// 새 값은 반드시 끝에 추가한다. 씬/프리팹에 정수로 직렬화되므로 순서를 바꾸면 기존 설정이 어긋난다.
public enum ErrorSource
{
    XAxis,
    YAxis,
    ZAxis,

    // 특정 축에 속하지 않는 오류(Dashboard의 설정 점검 등).
    // 대응하는 Transform이 없으므로 ErrorVisualizer는 건너뛰고 오류 패널에만 표시된다.
    System
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