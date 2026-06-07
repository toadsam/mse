using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    public static UIButtonAnimator Ensure(Button target)
    {
        if (target == null)
            return null;

        UIButtonAnimator animator = target.GetComponent<UIButtonAnimator>();
        if (animator == null)
            animator = target.gameObject.AddComponent<UIButtonAnimator>();

        animator.CaptureBaseScale();
        return animator;
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        rectTransform = transform as RectTransform;
        CaptureBaseScale();
    }

    private void OnEnable()
    {
        CaptureBaseScale();
        AnimateToTarget(true);
    }

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
}
