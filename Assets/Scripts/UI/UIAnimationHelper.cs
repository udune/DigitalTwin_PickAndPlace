using System.Collections;
using UnityEngine;

public static class UIAnimationHelper
{
    public static IEnumerator FadeIn(CanvasGroup canvasGroup, float duration)
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, 1f, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    public static IEnumerator FadeOut(CanvasGroup canvasGroup, float duration)
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, 0f, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }

    public static IEnumerator SlideIn(RectTransform rectTransform, float distance, float duration)
    {
        Vector2 endPos = rectTransform.anchoredPosition;
        Vector2 startPos = endPos + new Vector2(distance, 0);
        rectTransform.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float time = elapsed / duration;
            time = 1f - Mathf.Pow(1f - time, 3);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, time);
            yield return null;
        }
        rectTransform.anchoredPosition = endPos;
    }
}