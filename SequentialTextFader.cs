using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class SequentialTextFader : MonoBehaviour
{
    [Header("Target Component")]
    [Tooltip("Assign the TextMeshProUGUI component here.")]
    public TextMeshProUGUI targetText;

    [Header("Sequence Settings")]
    [Tooltip("The list of strings to display in order.")]
    [TextArea(2, 5)]
    public List<string> textElements;

    [Tooltip("Should the sequence restart from the beginning when finished?")]
    public bool loopSequence = true;

    [Tooltip("Start playing the sequence automatically on Start.")]
    public bool playOnStart = true;

    [Header("Timing Parameters")]
    [Tooltip("Time (seconds) to fade from invisible to fully visible.")]
    public float fadeInDuration = 1.0f;

    [Tooltip("Time (seconds) the text stays fully visible.")]
    public float stayDuration = 2.0f;

    [Tooltip("Time (seconds) to fade from fully visible to invisible.")]
    public float fadeOutDuration = 1.0f;

    [Tooltip("Time (seconds) to wait after fading out before the next element starts.")]
    public float delayBeforeNext = 0.5f;

    private Coroutine sequenceCoroutine;

    private void Start()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TextMeshProUGUI>();
        }

        if (targetText != null)
        {
            // Ensure alpha starts at 0
            targetText.alpha = 0;

            if (playOnStart)
            {
                StartSequence();
            }
        }
        else
        {
            Debug.LogError("[SequentialTextFader] No TextMeshProUGUI assigned or found!");
        }
    }

    /// <summary>
    /// Starts or Restarts the text sequence.
    /// </summary>
    [ContextMenu("Play Sequence")]
    public void StartSequence()
    {
        StopSequence(); // Ensure no duplicates run
        if (textElements != null && textElements.Count > 0)
        {
            sequenceCoroutine = StartCoroutine(RunSequence());
        }
    }

    /// <summary>
    /// Stops the current sequence immediately.
    /// </summary>
    public void StopSequence()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }
    }

    private IEnumerator RunSequence()
    {
        int index = 0;

        while (true)
        {
            // 1. Set Content
            if (index >= textElements.Count)
            {
                if (loopSequence)
                {
                    index = 0;
                }
                else
                {
                    yield break; // End of sequence
                }
            }

            targetText.text = textElements[index];

            // 2. Fade In
            yield return FadeText(0f, 1f, fadeInDuration);

            // 3. Stay
            yield return new WaitForSeconds(stayDuration);

            // 4. Fade Out
            yield return FadeText(1f, 0f, fadeOutDuration);

            // 5. Delay before next
            yield return new WaitForSeconds(delayBeforeNext);

            index++;
        }
    }

    private IEnumerator FadeText(float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;

        // Ensure starting value is set explicitly
        targetText.alpha = startAlpha;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            targetText.alpha = newAlpha;
            yield return null;
        }

        // Ensure final value is set explicitly
        targetText.alpha = endAlpha;
    }
}