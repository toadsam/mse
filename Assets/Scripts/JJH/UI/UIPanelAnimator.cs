using System;
using System.Collections;
using UnityEngine;

// file: Assets/Scripts/JJH/UI/UIPanelAnimator.cs
// Reusable panel animator for fade/slide/scale transitions plus shake and punch emphasis.
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

    // Ensures the target object has a panel animator and captures its designed base transform.
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

    // Overrides the hidden-state offset/scale and transition durations for a specific panel.
    public void Configure(Vector2 offset, float scale, float showSeconds = 0.16f, float hideSeconds = 0.1f)
    {
        hiddenOffset = offset;
        hiddenScale = Mathf.Clamp(scale, 0.01f, 1f);
        showDuration = Mathf.Max(0.01f, showSeconds);
        hideDuration = Mathf.Max(0.01f, hideSeconds);
    }

    // Re-captures the current transform as the new animation baseline.
    public void ResetBaseTransform()
    {
        hasBaseTransform = false;
        CaptureBaseTransform();
    }

    // Shows the panel with optional delay or immediately when requested.
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

    // Hides the panel and optionally deactivates it after the transition.
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

    // Applies a short horizontal shake, typically for validation or error feedback.
    public void Shake(float duration = 0.18f, float strength = 8f, float delay = 0f)
    {
        CaptureBaseTransform();

        if (rectTransform == null)
            return;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeRoutine(duration, strength, delay));
    }

    // Temporarily scales the panel up and back down to emphasize success or focus.
    public void Punch(float scale = 1.08f, float duration = 0.18f, float delay = 0f)
    {
        CaptureBaseTransform();

        if (punchRoutine != null)
            StopCoroutine(punchRoutine);

        punchRoutine = StartCoroutine(PunchRoutine(scale, duration, delay));
    }

    // Drives the main show/hide animation by interpolating toward the target visibility state.
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

    // Applies a decaying side-to-side offset around the panel's captured base position.
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

    // Plays a scale punch from the current size to the emphasized peak and back.
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

    // Shared scale interpolation helper used by punch animations.
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

    // Converts a normalized visible value into canvas alpha, raycast state, position, and scale.
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

    // Captures the designed base position/scale once so animations can return to it precisely.
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
