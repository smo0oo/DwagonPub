using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UIIconEffect : MonoBehaviour, IMaterialModifier
{
    // Allow manual override in Inspector if you ever change the property name
    [SerializeField] private string texturePropertyName = "_MainTex";
    [SerializeField] private string desaturationPropertyName = "_Desaturation";

    private Material _cachedMaterial;
    private float _currentDesaturation = 0f;
    private Texture _overrideTexture;

    private int _desaturationID;
    private int _mainTexID;

    void Awake()
    {
        // Cache the IDs once based on the names above
        _desaturationID = Shader.PropertyToID(desaturationPropertyName);
        _mainTexID = Shader.PropertyToID(texturePropertyName);
    }

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        // 1. Create or Recreate Instance if base changed
        if (_cachedMaterial == null || _cachedMaterial.shader != baseMaterial.shader)
        {
            if (_cachedMaterial != null) DestroyImmediate(_cachedMaterial);
            _cachedMaterial = new Material(baseMaterial);
            _cachedMaterial.name = baseMaterial.name + " (UI Instance)";
            _cachedMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        // 2. Apply Properties
        _cachedMaterial.SetFloat(_desaturationID, _currentDesaturation);

        if (_overrideTexture != null)
        {
            _cachedMaterial.SetTexture(_mainTexID, _overrideTexture);
        }

        return _cachedMaterial;
    }

    public void SetDesaturation(float amount)
    {
        if (Mathf.Abs(_currentDesaturation - amount) > 0.01f)
        {
            _currentDesaturation = amount;
            GetComponent<Image>().SetMaterialDirty();
        }
    }

    public void SetTexture(Texture tex)
    {
        if (_overrideTexture != tex)
        {
            _overrideTexture = tex;
            GetComponent<Image>().SetMaterialDirty();
        }
    }

    void OnDestroy()
    {
        if (_cachedMaterial != null) DestroyImmediate(_cachedMaterial);
    }
}