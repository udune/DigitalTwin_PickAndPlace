using UnityEngine;

public class ErrorConditionMonitor : MonoBehaviour
{
    [Header("Axis References")]
    public Transform xAxis;
    public Transform yAxis;
    public Transform zAxis;

    [Header("Axis Limits")]
    public float xMin = -5f;
    public float xMax = 5f;
    public float yMin = -5f;
    public float yMax = 5f;
    public float zMin = -5f;
    public float zMax = 5f;

    [Header("Settings")]
    [Range(0f, 1f)]
    public float warningThreshold = 0.85f;

    private void Update()
    {
        if (ErrorManager.Instance == null)
        {
            return;
        }

        CheckAxis(xAxis.localPosition.x, xMin, xMax, ErrorSource.XAxis, "XAxis");
        CheckAxis(yAxis.localPosition.z, yMin, yMax, ErrorSource.YAxis, "YAxis");
        CheckAxis(zAxis.localPosition.y, zMin, zMax, ErrorSource.ZAxis, "ZAxis");
    }

    private void CheckAxis(float value, float min, float max, ErrorSource source, string id)
    {
        float range = max - min;
        float warningMargin = range * (1f - warningThreshold) * 0.5f;

        string errorId = $"{id}_Limit";
        string warningId = $"{id}_Warning";

        // 상태 판별: Error > Warning > Normal
        bool isError = value < min || value > max;
        bool isWarning = !isError && (value < min + warningMargin || value > max - warningMargin);
        bool isNormal = !isError && !isWarning;

        // Error 상태
        if (isError)
        {
            ErrorManager.Instance.RaiseError(new ErrorInfo
            {
                Id = errorId,
                Type = ErrorType.Error,
                Source = source,
                Location = id,
                Message = $"Limit Exceeded: {value:F2}",
                Timestamp = Time.time
            });
            ErrorManager.Instance.ClearError(warningId);
        }
        // Warning 상태
        else if (isWarning)
        {
            ErrorManager.Instance.RaiseError(new ErrorInfo
            {
                Id = warningId,
                Type = ErrorType.Warning,
                Source = source,
                Location = id,
                Message = $"Approaching Limit: {value:F2}",
                Timestamp = Time.time
            });
            ErrorManager.Instance.ClearError(errorId);
        }
        // Normal 상태
        else if (isNormal)
        {
            ErrorManager.Instance.ClearError(errorId);
            ErrorManager.Instance.ClearError(warningId);
        }
    }
}
