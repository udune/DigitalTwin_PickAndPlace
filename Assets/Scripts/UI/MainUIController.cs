using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UIToolkit 기반 메인 UI 컨트롤러
/// UIDocument에서 VisualElement를 가져와 하위 컨트롤러에 전달
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainUIController : MonoBehaviour
{
    [Header("UI Templates")]
    [Tooltip("에러 아이템 템플릿 (ErrorItem.uxml)")]
    public VisualTreeAsset errorItemTemplate;

    // UI 요소 참조
    private UIDocument _uiDocument;
    private VisualElement _root;

    // 하위 컨트롤러
    private StatusBarController _statusBarController;
    private ErrorPanelController _errorPanelController;
    private ConnectionStatusController _connectionStatusController;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        _root = _uiDocument.rootVisualElement;

        InitializeControllers();
    }

    private void Start()
    {
        // Start()는 모든 GameObject의 Awake()가 완료된 후 호출됨
        // ErrorManager.Instance가 확실히 초기화된 상태에서 구독
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        CleanupControllers();
    }

    private void InitializeControllers()
    {
        // StatusBar 초기화
        var statusBar = _root.Q<VisualElement>("status-bar");
        var statusText = _root.Q<Label>("status-text");
        _statusBarController = new StatusBarController(statusBar, statusText);

        // ErrorPanel 초기화
        var errorPanel = _root.Q<VisualElement>("error-panel");
        var errorContainer = _root.Q<VisualElement>("error-container");
        var clearHistoryButton = _root.Q<Button>("clear-history-button");
        _errorPanelController = new ErrorPanelController(
            errorPanel, errorContainer, clearHistoryButton, errorItemTemplate);

        // ConnectionStatus 초기화
        var connectionStatus = _root.Q<VisualElement>("connection-status");
        var connectionIndicator = _root.Q<VisualElement>("connection-indicator");
        var connectionStatusText = _root.Q<Label>("connection-status-text");
        var reconnectButton = _root.Q<Button>("reconnect-button");
        _connectionStatusController = new ConnectionStatusController(
            connectionStatus, connectionIndicator, connectionStatusText, reconnectButton);
    }

    private void SubscribeToEvents()
    {
        // ErrorManager 이벤트 구독
        if (ErrorManager.Instance != null)
        {
            ErrorManager.Instance.ErrorRaisedAction += OnErrorRaised;
            ErrorManager.Instance.ErrorClearedAction += OnErrorCleared;
            ErrorManager.Instance.AllClearedAction += OnAllErrorsCleared;
        }

        // IPCReceiver 이벤트 구독
        if (IPCReceiver.Instance != null)
        {
            IPCReceiver.Instance.ConnectionStatusChanged += OnConnectionStatusChanged;
            // 초기 상태 반영
            _connectionStatusController.UpdateStatus(IPCReceiver.Instance.Status);
        }
        else
        {
            // IPCReceiver가 아직 없으면 대기
            StartCoroutine(WaitForIPCReceiver());
        }
    }

    private System.Collections.IEnumerator WaitForIPCReceiver()
    {
        while (IPCReceiver.Instance == null)
        {
            yield return null;
        }

        IPCReceiver.Instance.ConnectionStatusChanged += OnConnectionStatusChanged;
        _connectionStatusController.UpdateStatus(IPCReceiver.Instance.Status);
    }

    private void CleanupControllers()
    {
        // 이벤트 구독 해제
        if (ErrorManager.Instance != null)
        {
            ErrorManager.Instance.ErrorRaisedAction -= OnErrorRaised;
            ErrorManager.Instance.ErrorClearedAction -= OnErrorCleared;
            ErrorManager.Instance.AllClearedAction -= OnAllErrorsCleared;
        }

        if (IPCReceiver.Instance != null)
        {
            IPCReceiver.Instance.ConnectionStatusChanged -= OnConnectionStatusChanged;
        }

        _errorPanelController?.Cleanup();
        _connectionStatusController?.Cleanup();
    }

    // === 이벤트 핸들러 ===

    private void OnErrorRaised(ErrorInfo errorInfo)
    {
        _statusBarController.UpdateStatus(ErrorManager.Instance);
        _errorPanelController.AddError(errorInfo);
    }

    private void OnErrorCleared(string errorId)
    {
        _statusBarController.UpdateStatus(ErrorManager.Instance);
        _errorPanelController.MarkAsResolved(errorId);
    }

    private void OnAllErrorsCleared()
    {
        _statusBarController.UpdateStatus(ErrorManager.Instance);
        _errorPanelController.MarkAllAsResolved();
    }

    private void OnConnectionStatusChanged(ConnectionStatus status)
    {
        _connectionStatusController.UpdateStatus(status);
    }
}

