using UnityEngine;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected
}

public class IPCReceiver : MonoBehaviour
{
    // Windows API for pipe connection check
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PeekNamedPipe(
        SafePipeHandle hNamedPipe,
        byte[] lpBuffer,
        uint nBufferSize,
        IntPtr lpBytesRead,
        out uint lpTotalBytesAvail,
        IntPtr lpBytesLeftThisMessage);

    public static IPCReceiver Instance { get; private set; }

    public PickAndPlaceController controller;
    public GripperController gripper;

    [Header("Connection Settings")]
    [Tooltip("재연결 시도 간격 (초)")]
    public float reconnectInterval = 3f;

    [Tooltip("최대 재연결 시도 횟수 (0 = 무제한)")]
    public int maxReconnectAttempts = 0;

    [Tooltip("연결 상태 확인 간격 (초)")]
    public float connectionCheckInterval = 0.5f;

    private NamedPipeClientStream _pipeClient;
    private StreamReader _reader;
    private StreamWriter _writer;
    private bool _isRunning = false;
    private CancellationTokenSource _cancellationTokenSource;
    private int _reconnectAttempts = 0;

    // 현재 축 위치 (mm 단위) - AxisMover와 동기화용
    public float CurrentX { get; private set; }
    public float CurrentY { get; private set; }
    public float CurrentZ { get; private set; }

    // 축 데이터 수신 이벤트
    public event Action<float, float, float> OnAxisDataReceived;

