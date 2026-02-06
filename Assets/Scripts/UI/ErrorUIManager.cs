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

        [Header("Colors")]
        public Color normalColor = new Color(0.1f, 0.37f, 0.12f); // Green
        public Color warningColor = new Color(0.96f, 0.5f, 0.09f); // Orange
        public Color errorColor = new Color(0.72f, 0.11f, 0.11f); // Red

        private Dictionary<string, ErrorUI> errorUIDict = new Dictionary<string, ErrorUI>();

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
        }

        private void OnDestroy()
        {
            if (ErrorManager.Instance != null)
            {
                ErrorManager.Instance.ErrorRaisedAction -= HandleErrorRaisedAction;
                ErrorManager.Instance.ErrorClearedAction -= HandleErrorClearedAction;
                ErrorManager.Instance.AllClearedAction -= HandleAllClearedAction;
            }
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
                StartCoroutine(RemoveErrorUI(errorUI));
            }
        }

        private IEnumerator RemoveErrorUI(ErrorUI errorUI)
        {
            yield return StartCoroutine(UIAnimationHelper.FadeOut(errorUI.canvasGroup, 0.2f));
            Destroy(errorUI.gameObject);

            if (errorUIDict.Count == 0)
            {
                StartCoroutine(UIAnimationHelper.FadeOut(panelCanvasGroup, 0.3f));
            }
        }

        private void HandleAllClearedAction()
        {
            UpdateStatusBar();
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
    }