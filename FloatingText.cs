using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    [Header("References")]
    public TextMeshPro textMesh;

    [Header("Style Settings")]
    public float normalFontSize = 12f;
    public float critFontSize = 16f;

    [Header("Animation Settings")]
    [Tooltip("How far the text can randomly drift horizontally from its starting point.")]
    public float horizontalRandomness = 1.5f;
    [Tooltip("How high the text will float up.")]
    public float upwardMovement = 4.0f;
    [Tooltip("How long the text stays on screen in seconds.")]
    public float lifetime = 0.8f;

    private Transform cameraTransform;
    private PooledObject pooledObject;
    private Sequence activeSequence;

    void Awake()
    {
        // GetComponent<PooledObject>() has been REMOVED from Awake()
    }

    void OnEnable()
    {
        // --- FIX: Get the component here ---
        if (pooledObject == null)
        {
            pooledObject = GetComponent<PooledObject>();
        }
        // --- END FIX ---

        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Reset state
        textMesh.alpha = 1f;
        transform.localScale = Vector3.one;

        // Start animation
        Vector3 randomOffset = Random.insideUnitSphere * horizontalRandomness;
        randomOffset.y = 0;
        Vector3 endPosition = transform.position + (Vector3.up * upwardMovement) + randomOffset;

        // Kill any previous tween and start a new one
        activeSequence.Kill();
        activeSequence = DOTween.Sequence();
        activeSequence.Append(transform.DOMove(endPosition, lifetime).SetEase(Ease.OutQuad));
        activeSequence.Join(textMesh.DOFade(0, lifetime).SetEase(Ease.InQuad));
        activeSequence.OnComplete(() => {
            // This check will now succeed
            if (pooledObject != null) pooledObject.ReturnToPool();
        });
    }

    void OnDisable()
    {
        activeSequence.Kill();
    }

    void LateUpdate()
    {
        if (cameraTransform != null)
        {
            transform.LookAt(transform.position + cameraTransform.rotation * Vector3.forward,
                             cameraTransform.rotation * Vector3.up);
        }
    }

    public void SetText(string text)
    {
        if (textMesh != null)
        {
            textMesh.text = text;
        }
    }

    public void SetColor(Color color)
    {
        if (textMesh != null)
        {
            textMesh.color = color;
        }
    }

    public void SetStyle(bool isCrit)
    {
        if (textMesh == null) return;

        if (isCrit)
        {
            textMesh.fontSize = critFontSize;
            textMesh.fontStyle = FontStyles.Bold;
        }
        else
        {
            textMesh.fontSize = normalFontSize;
            textMesh.fontStyle = FontStyles.Normal;
        }
    }

    public void DestroyObject()
    {
        if (pooledObject != null) pooledObject.ReturnToPool();
    }
}