/// <summary>
/// 상단 상태 표시줄 컨트롤러
/// </summary>
public class StatusBarController
{
    private readonly VisualElement _statusBar;
    private readonly Label _statusText;

    public StatusBarController(VisualElement statusBar, Label statusText)
    {
        _statusBar = statusBar;
        _statusText = statusText;
    }

    public void UpdateStatus(ErrorManager errorManager)
    {
        if (errorManager == null) return;

        // 기존 클래스 제거
        _statusBar.RemoveFromClassList("warning");
        _statusBar.RemoveFromClassList("error");

        if (errorManager.HasErrors)
        {
            _statusBar.AddToClassList("error");
            _statusText.text = "SYSTEM ERROR";
        }
        else if (errorManager.HasWarnings)
        {
            _statusBar.AddToClassList("warning");
            _statusText.text = "SYSTEM WARNING";
        }
        else
        {
            _statusText.text = "SYSTEM NORMAL";
        }
    }
}

/// <summary>
/// 에러 패널 컨트롤러
/// </summary>
public class ErrorPanelController
{
    private readonly VisualElement _errorPanel;
    private readonly VisualElement _errorContainer;
    private readonly Button _clearHistoryButton;
    private readonly VisualTreeAsset _errorItemTemplate;

    private readonly System.Collections.Generic.Dictionary<string, VisualElement> _errorItems
        = new System.Collections.Generic.Dictionary<string, VisualElement>();
    private readonly System.Collections.Generic.List<VisualElement> _allErrorItems
        = new System.Collections.Generic.List<VisualElement>();

    public ErrorPanelController(
        VisualElement errorPanel,
        VisualElement errorContainer,
        Button clearHistoryButton,
        VisualTreeAsset errorItemTemplate)
    {
        _errorPanel = errorPanel;
        _errorContainer = errorContainer;
        _clearHistoryButton = clearHistoryButton;
        _errorItemTemplate = errorItemTemplate;

        _clearHistoryButton.clicked += OnClearHistoryClicked;
    }

    public void Cleanup()
    {
        _clearHistoryButton.clicked -= OnClearHistoryClicked;
    }

    public void AddError(ErrorInfo errorInfo)
    {
        // 이미 존재하는 에러는 무시
        if (_errorItems.ContainsKey(errorInfo.Id)) return;

        // 패널 표시
        _errorPanel.AddToClassList("visible");

        // 템플릿에서 새 에러 아이템 생성
        var errorItem = _errorItemTemplate.Instantiate();
        var itemRoot = errorItem.Q<VisualElement>("error-item");

        // 데이터 바인딩
        var badgeText = errorItem.Q<Label>("error-badge-text");
        var badge = errorItem.Q<VisualElement>("error-badge");
        var border = errorItem.Q<VisualElement>("error-border");
        var location = errorItem.Q<Label>("error-location");
        var message = errorItem.Q<Label>("error-message");
        var timestamp = errorItem.Q<Label>("error-timestamp");

        bool isWarning = errorInfo.Type == ErrorType.Warning;

        badgeText.text = isWarning ? "WARN" : "ERROR";
        location.text = errorInfo.Location;
        message.text = errorInfo.Message;
        timestamp.text = System.DateTime.Now.ToString("HH:mm:ss");

        if (isWarning)
        {
            badge.AddToClassList("warning");
            border.AddToClassList("warning");
        }

        // 컨테이너 맨 위에 추가
        _errorContainer.Insert(0, errorItem);

        // 추적 목록에 추가
        _errorItems[errorInfo.Id] = itemRoot;
        _allErrorItems.Add(itemRoot);

        // 애니메이션 트리거 (다음 프레임에 클래스 추가)
        itemRoot.schedule.Execute(() => itemRoot.AddToClassList("visible"));
    }

