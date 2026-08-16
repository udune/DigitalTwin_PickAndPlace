using UnityEngine;

/// <summary>
/// 단독 실행용 로컬 오류 판정.
/// 오류 판정의 단일 기준은 Dashboard이므로, IPC가 연결되면 이 판정은 스스로 멈춘다.
/// Dashboard 없이 Unity만 재생할 때 오류 시각화를 확인하기 위한 대체 수단이다.
/// </summary>
public class ErrorConditionMonitor : MonoBehaviour
{
    // 이 판정이 만든 오류들. 판정을 멈출 때 자기가 남긴 것만 정확히 거두기 위해 축 이름을 고정해 둔다.
    private static readonly string[] LocalAxisIds = { "XAxis", "YAxis", "ZAxis" };

    [Header("Axis References")]
    public Transform xAxis;
    public Transform yAxis;
    public Transform zAxis;

    [Header("Axis Limits")]
    [Tooltip("단독 실행 시에만 쓰이는 로컬 경계 (Unity 단위, 1 unit = 100mm). " +
             "Dashboard 연결 시에는 appsettings.json의 Alarm* 값이 유일한 기준이다.")]
    public float xMin = -5f;
    public float xMax = 5f;
    public float yMin = -5f;
    public float yMax = 5f;
    public float zMin = -5f;
    public float zMax = 5f;

    [Header("Settings")]
    [Range(0f, 1f)]
    public float warningThreshold = 0.85f;

    [Tooltip("Dashboard(IPC)가 연결되면 로컬 판정을 멈춘다. 끄면 같은 사건이 두 번 판정된다.")]
    public bool disableWhenDashboardConnected = true;

    // 로컬 판정을 멈춘 상태인지. 연결/해제 전환 시점을 한 번만 처리하기 위해 들고 있는다.
    private bool _suspended;

    private void Update()
    {
        if (ErrorManager.Instance == null)
        {
            return;
        }

        if (DashboardOwnsJudgement())
        {
            if (!_suspended)
            {
                // 연결 직전까지 로컬 판정이 남긴 오류를 거둔다.
                // 안 거두면 Dashboard 판정과 겹쳐 같은 문제가 두 줄로 남는다.
                ClearLocalErrors();
                _suspended = true;
            }

            return;
        }

        if (_suspended)
        {
            // 연결이 끊겼다. Dashboard가 보낸 오류는 이제 갱신도 해제도 되지 않는 낡은 상태이므로
            // 전부 거두고, 아래 로컬 판정이 실제로 문제가 있으면 곧바로 다시 올린다.
            ErrorManager.Instance.ClearAllErrors();
            _suspended = false;
        }

        CheckAxis(xAxis.localPosition.x, xMin, xMax, ErrorSource.XAxis, "XAxis");
        CheckAxis(yAxis.localPosition.z, yMin, yMax, ErrorSource.YAxis, "YAxis");
        CheckAxis(zAxis.localPosition.y, zMin, zMax, ErrorSource.ZAxis, "ZAxis");
    }

    private bool DashboardOwnsJudgement()
    {
        return disableWhenDashboardConnected
               && IPCReceiver.Instance != null
               && IPCReceiver.Instance.Status == ConnectionStatus.Connected;
    }

    private void ClearLocalErrors()
    {
        foreach (string id in LocalAxisIds)
        {
            ErrorManager.Instance.ClearError($"{id}_Limit");
            ErrorManager.Instance.ClearError($"{id}_Warning");
        }
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
        else
        {
            ErrorManager.Instance.ClearError(errorId);
            ErrorManager.Instance.ClearError(warningId);
        }
    }
}
