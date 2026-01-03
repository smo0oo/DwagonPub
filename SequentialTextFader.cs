using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events; // Added for UnityEvent

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
    public bool loopSequence = false; // Defaulted to false for Intro scenes

    [Tooltip("Start playing the sequence automatically on Start.")]
    public bool playOnStart = true;

    [Header("Timing Parameters")]
    public float fadeInDuration = 1.0f;
    public float stayDuration = 2.0f;
    public float fadeOutDuration = 1.0f;
    public float delayBeforeNext = 0.5f;

    [Header("Events")]
    public UnityEvent onSequenceComplete; // New Event

    private Coroutine sequenceCoroutine;

    private void Start()
    {
        if (targetText == null) targetText = GetComponent<TextMeshProUGUI>();

        if (targetText != null)
        {
            targetText.alpha = 0;
            if (playOnStart) StartSequence();
        }
    }

    [ContextMenu("Play Sequence")]
    public void StartSequence()
    {
        StopSequence();
        if (textElements != null && textElements.Count > 0)
        {
            sequenceCoroutine = StartCoroutine(RunSequence());
        }
    }

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
            // 1. Check if done
            if (index >= textElements.Count)
            {
                if (loopSequence)
                {
                    index = 0;
                }
                else
                {
                    // Trigger the event when finished
                    onSequenceComplete?.Invoke();
                    yield break;
                }
            }

            // 2. Set Content & Fade In
            targetText.text = textElements[index];
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
        targetText.alpha = startAlpha;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            targetText.alpha = newAlpha;
            yield return null;
        }

        targetText.alpha = endAlpha;
    }
}