using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ErrorUIManager : MonoBehaviour
{
    [Header("Status Bar")]
    public Image statusBarBackground;
    public TextMeshProUGUI statusText;
    public Image statusIcon;

    [Header("Error List")]
    public Transform errorContainer;
    public GameObject errorPrefab;
    public CanvasGroup panelCanvasGroup;
    public Button clearHistoryButton;

    [Header("Colors")]
    public Color normalColor = new Color(0.1f, 0.37f, 0.12f); // Green
    public Color warningColor = new Color(0.96f, 0.5f, 0.09f); // Orange
    public Color errorColor = new Color(0.72f, 0.11f, 0.11f); // Red

    // 활성 에러 추적 (아직 해소되지 않은 것)
    private Dictionary<string, ErrorUI> errorUIDict = new Dictionary<string, ErrorUI>();
    // 모든 에러 UI 추적 (히스토리 포함)
    private List<ErrorUI> errorUIList = new List<ErrorUI>();

    private void Start()
    {
        UpdateStatusBar();
        panelCanvasGroup.alpha = 0f;

        if (ErrorManager.Instance != null)
        {
            ErrorManager.Instance.ErrorRaisedAction += HandleErrorRaisedAction;
            ErrorManager.Instance.ErrorClearedAction += HandleErrorClearedAction;
            ErrorManager.Instance.AllClearedAction += HandleAllClearedAction;
        }

        clearHistoryButton.onClick.AddListener(ClearHistory);
    }

    private void OnDestroy()
    {
        if (ErrorManager.Instance != null)
        {
            ErrorManager.Instance.ErrorRaisedAction -= HandleErrorRaisedAction;
            ErrorManager.Instance.ErrorClearedAction -= HandleErrorClearedAction;
            ErrorManager.Instance.AllClearedAction -= HandleAllClearedAction;
        }

        clearHistoryButton.onClick.RemoveListener(ClearHistory);
    }

    private void HandleErrorRaisedAction(ErrorInfo errorInfo)
    {
        UpdateStatusBar();

        if (panelCanvasGroup.alpha < 0.1f)
        {
            StartCoroutine(UIAnimationHelper.FadeIn(panelCanvasGroup, 0.3f));
        }

        if (errorUIDict.ContainsKey(errorInfo.Id) == false)
        {
            GameObject obj = Instantiate(errorPrefab, errorContainer);
            ErrorUI errorUI = obj.GetComponent<ErrorUI>();
            Color color = errorInfo.Type == ErrorType.Error ? errorColor : warningColor;

            errorUI.Setup(errorInfo, color);
            errorUIDict[errorInfo.Id] = errorUI;
            errorUIList.Add(errorUI);

            // 새 에러를 맨 위에 표시
            obj.transform.SetAsFirstSibling();

            // 레이아웃 강제 갱신 후 애니메이션 시작
            LayoutRebuilder.ForceRebuildLayoutImmediate(errorContainer as RectTransform);

            StartCoroutine(UIAnimationHelper.FadeIn(errorUI.canvasGroup, 0.3f));
            StartCoroutine(UIAnimationHelper.SlideIn(errorUI.GetComponent<RectTransform>(), 50f, 0.3f));
        }
    }

    private void HandleErrorClearedAction(string id)
    {
        UpdateStatusBar();

        if (errorUIDict.TryGetValue(id, out ErrorUI errorUI))
        {
            errorUIDict.Remove(id);
            errorUI.MarkAsResolved();
        }
    }

    private void HandleAllClearedAction()
    {
        UpdateStatusBar();

        // 모든 활성 에러를 Resolved로 표시
        foreach (KeyValuePair<string, ErrorUI> item in errorUIDict)
        {
            item.Value.MarkAsResolved();
        }
        errorUIDict.Clear();
    }

    private void UpdateStatusBar()
    {
        if (ErrorManager.Instance == null)
        {
            return;
        }

        if (ErrorManager.Instance.HasErrors)
        {
            statusBarBackground.color = errorColor;
            statusText.text = "SYSTEM ERROR";
        }
        else if (ErrorManager.Instance.HasWarnings)
        {
            statusBarBackground.color = warningColor;
            statusText.text = "SYSTEM WARNING";
        }
        else
        {
            statusBarBackground.color = normalColor;
            statusText.text = "SYSTEM NORMAL";
        }
    }

    /// <summary>
    /// 히스토리 전체 삭제 (Resolved 항목만 삭제하거나 전체 삭제)
    /// </summary>
    public void ClearHistory()
    {
        // Resolved된 항목만 삭제
        for (int i = errorUIList.Count - 1; i >= 0; i--)
        {
            ErrorUI errorUI = errorUIList[i];
            if (errorUI != null && errorUI.IsResolved)
            {
                errorUIList.RemoveAt(i);
                Destroy(errorUI.gameObject);
            }
        }

        // 남은 항목이 없으면 패널 숨김
        if (errorUIList.Count == 0)
        {
            StartCoroutine(UIAnimationHelper.FadeOut(panelCanvasGroup, 0.3f));
        }
    }

    /// <summary>
    /// 모든 히스토리 삭제 (활성 에러 포함)
    /// </summary>
    public void ClearAllHistory()
    {
        foreach (var errorUI in errorUIList)
        {
            if (errorUI != null)
            {
                Destroy(errorUI.gameObject);
            }
        }

        errorUIList.Clear();
        errorUIDict.Clear();
        StartCoroutine(UIAnimationHelper.FadeOut(panelCanvasGroup, 0.3f));
    }
}