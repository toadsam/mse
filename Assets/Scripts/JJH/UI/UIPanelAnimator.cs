using System;
using System.Collections;
using UnityEngine;

public class UIPanelAnimator : MonoBehaviour
{
    [SerializeField] private float showDuration = 0.16f;
    [SerializeField] private float hideDuration = 0.1f;
    [SerializeField] private Vector2 hiddenOffset = new Vector2(0f, -12f);
    [SerializeField] private float hiddenScale = 0.96f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Coroutine animationRoutine;
    private Coroutine shakeRoutine;
    private Coroutine punchRoutine;
    private Vector2 baseAnchoredPosition;
    private Vector3 baseScale;
    private bool hasBaseTransform;

    public static UIPanelAnimator Ensure(GameObject target)
    {
        if (target == null)
            return null;

        UIPanelAnimator animator = target.GetComponent<UIPanelAnimator>();
        if (animator == null)
            animator = target.AddComponent<UIPanelAnimator>();

        animator.CaptureBaseTransform();
        return animator;
    }

    public void Configure(Vector2 offset, float scale, float showSeconds = 0.16f, float hideSeconds = 0.1f)
    {
        hiddenOffset = offset;
        hiddenScale = Mathf.Clamp(scale, 0.01f, 1f);
        showDuration = Mathf.Max(0.01f, showSeconds);
        hideDuration = Mathf.Max(0.01f, hideSeconds);
    }

    public void ResetBaseTransform()
    {
        hasBaseTransform = false;
        CaptureBaseTransform();
    }

    public void Show(bool instant = false, float delay = 0f)
    {
        CaptureBaseTransform();
        gameObject.SetActive(true);

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        if (instant)
        {
            ApplyState(1f, true);
            return;
        }

        animationRoutine = StartCoroutine(AnimateTo(1f, showDuration, false, delay));
    }

    public void Hide(bool instant = false)
    {
        CaptureBaseTransform();

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        if (instant || !gameObject.activeInHierarchy)
        {
            ApplyState(0f, false);
            gameObject.SetActive(false);
            return;
        }

        animationRoutine = StartCoroutine(AnimateTo(0f, hideDuration, true, 0f));
    }

    public void Shake(float duration = 0.18f, float strength = 8f, float delay = 0f)
    {
        CaptureBaseTransform();

        if (rectTransform == null)
            return;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeRoutine(duration, strength, delay));
    }

    public void Punch(float scale = 1.08f, float duration = 0.18f, float delay = 0f)
    {
        CaptureBaseTransform();

        if (punchRoutine != null)
            StopCoroutine(punchRoutine);

        punchRoutine = StartCoroutine(PunchRoutine(scale, duration, delay));
    }

    private IEnumerator AnimateTo(float target, float duration, bool deactivateOnEnd, float delay)
    {
        if (delay > 0f)
        {
            ApplyState(0f, false);
            yield return new WaitForSecondsRealtime(delay);
        }

        float start = canvasGroup != null ? canvasGroup.alpha : target;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            ApplyState(Mathf.Lerp(start, target, eased), target > 0f);
            yield return null;
        }

        ApplyState(target, target > 0f);

        if (deactivateOnEnd)
            gameObject.SetActive(false);

        animationRoutine = null;
    }

    private IEnumerator ShakeRoutine(float duration, float strength, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        Vector2 origin = rectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / duration);
            float x = Mathf.Sin(elapsed * 95f) * strength * fade;
            rectTransform.anchoredPosition = origin + new Vector2(x, 0f);
            yield return null;
        }

        rectTransform.anchoredPosition = origin;
        shakeRoutine = null;
    }

    private IEnumerator PunchRoutine(float scale, float duration, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        Vector3 start = transform.localScale;
        Vector3 peak = baseScale * Mathf.Max(1f, scale);
        float halfDuration = Mathf.Max(0.01f, duration * 0.5f);

        yield return ScaleOverTime(start, peak, halfDuration);
        yield return ScaleOverTime(peak, baseScale, halfDuration);

        transform.localScale = baseScale;
        punchRoutine = null;
    }

    private IEnumerator ScaleOverTime(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }
    }

    private void ApplyState(float visible, bool interactive)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible;
            canvasGroup.interactable = interactive;
            canvasGroup.blocksRaycasts = interactive;
        }

        if (rectTransform != null)
            rectTransform.anchoredPosition = baseAnchoredPosition + hiddenOffset * (1f - visible);

        transform.localScale = Vector3.LerpUnclamped(baseScale * hiddenScale, baseScale, visible);
    }

    private void CaptureBaseTransform()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (hasBaseTransform)
            return;

        baseScale = transform.localScale;
        baseAnchoredPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
        hasBaseTransform = true;
    }
}
