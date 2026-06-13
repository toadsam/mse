using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// file: Assets/Scripts/JJH/UI/UIButtonAnimator.cs
// Lightweight hover/press/select animator that also attaches a shared button click sound.
public class UIButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float pressedScale = 0.96f;
    [SerializeField] private float duration = 0.08f;

    private Button button;
    private RectTransform rectTransform;
    private Coroutine scaleRoutine;
    private Vector3 baseScale;
    private bool hasBaseScale;
    private bool isHovered;
    private bool isPressed;
    private bool isSelected;

    // Ensures the target button has exactly one animator component configured and ready.
    public static UIButtonAnimator Ensure(Button target)
    {
        if (target == null)
            return null;

        UIButtonAnimator animator = target.GetComponent<UIButtonAnimator>();
        if (animator == null)
            animator = target.gameObject.AddComponent<UIButtonAnimator>();

        animator.CaptureBaseScale();
        animator.BindClickSound();
        return animator;
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        rectTransform = transform as RectTransform;
        CaptureBaseScale();
        BindClickSound();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(PlayClickSound);
    }

    // Resets visual state whenever the button becomes active again.
    private void OnEnable()
    {
        CaptureBaseScale();
        AnimateToTarget(true);
    }

    // Restores the captured base scale when the button is disabled.
    private void OnDisable()
    {
        isHovered = false;
        isPressed = false;
        isSelected = false;

        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);

        if (hasBaseScale)
            transform.localScale = baseScale;
    }

    // Keeps disabled buttons snapped to their neutral scale.
    private void Update()
    {
        if (button != null && !button.interactable && transform.localScale != baseScale)
            AnimateToTarget();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        AnimateToTarget();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        AnimateToTarget();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        AnimateToTarget();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        AnimateToTarget();
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        AnimateToTarget();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        isPressed = false;
        AnimateToTarget();
    }

    // Resolves the current target scale from pointer/selection state and animates toward it.
    private void AnimateToTarget(bool instant = false)
    {
        CaptureBaseScale();

        float targetScale = 1f;

        if (button == null || button.interactable)
        {
            if (isPressed)
                targetScale = pressedScale;
            else if (isHovered || isSelected)
                targetScale = hoverScale;
        }

        Vector3 target = baseScale * targetScale;

        if (instant || duration <= 0f || !gameObject.activeInHierarchy)
        {
            transform.localScale = target;
            return;
        }

        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);

        scaleRoutine = StartCoroutine(ScaleTo(target));
    }

    // Smoothly interpolates the button scale for hover and press feedback.
    private IEnumerator ScaleTo(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.localScale = Vector3.LerpUnclamped(start, target, t);
            yield return null;
        }

        transform.localScale = target;
        scaleRoutine = null;
    }

    // Captures the original transform scale once so animation always returns to the designer value.
    private void CaptureBaseScale()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (button == null)
            button = GetComponent<Button>();

        if (hasBaseScale)
            return;

        baseScale = transform.localScale;
        hasBaseScale = true;
    }

    // Binds the shared UI click sound without duplicating listeners.
    private void BindClickSound()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
            return;

        button.onClick.RemoveListener(PlayClickSound);
        button.onClick.AddListener(PlayClickSound);
    }

    private void PlayClickSound()
    {
        GameAudio.PlaySfx2D(GameAudioClipId.ButtonClick, 0.75f);
    }
}
