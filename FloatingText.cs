using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Text; // Required for StringBuilder

public class FloatingText : MonoBehaviour
{
    [Header("References")]
    public TextMeshPro textMesh;

    [Header("Style Settings")]
    public float normalFontSize = 12f;
    public float critFontSize = 16f;

    [Header("Animation Settings")]
    public float horizontalRandomness = 1.5f;
    public float upwardMovement = 4.0f;
    public float lifetime = 0.8f;

    private Transform cameraTransform;
    private PooledObject pooledObject;
    private Sequence activeSequence;
    private Transform _transform;

    void Awake()
    {
        _transform = transform;
    }

    void OnEnable()
    {
        if (pooledObject == null) pooledObject = GetComponent<PooledObject>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        textMesh.alpha = 1f;
        _transform.localScale = Vector3.one;

        // Force rotation immediately so it doesn't pop in wrong
        if (cameraTransform != null) _transform.rotation = cameraTransform.rotation;

        Vector3 randomOffset = Random.insideUnitSphere * horizontalRandomness;
        randomOffset.y = 0;
        Vector3 endPosition = _transform.position + (Vector3.up * upwardMovement) + randomOffset;

        activeSequence.Kill();
        activeSequence = DOTween.Sequence();
        activeSequence.Append(_transform.DOMove(endPosition, lifetime).SetEase(Ease.OutQuad));
        activeSequence.Join(textMesh.DOFade(0, lifetime).SetEase(Ease.InQuad));
        activeSequence.OnComplete(() => {
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
            // --- OPTIMIZATION: Direct Rotation Copy ---
            _transform.rotation = cameraTransform.rotation;
        }
    }

    public void SetText(string text)
    {
        if (textMesh != null) textMesh.text = text;
    }

    // --- NEW: Optimization for StringBuilder ---
    public void SetText(StringBuilder text)
    {
        if (textMesh != null) textMesh.SetText(text);
    }

    public void SetColor(Color color)
    {
        if (textMesh != null) textMesh.color = color;
    }

    public void SetStyle(bool isCrit)
    {
        if (textMesh == null) return;
        textMesh.fontSize = isCrit ? critFontSize : normalFontSize;
        textMesh.fontStyle = isCrit ? FontStyles.Bold : FontStyles.Normal;
    }
}