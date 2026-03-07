using UnityEngine;

public class ErrorConditionMonitor : MonoBehaviour
{
    [Header("Axis References")]
    public Transform xAxis;
    public Transform yAxis;
    public Transform zAxis;
    public Transform gripper;

    [Header("Axis Limits")]
    public float xMin = -5f;
    public float xMax = 5f;
    public float yMin = -5f;
    public float yMax = 5f;
    public float zMin = -5f;
    public float zMax = 5f;

    [Header("Gripper Settings")]
    [Tooltip("Gripper 상태 모니터링 활성화")]
    public bool monitorGripper = true;

    [Tooltip("Gripper 최소 높이 (충돌 방지)")]
    public float gripperMinHeight = 0.1f;

    [Header("Settings")]
    [Range(0f, 1f)]
    public float warningThreshold = 0.85f;
    private const float Hysteresis = 0.02f;

    private void Update()
    {
        if (ErrorManager.Instance == null)
        {
            return;
        }

        CheckAxis(xAxis, xAxis.localPosition.x, xMin, xMax, ErrorSource.XAxis, "XAxis", "X-AXIS");
        CheckAxis(yAxis, yAxis.localPosition.z, yMin, yMax, ErrorSource.YAxis, "YAxis", "Y-AXIS");
        CheckAxis(zAxis, zAxis.localPosition.y, zMin, zMax, ErrorSource.ZAxis, "ZAxis", "Z-AXIS");

        if (monitorGripper)
        {
            CheckGripper();
        }
    }

    private void CheckGripper()
    {
        if (gripper == null)
        {
            return;
        }

        // Gripper의 월드 좌표 기준 높이 체크
        float gripperHeight = gripper.position.y;
        string errorId = "Gripper_Height";
        string warningId = "Gripper_Height_Warning";

        // 최소 높이 이하로 내려갔을 때 Error
        if (gripperHeight < gripperMinHeight)
        {
            ErrorManager.Instance.RaiseError(new ErrorInfo
            {
                Id = errorId,
                Type = ErrorType.Error,
                Source = ErrorSource.Gripper,
                Location = "GRIPPER",
                Message = $"Height too low: {gripperHeight:F2}m (Min: {gripperMinHeight:F2}m)",
                Timestamp = Time.time
            });

            ErrorManager.Instance.ClearError(warningId);
        }
        else
        {
            // 안전 높이 복귀 시 Error 해제
            if (gripperHeight > gripperMinHeight + Hysteresis)
            {
                ErrorManager.Instance.ClearError(errorId);
            }
        }

        // Warning 영역 체크 (최소 높이의 1.5배 이하)
        float warningHeight = gripperMinHeight * 1.5f;
        bool isInWarningZone = gripperHeight < warningHeight && gripperHeight >= gripperMinHeight;

        if (isInWarningZone && !ErrorManager.Instance.ErrorInfoList.Exists(e => e.Id == errorId))
        {
            ErrorManager.Instance.RaiseError(new ErrorInfo
            {
                Id = warningId,
                Type = ErrorType.Warning,
                Source = ErrorSource.Gripper,
                Location = "GRIPPER",
                Message = $"Approaching minimum height: {gripperHeight:F2}m",
                Timestamp = Time.time
            });
        }
        else if (gripperHeight > warningHeight + Hysteresis)
        {
            ErrorManager.Instance.ClearError(warningId);
        }
    }

    private void CheckAxis(Transform axis, float currentValue, float min, float max, ErrorSource source, string idPrefix, string locationName)
    {
        if (axis == null)
        {
            return;
        }

        float range = max - min;
        float warningMargin = range * (1.0f - warningThreshold) * 0.5f;

        bool isErrorMin = currentValue < min;
        bool isErrorMax = currentValue > max;
        bool isWarningMin = currentValue < (min + warningMargin) && !isErrorMin;
        bool isWarningMax = currentValue > (max - warningMargin) && !isErrorMax;

        string errorId = $"{idPrefix}_Limit";
        string warningId = $"{idPrefix}_Warning";

        if (isErrorMin || isErrorMax)
        {
            ErrorManager.Instance.RaiseError(new ErrorInfo
            {
                Id = errorId,
                Type = ErrorType.Error,
                Source = source,
                Location = locationName,
                Message = $"Limit Exceeded: {currentValue:F2}",
                Timestamp = Time.time
            });

            ErrorManager.Instance.ClearError(warningId);
        }
        else
        {
            if (currentValue > min + Hysteresis && currentValue < max - Hysteresis)
            {
                ErrorManager.Instance.ClearError(errorId);
            }
        }

        if ((isWarningMin || isWarningMax) && !ErrorManager.Instance.ErrorInfoList.Exists(errorInfo => errorInfo.Id == errorId))
        {
            ErrorManager.Instance.RaiseError(new ErrorInfo
            {
                Id = warningId,
                Type = ErrorType.Warning,
                Source = source,
                Location = locationName,
                Message = $"Approaching Limit: {currentValue:F2}",
                Timestamp = Time.time
            });
        }
        else
        {
            bool clearMin = currentValue > (min + warningMargin + Hysteresis);
            bool clearMax = currentValue < (max - warningMargin - Hysteresis);

            if (clearMin && clearMax)
            {
                ErrorManager.Instance.ClearError(warningId);
            }
        }
    }
}