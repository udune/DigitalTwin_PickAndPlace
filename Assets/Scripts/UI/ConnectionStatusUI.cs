using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConnectionStatusUI : MonoBehaviour
{
    [Header("UI References")]
    public Image statusIndicator;
    public TextMeshProUGUI statusText;
    public Button reconnectButton;

    [Header("Colors")]
    public Color connectedColor = new Color(0.1f, 0.7f, 0.2f);
    public Color connectingColor = new Color(0.9f, 0.7f, 0.1f);
    public Color disconnectedColor = new Color(0.7f, 0.2f, 0.2f);

    [Header("Animation")]
    public float blinkSpeed = 2f;

    private bool _isBlinking = false;
    private float _blinkTimer = 0f;

    private void Start()
    {
        if (reconnectButton != null)
        {
            reconnectButton.onClick.AddListener(OnReconnectClicked);
        }

        // 초기 상태 설정
        UpdateUI(ConnectionStatus.Disconnected);

        // IPCReceiver 이벤트 구독
        if (IPCReceiver.Instance != null)
        {
            IPCReceiver.Instance.ConnectionStatusChanged += OnConnectionStatusChanged;
            UpdateUI(IPCReceiver.Instance.Status);
        }
        else
        {
            // IPCReceiver가 아직 초기화되지 않은 경우 대기
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
        UpdateUI(IPCReceiver.Instance.Status);
    }

    private void OnDestroy()
    {
        if (IPCReceiver.Instance != null)
        {
            IPCReceiver.Instance.ConnectionStatusChanged -= OnConnectionStatusChanged;
        }

        if (reconnectButton != null)
        {
            reconnectButton.onClick.RemoveListener(OnReconnectClicked);
        }
    }

    private void Update()
    {
        if (_isBlinking && statusIndicator != null)
        {
            _blinkTimer += Time.unscaledDeltaTime * blinkSpeed;
            float alpha = (Mathf.Sin(_blinkTimer * Mathf.PI * 2f) + 1f) / 2f;
            alpha = Mathf.Lerp(0.3f, 1f, alpha);

            Color color = statusIndicator.color;
            color.a = alpha;
            statusIndicator.color = color;
        }
    }

    private void OnConnectionStatusChanged(ConnectionStatus status)
    {
        UpdateUI(status);
    }

    private void UpdateUI(ConnectionStatus status)
    {
        switch (status)
        {
            case ConnectionStatus.Connected:
                SetStatus("CONNECTED", connectedColor, false);
                SetReconnectButtonVisible(false);
                break;

            case ConnectionStatus.Connecting:
                SetStatus("CONNECTING...", connectingColor, true);
                SetReconnectButtonVisible(false);
                break;

            case ConnectionStatus.Disconnected:
                SetStatus("DISCONNECTED", disconnectedColor, true);
                SetReconnectButtonVisible(true);
                break;
        }
    }

    private void SetStatus(string text, Color color, bool blink)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }

        if (statusIndicator != null)
        {
            statusIndicator.color = color;
        }

        _isBlinking = blink;
        if (!blink)
        {
            _blinkTimer = 0f;
            if (statusIndicator != null)
            {
                Color c = statusIndicator.color;
                c.a = 1f;
                statusIndicator.color = c;
            }
        }
    }

    private void SetReconnectButtonVisible(bool visible)
    {
        if (reconnectButton != null)
        {
            reconnectButton.gameObject.SetActive(visible);
        }
    }

    private void OnReconnectClicked()
    {
        if (IPCReceiver.Instance != null)
        {
            IPCReceiver.Instance.Reconnect();
        }
    }
}
