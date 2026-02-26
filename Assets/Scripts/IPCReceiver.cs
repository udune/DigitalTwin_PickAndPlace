using UnityEngine;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

public class IPCReceiver : MonoBehaviour
{
    public PickAndPlaceController controller;
    
    private NamedPipeClientStream _pipeClient;
    private StreamReader _reader;
    private bool _isRunning = false;
    
    async void Start()
    {
        await ConnectToPipe();

        // 연결 성공 시에만 데이터 수신 시작
        if (_isRunning && _reader != null)
        {
            _ = ReceiveData();
        }
    }
    
    private async Task ConnectToPipe()
    {
        try
        {
            _pipeClient = new NamedPipeClientStream(".", "DigitalTwinPipe", 
                PipeDirection.InOut);
            
            Debug.Log("[IPC] Connecting to WPF...");
            await _pipeClient.ConnectAsync();
            Debug.Log("[IPC] Connected to WPF!");
            
            _reader = new StreamReader(_pipeClient);
            _isRunning = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IPC] Connection failed: {ex.Message}");
        }
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
            "Gripper" => ErrorSource.Gripper,
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
        _isRunning = false;

        try
        {
            _reader?.Dispose();
            _pipeClient?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[IPC] Cleanup error: {ex.Message}");
        }

        _reader = null;
        _pipeClient = null;
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