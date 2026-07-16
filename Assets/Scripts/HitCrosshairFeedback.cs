using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HitCrosshairFeedback : MonoBehaviour
{
    [Header("References")]
    public Image hitFeedbackImage;
    public Sprite hitCrosshairSprite;

    [Header("Animation Settings")]
    public Color hitFeedbackColor = new Color(1f, 0.3f, 0.3f, 1f); // Red
    public float pulseScaleMax = 1.2f;
    public float pulseDuration = 0.15f;
    public float fadeDuration = 0.3f;
    public float totalDisplayDuration = 0.45f;

    private Coroutine pulseCoroutine;
    private Vector3 cachedOriginalScale;

    private void Start()
    {
        if (hitFeedbackImage == null)
        {
            hitFeedbackImage = GetComponent<Image>();
        }

        // Set up the hit feedback image
        if (hitFeedbackImage != null)
        {
            if (hitCrosshairSprite != null)
            {
                hitFeedbackImage.sprite = hitCrosshairSprite;
            }
            // Initially invisible
            hitFeedbackImage.color = new Color(hitFeedbackColor.r, hitFeedbackColor.g, hitFeedbackColor.b, 0f);
        }

        cachedOriginalScale = hitFeedbackImage != null
            ? hitFeedbackImage.GetComponent<RectTransform>().localScale
            : Vector3.one;
    }

    /// <summary>
    /// Plays the hit feedback animation
    /// </summary>
    public void PlayHitFeedback()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
        }
        pulseCoroutine = StartCoroutine(PulseAnimation());
    }

    private IEnumerator PulseAnimation()
    {
        if (hitFeedbackImage == null)
            yield break;

        RectTransform rectTransform = hitFeedbackImage.GetComponent<RectTransform>();
        rectTransform.localScale = cachedOriginalScale;
        Vector3 originalScale = cachedOriginalScale;
        Vector3 targetScale = originalScale * pulseScaleMax;

        // Pulse out phase (scale up + appear)
        float elapsedTime = 0f;
        while (elapsedTime < pulseDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / pulseDuration;

            // Scale pulse
            rectTransform.localScale = Vector3.Lerp(originalScale, targetScale, progress);

            // Fade in
            Color currentColor = hitFeedbackColor;
            currentColor.a = Mathf.Lerp(0f, 1f, progress);
            hitFeedbackImage.color = currentColor;

            yield return null;
        }

        rectTransform.localScale = targetScale;
        Color pulseEndColor = hitFeedbackColor;
        pulseEndColor.a = 1f;
        hitFeedbackImage.color = pulseEndColor;

        // Fade out phase
        elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / fadeDuration;

            // Scale down back to original
            rectTransform.localScale = Vector3.Lerp(targetScale, originalScale, progress);

            // Fade out
            Color currentColor = hitFeedbackColor;
            currentColor.a = Mathf.Lerp(1f, 0f, progress);
            hitFeedbackImage.color = currentColor;

            yield return null;
        }

        // Ensure final state
        rectTransform.localScale = originalScale;
        Color finalColor = hitFeedbackColor;
        finalColor.a = 0f;
        hitFeedbackImage.color = finalColor;
    }
}