    // 연결 상태
    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;
    public event Action<ConnectionStatus> ConnectionStatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    async void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        await ConnectWithRetry();
    }

    private async Task ConnectWithRetry()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            if (await ConnectToPipe())
            {
                _reconnectAttempts = 0;
                await ReceiveData();

                // 연결이 끊어진 경우 재연결 도
                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Debug.Log("[IPC] Connection lost. Attempting to reconnect...");
                    SetConnectionStatus(ConnectionStatus.Disconnected);
                }
            }

            // 최대 재연결 횟수 체크
            _reconnectAttempts++;
            if (maxReconnectAttempts > 0 && _reconnectAttempts >= maxReconnectAttempts)
            {
                Debug.LogError($"[IPC] Max reconnect attempts ({maxReconnectAttempts}) reached.");
                break;
            }

            // 재연결 대기
            if (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                Debug.Log($"[IPC] Reconnecting in {reconnectInterval} seconds... (Attempt {_reconnectAttempts})");
                await Task.Delay((int)(reconnectInterval * 1000), _cancellationTokenSource.Token).ContinueWith(t => { });
            }
        }
    }

    private async Task<bool> ConnectToPipe()
    {
        try
        {
            CleanupConnection();

            _pipeClient = new NamedPipeClientStream(".", "DigitalTwinPipe",
                PipeDirection.InOut);

            SetConnectionStatus(ConnectionStatus.Connecting);
            Debug.Log("[IPC] Connecting to WPF...");

            // 타임아웃 설정 (5초)
            using var timeoutCts = new CancellationTokenSource(5000);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationTokenSource.Token, timeoutCts.Token);

            await _pipeClient.ConnectAsync(linkedCts.Token);

            Debug.Log("[IPC] Connected to WPF!");
            _reader = new StreamReader(_pipeClient);
            _writer = new StreamWriter(_pipeClient) { AutoFlush = true };
            _isRunning = true;
            SetConnectionStatus(ConnectionStatus.Connected);

            // 연결 상태 모니터링 시작
            _ = MonitorConnection();

            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[IPC] Connection attempt cancelled or timed out.");
            SetConnectionStatus(ConnectionStatus.Disconnected);
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IPC] Connection failed: {ex.Message}");
            SetConnectionStatus(ConnectionStatus.Disconnected);
            return false;
        }
    }

    private void SetConnectionStatus(ConnectionStatus status)
    {
        if (Status != status)
        {
            Status = status;
            UnityMainThreadDispatcher.Enqueue(() => ConnectionStatusChanged?.Invoke(status));
        }
    }

    private void CleanupConnection()
    {
        _isRunning = false;

        try
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _pipeClient?.Dispose();
        }
        catch { }

        _reader = null;
        _writer = null;
        _pipeClient = null;
    }
    
    private async Task ReceiveData()
    {
        while (_isRunning && _reader != null)
        {
            try
            {
                string json = await _reader.ReadLineAsync();

                // 연결이 끊어진 경우 (null 반환)
                if (json == null)
                {
                    Debug.LogWarning("[IPC] Connection closed by server.");
                    break;
                }

                if (string.IsNullOrEmpty(json)) continue;

                // Unity Main Thread에서 실행
                UnityMainThreadDispatcher.Enqueue(() => ProcessMessage(json));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IPC] Receive error: {ex.Message}");
                break;
            }
        }

        _isRunning = false;
    }

    /// <summary>
    /// 연결 상태를 주기적으로 모니터링하여 끊김을 감지합니다.
    /// </summary>
    private async Task MonitorConnection()
    {
        int checkIntervalMs = (int)(connectionCheckInterval * 1000);

        while (_isRunning && _pipeClient != null)
        {
            await Task.Delay(checkIntervalMs);

            if (!_isRunning || _pipeClient == null)
                break;

            try
            {
                // 파이프가 연결되어 있는지 확인
                // IsConnected는 마지막 I/O 결과 기반이므로, 0바이트 쓰기로 실제 상태 확인
                if (!_pipeClient.IsConnected || !IsConnectionAlive())
                {
                    Debug.LogWarning("[IPC] Connection lost detected by monitor.");
                    _isRunning = false;
                    break;
                }
            }
            catch (Exception)
            {
                Debug.LogWarning("[IPC] Connection check failed - pipe disconnected.");
                _isRunning = false;
                break;
            }
        }
    }

    /// <summary>
    /// 파이프 연결이 실제로 살아있는지 확인합니다.
    /// PeekNamedPipe API를 사용하여 I/O 없이도 연결 상태를 정확히 감지합니다.
    /// </summary>
    private bool IsConnectionAlive()
    {
        if (_pipeClient == null || !_pipeClient.IsConnected)
            return false;

        try
        {
            // PeekNamedPipe는 파이프가 끊어졌으면 false를 반환합니다.
            // 이 방법은 실제 데이터를 읽지 않고도 연결 상태를 확인할 수 있습니다.
            bool result = PeekNamedPipe(
                _pipeClient.SafePipeHandle,
                null,
                0,
                IntPtr.Zero,
                out _,
                IntPtr.Zero);

            return result;
        }
        catch
        {
            return false;
        }
    }
    
    private void ProcessMessage(string json)
    {
        try
        {
            var message = JsonUtility.FromJson<IPCMessage>(json);
            
            switch (message.type)
            {
                case "axis_data":
                    ParseAxisData(json);
                    break;
                    
                case "error":
                    ParseErrorMessage(json);
                    break;

                case "error_clear":
                    ParseErrorClear(json);
                    break;

                case "clear_all_errors":
                    if (ErrorManager.Instance != null)
                    {
                        ErrorManager.Instance.ClearAllErrors();
                    }
                    break;

                case "gripper_command":
                    ParseGripperCommand(json);
                    break;

                default:
                    Debug.LogWarning($"[IPC] Unknown message type: {message.type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IPC] Process error: {ex.Message}\nJSON: {json}");
        }
    }
    
    private void ParseAxisData(string json)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<AxisDataWrapper>(json);

            if (wrapper.data != null)
            {
                // 현재 위치 저장
                CurrentX = wrapper.data.x;
                CurrentY = wrapper.data.y;
                CurrentZ = wrapper.data.z;

                // 컨트롤러에 전달
                if (controller != null)
                {
                    controller.MoveToPosition(CurrentX, CurrentY, CurrentZ);
                }

                // 이벤트 발생 (AxisMover 동기화용)
                OnAxisDataReceived?.Invoke(CurrentX, CurrentY, CurrentZ);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IPC] ParseAxisData error: {ex.Message}");
        }
    }

    /// <summary>
    /// Unity에서 WPF로 축 데이터 전송
    /// </summary>
    public void SendAxisData(float x, float y, float z)
    {
        if (Status != ConnectionStatus.Connected || _writer == null)
        {
            return;
        }

        try
        {
            CurrentX = x;
            CurrentY = y;
            CurrentZ = z;

            var data = new AxisDataWrapper
            {
                type = "axis_data",
                data = new AxisData
                {
                    x = x,
                    y = y,
                    z = z,
                    timestamp = DateTime.Now.ToString("o")
                }
            };

            string json = JsonUtility.ToJson(data);
            _writer.WriteLine(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IPC] SendAxisData error: {ex.Message}");
        }
    }
    
    private void ParseErrorMessage(string json)
    {
        try
        {
            var errorMsg = JsonUtility.FromJson<ErrorMessage>(json);

            // code는 Dashboard가 정하는 오류 식별자이자 error_clear가 지목할 키다.
            // 없으면 해제할 방법이 없으므로 받지 않는다.
            if (string.IsNullOrEmpty(errorMsg.code))
            {
                Debug.LogWarning($"[IPC] Received error without code. Ignoring. Message: {errorMsg.message}");
                return;
            }

            // source 유효성 검사: 빈 source는 에러 추적 및 해제가 불가능하므로 무시
            if (string.IsNullOrEmpty(errorMsg.source))
            {
                Debug.LogWarning($"[IPC] Received error with empty source. Ignoring. Code: {errorMsg.code}");
                return;
            }

            // 모르는 값을 기본값으로 삼키면 엉뚱한 축에 오류가 붙는다. 반드시 거르고 알린다.
            if (!TryParseErrorSource(errorMsg.source, out ErrorSource source))
            {
                Debug.LogWarning($"[IPC] Unknown error source '{errorMsg.source}'. Ignoring. Code: {errorMsg.code}");
                return;
            }

            if (!TryParseErrorType(errorMsg.errorType, out ErrorType type))
            {
                Debug.LogWarning($"[IPC] Unknown error type '{errorMsg.errorType}'. Ignoring. Code: {errorMsg.code}");
                return;
            }

            if (ErrorManager.Instance != null)
            {
                ErrorInfo info = new ErrorInfo
                {
                    // Dashboard의 code를 그대로 Id로 쓴다. 같은 사건에 양쪽이 각각 Id를
                    // 만들면 오류 하나가 목록에 두 줄로 남는다.
                    Id = errorMsg.code,
                    Type = type,
                    Source = source,
                    Location = errorMsg.source,
                    Message = errorMsg.message,
                    Timestamp = Time.time
                };

                ErrorManager.Instance.RaiseError(info);

                Debug.Log($"[IPC] Error raised: {errorMsg.code} - {source} - {type} - {errorMsg.message}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IPC] ParseErrorMessage error: {ex.Message}");
        }
    }

    /// <summary>
    /// Dashboard가 조건 해소를 알려오면 해당 오류 하나만 거둔다.
    /// </summary>
    private void ParseErrorClear(string json)
    {
        try
        {
            var clearMsg = JsonUtility.FromJson<ErrorClearMessage>(json);

            if (string.IsNullOrEmpty(clearMsg.code))
            {
                Debug.LogWarning("[IPC] Received error_clear without code. Ignoring.");
                return;
            }

            if (ErrorManager.Instance != null)
            {
                ErrorManager.Instance.ClearError(clearMsg.code);
                Debug.Log($"[IPC] Error cleared: {clearMsg.code}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IPC] ParseErrorClear error: {ex.Message}");
        }
    }

    private bool TryParseErrorSource(string source, out ErrorSource result)
    {
        switch (source)
        {
            case "XAxis": result = ErrorSource.XAxis; return true;
            case "YAxis": result = ErrorSource.YAxis; return true;
            case "ZAxis": result = ErrorSource.ZAxis; return true;
            case "System": result = ErrorSource.System; return true;
            default: result = default; return false;
        }
    }

    private bool TryParseErrorType(string type, out ErrorType result)
    {
        switch (type)
        {
            case "Error": result = ErrorType.Error; return true;
            case "Warning": result = ErrorType.Warning; return true;
            default: result = default; return false;
        }
    }

    /// <summary>
    /// WPF로부터 그리퍼 명령 수신 및 처리
    /// </summary>
    private void ParseGripperCommand(string json)
    {
        try
        {
            var gripperMsg = JsonUtility.FromJson<GripperCommandMessage>(json);

            if (gripper == null)
            {
                Debug.LogError("[IPC] GripperController not assigned!");
                return;
            }

            bool success = false;

            switch (gripperMsg.command)
            {
                case "pick":
                    success = gripper.Pick();
                    Debug.Log($"[IPC] Pick command received - Success: {success}");
                    break;

                case "place":
                    success = gripper.Place();
                    Debug.Log($"[IPC] Place command received - Success: {success}");
                    break;

                default:
                    Debug.LogWarning($"[IPC] Unknown gripper command: {gripperMsg.command}");
                    break;
            }

            // 결과를 WPF로 전송
            if (success)
            {
                SendGripperStatus(gripper);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IPC] ParseGripperCommand error: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 그리퍼 상태를 WPF로 전송
    /// </summary>
    public void SendGripperStatus(GripperController gripperController)
    {
        if (gripperController == null || _writer == null || !_isRunning)
            return;

        try
        {
            var status = new GripperStatusMessage
            {
                type = "gripper_status",
                isGripping = gripperController.IsGripping,
                objectName = gripperController.GrippedObject != null
                    ? gripperController.GrippedObject.name
                    : "",
                timestamp = DateTime.Now.ToString("o")
            };

            string json = JsonUtility.ToJson(status);
            _writer.WriteLine(json);

            Debug.Log($"[IPC] Gripper status sent: IsGripping={status.isGripping}, Object={status.objectName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IPC] SendGripperStatus error: {ex.Message}");
        }
    }

    void OnDestroy()
    {
        _cancellationTokenSource?.Cancel();
        CleanupConnection();
        _cancellationTokenSource?.Dispose();
    }

    /// <summary>
    /// 수동으로 재연결 시도
    /// </summary>
    public void Reconnect()
    {
        if (Status != ConnectionStatus.Connecting)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _reconnectAttempts = 0;
            _ = ConnectWithRetry();
        }
    }
    
    // ===== 데이터 클래스 =====

    [Serializable]
    private class IPCMessage
    {
        public string type;
    }

    [Serializable]
    public class AxisDataWrapper
    {
        public string type;
        public AxisData data;
    }

    [Serializable]
    public class AxisData
    {
        public float x;
        public float y;
        public float z;
        public float velocityX;
        public float velocityY;
        public float velocityZ;
        public string timestamp;
    }
    
    [Serializable]
    private class ErrorMessage
    {
        public string type;
        public string code;          // "X_LIMIT" 등 — 오류 식별자(ErrorInfo.Id)
        public string source;        // "XAxis", "YAxis", "ZAxis", "System"
        public string errorType;     // "Error", "Warning"
        public string message;
        public string timestamp;
    }

    [Serializable]
    private class ErrorClearMessage
    {
        public string type;          // "error_clear"
        public string code;          // 해제할 오류의 code
        public string timestamp;
    }

    // ===== 그리퍼 관련 데이터 클래스 =====

    [Serializable]
    private class GripperCommandMessage
    {
        public string type;      // "gripper_command"
        public string command;   // "pick" or "place"
        public string timestamp;
    }

    [Serializable]
    private class GripperStatusMessage
    {
        public string type;        // "gripper_status"
        public bool isGripping;    // 그리퍼 상태
        public string objectName;  // 잡고 있는 물체 이름
        public string timestamp;
    }
}