    public void MarkAsResolved(string errorId)
    {
        if (_errorItems.TryGetValue(errorId, out var errorItem))
        {
            _errorItems.Remove(errorId);

            errorItem.AddToClassList("resolved");

            var border = errorItem.Q<VisualElement>("error-border");
            var badge = errorItem.Q<VisualElement>("error-badge");
            var location = errorItem.Q<Label>("error-location");
            var message = errorItem.Q<Label>("error-message");
            var timestamp = errorItem.Q<Label>("error-timestamp");

            border.AddToClassList("resolved");
            badge.AddToClassList("resolved");
            location.AddToClassList("resolved");
            message.AddToClassList("resolved");

            timestamp.text += " (Resolved)";
        }
    }

    public void MarkAllAsResolved()
    {
        foreach (var kvp in _errorItems)
        {
            var errorItem = kvp.Value;
            errorItem.AddToClassList("resolved");

            var border = errorItem.Q<VisualElement>("error-border");
            var badge = errorItem.Q<VisualElement>("error-badge");
            var location = errorItem.Q<Label>("error-location");
            var message = errorItem.Q<Label>("error-message");
            var timestamp = errorItem.Q<Label>("error-timestamp");

            border.AddToClassList("resolved");
            badge.AddToClassList("resolved");
            location.AddToClassList("resolved");
            message.AddToClassList("resolved");

            timestamp.text += " (Resolved)";
        }

        _errorItems.Clear();
    }

    private void OnClearHistoryClicked()
    {
        // Resolved된 항목만 삭제
        for (int i = _allErrorItems.Count - 1; i >= 0; i--)
        {
            var item = _allErrorItems[i];
            if (item.ClassListContains("resolved"))
            {
                item.parent?.Remove(item);
                _allErrorItems.RemoveAt(i);
            }
        }

        // 남은 항목이 없으면 패널 숨김
        if (_allErrorItems.Count == 0)
        {
            _errorPanel.RemoveFromClassList("visible");
        }
    }
}

/// <summary>
/// 연결 상태 컨트롤러
/// </summary>
public class ConnectionStatusController
{
    private readonly VisualElement _connectionStatus;
    private readonly VisualElement _indicator;
    private readonly Label _statusText;
    private readonly Button _reconnectButton;

    private IVisualElementScheduledItem _blinkSchedule;
    private bool _isBlinkOn = true;

    public ConnectionStatusController(
        VisualElement connectionStatus,
        VisualElement indicator,
        Label statusText,
        Button reconnectButton)
    {
        _connectionStatus = connectionStatus;
        _indicator = indicator;
        _statusText = statusText;
        _reconnectButton = reconnectButton;

        _reconnectButton.clicked += OnReconnectClicked;
    }

    public void Cleanup()
    {
        _reconnectButton.clicked -= OnReconnectClicked;
        StopBlinking();
    }

    public void UpdateStatus(ConnectionStatus status)
    {
        // 기존 클래스 제거
        _indicator.RemoveFromClassList("connected");
        _indicator.RemoveFromClassList("connecting");
        _indicator.RemoveFromClassList("disconnected");

        StopBlinking();

        switch (status)
        {
            case ConnectionStatus.Connected:
                _indicator.AddToClassList("connected");
                _statusText.text = "CONNECTED";
                _reconnectButton.AddToClassList("hidden");
                break;

            case ConnectionStatus.Connecting:
                _indicator.AddToClassList("connecting");
                _statusText.text = "CONNECTING...";
                _reconnectButton.AddToClassList("hidden");
                StartBlinking();
                break;

            case ConnectionStatus.Disconnected:
                _indicator.AddToClassList("disconnected");
                _statusText.text = "DISCONNECTED";
                _reconnectButton.RemoveFromClassList("hidden");
                StartBlinking();
                break;
        }
    }

    private void StartBlinking()
    {
        _isBlinkOn = true;
        _blinkSchedule = _indicator.schedule.Execute(() =>
        {
            _isBlinkOn = !_isBlinkOn;
            _indicator.style.opacity = _isBlinkOn ? 1f : 0.3f;
        }).Every(500);
    }

    private void StopBlinking()
    {
        _blinkSchedule?.Pause();
        _blinkSchedule = null;
        _indicator.style.opacity = 1f;
    }

    private void OnReconnectClicked()
    {
        if (IPCReceiver.Instance != null)
        {
            IPCReceiver.Instance.Reconnect();
        }
    }
}
