using UnityEngine;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected
}

public class IPCReceiver : MonoBehaviour
{
    public static IPCReceiver Instance { get; private set; }

    public PickAndPlaceController controller;

    [Header("Connection Settings")]
    [Tooltip("재연결 시도 간격 (초)")]
    public float reconnectInterval = 3f;

    [Tooltip("최대 재연결 시도 횟수 (0 = 무제한)")]
    public int maxReconnectAttempts = 0;

    private NamedPipeClientStream _pipeClient;
    private StreamReader _reader;
    private bool _isRunning = false;
    private CancellationTokenSource _cancellationTokenSource;
    private int _reconnectAttempts = 0;

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
            _isRunning = true;
            SetConnectionStatus(ConnectionStatus.Connected);

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
            _pipeClient?.Dispose();
        }
        catch { }

        _reader = null;
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
            
            if (controller != null && wrapper.data != null)
            {
                controller.MoveToPosition(
                    wrapper.data.x,
                    wrapper.data.y,
                    wrapper.data.z
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IPC] ParseAxisData error: {ex.Message}");
        }
    }
    
    private void ParseErrorMessage(string json)
    {
        try
        {
            var errorMsg = JsonUtility.FromJson<ErrorMessage>(json);

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
    private class AxisDataWrapper
    {
        public string type;
        public AxisData data;
    }
    
    [Serializable]
    private class AxisData
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