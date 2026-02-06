using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ErrorUI : MonoBehaviour
{
    public Image typeBadge;
    public TextMeshProUGUI locationText;
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI timestampText;
    public Image leftBorder;
    public Image background;
    public CanvasGroup canvasGroup;

    [Header("Resolved Colors")]
    public Color resolvedBorderColor = new Color(0.4f, 0.4f, 0.4f);
    public Color resolvedBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
    public Color resolvedTextColor = new Color(0.5f, 0.5f, 0.5f);

    public string ErrorId { get; private set; }
    public bool IsResolved { get; private set; }

    public void Setup(ErrorInfo errorInfo, Color color)
    {
        ErrorId = errorInfo.Id;
        IsResolved = false;

        locationText.text = errorInfo.Location;
        messageText.text = errorInfo.Message;
        timestampText.text = System.DateTime.Now.ToString("HH:mm:ss");

        typeBadge.color = color;
        leftBorder.color = color;

        canvasGroup.alpha = 0f;
    }

    public void MarkAsResolved()
    {
        if (IsResolved)
        {
            return;
        }

        IsResolved = true;

        typeBadge.color = resolvedBorderColor;
        leftBorder.color = resolvedBorderColor;
        background.color = resolvedBackgroundColor;

        locationText.color = resolvedTextColor;
        messageText.color = resolvedTextColor;
        timestampText.text += " (Resolved)";
        timestampText.color = resolvedTextColor;

        canvasGroup.alpha = 0.6f;
    }
}