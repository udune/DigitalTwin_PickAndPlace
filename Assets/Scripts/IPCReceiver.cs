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
                    
                case "clear_all_errors":
                    if (ErrorManager.Instance != null)
                    {
                        ErrorManager.Instance.ClearAllErrors();
                    }
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

            // source 유효성 검사: 빈 source는 에러 추적 및 해제가 불가능하므로 무시
            if (string.IsNullOrEmpty(errorMsg.source))
            {
                Debug.LogWarning($"[IPC] Received error with empty source. Ignoring. Message: {errorMsg.message}");
                return;
            }

            if (ErrorManager.Instance != null)
            {
                // 문자열을 Enum으로 변환
                ErrorSource source = ParseErrorSource(errorMsg.source);
                ErrorType type = ParseErrorType(errorMsg.errorType);

                // ErrorInfo 구조체에 파싱된 데이터 설정
                // ErrorConditionMonitor와 동일한 패턴으로 고정 ID 사용
                string errorId = $"{errorMsg.source}_{errorMsg.errorType}";

                ErrorInfo info = new ErrorInfo
                {
                    Id = errorId,
                    Type = type,
                    Source = source,
                    Location = errorMsg.source,
                    Message = errorMsg.message,
                    Timestamp = Time.time
                };

                ErrorManager.Instance.RaiseError(info);

                Debug.Log($"[IPC] Error raised: {source} - {type} - {errorMsg.message}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IPC] ParseErrorMessage error: {ex.Message}");
        }
    }
    
    private ErrorSource ParseErrorSource(string source)
    {
        return source switch
        {
            "XAxis" => ErrorSource.XAxis,
            "YAxis" => ErrorSource.YAxis,
            "ZAxis" => ErrorSource.ZAxis,
            _ => ErrorSource.XAxis
        };
    }
    
    private ErrorType ParseErrorType(string type)
    {
        return type switch
        {
            "Error" => ErrorType.Error,
            "Warning" => ErrorType.Warning,
            _ => ErrorType.Error
        };
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
        public string source;        // "XAxis", "YAxis", etc.
        public string errorType;     // "Error", "Warning", etc.
        public string message;
        public string timestamp;
    }
}