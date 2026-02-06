using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ErrorUI : MonoBehaviour
{
    public Image typeBadge;
    public TextMeshProUGUI locationText;
    public TextMeshProUGUI messageText;
    public Image leftBorder;
    public CanvasGroup canvasGroup;

    public string ErrorId { get; private set; }

    public void Setup(ErrorInfo info, Color themeColor)
    {
        ErrorId = info.Id;
        locationText.text = info.Location;
        messageText.text = info.Message;
            
        typeBadge.color = themeColor;
        leftBorder.color = themeColor;
            
        canvasGroup.alpha = 0f;
    }
}