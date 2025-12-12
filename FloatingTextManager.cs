using UnityEngine;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager instance;

    [Header("References")]
    public GameObject floatingTextPrefab;

    [Header("Color Settings")]
    public Color physicalDamageColor = Color.white;
    public Color magicDamageColor = new Color(0.8f, 0.5f, 1f);
    public Color healColor = Color.green;
    public Color manaColor = Color.blue;
    public Color eventColor = Color.cyan;
    public Color overkillColor = Color.red;
    public Color critColor = Color.yellow;
    public Color aiStatusColor = Color.grey;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            // DontDestroyOnLoad(gameObject); // REMOVED: The root GameManager is already persistent.
        }
    }

    public void ShowAIStatus(string text, Vector3 position)
    {
        if (floatingTextPrefab == null) return;
        GameObject textObject = ObjectPooler.instance.Get(floatingTextPrefab, position, Quaternion.identity);
        if (textObject == null) return;
        FloatingText floatingText = textObject.GetComponent<FloatingText>();
        floatingText.SetText(text);
        floatingText.SetStyle(false);
        floatingText.SetColor(aiStatusColor);
    }

    public void ShowDamage(int amount, bool isCrit, DamageEffect.DamageType damageType, Vector3 position)
    {
        if (floatingTextPrefab == null) return;
        GameObject textObject = ObjectPooler.instance.Get(floatingTextPrefab, position, Quaternion.identity);
        if (textObject == null) return;
        FloatingText floatingText = textObject.GetComponent<FloatingText>();
        floatingText.SetText(amount.ToString());
        floatingText.SetStyle(isCrit);
        Color damageColor = (damageType == DamageEffect.DamageType.Physical) ? physicalDamageColor : magicDamageColor;
        floatingText.SetColor(isCrit ? critColor : damageColor);
    }

    public void ShowHeal(int amount, bool isCrit, Vector3 position)
    {
        if (floatingTextPrefab == null) return;
        GameObject textObject = ObjectPooler.instance.Get(floatingTextPrefab, position, Quaternion.identity);
        if (textObject == null) return;
        FloatingText floatingText = textObject.GetComponent<FloatingText>();
        floatingText.SetText("+" + amount.ToString());
        floatingText.SetStyle(isCrit);
        floatingText.SetColor(isCrit ? critColor : healColor);
    }

    public void ShowMana(int amount, Vector3 position)
    {
        if (floatingTextPrefab == null) return;
        GameObject textObject = ObjectPooler.instance.Get(floatingTextPrefab, position, Quaternion.identity);
        if (textObject == null) return;
        FloatingText floatingText = textObject.GetComponent<FloatingText>();
        floatingText.SetText($"+{amount} Mana");
        floatingText.SetStyle(false);
        floatingText.SetColor(manaColor);
    }

    public void ShowOverkill(int amount, Vector3 position)
    {
        if (floatingTextPrefab == null) return;
        GameObject textObject = ObjectPooler.instance.Get(floatingTextPrefab, position, Quaternion.identity);
        if (textObject == null) return;
        FloatingText floatingText = textObject.GetComponent<FloatingText>();
        floatingText.SetText($"Overkill: ({amount})");
        floatingText.SetStyle(true);
        floatingText.SetColor(overkillColor);
    }

    public void ShowEvent(string text, Vector3 position)
    {
        if (floatingTextPrefab == null) return;
        GameObject textObject = ObjectPooler.instance.Get(floatingTextPrefab, position, Quaternion.identity);
        if (textObject == null) return;
        FloatingText floatingText = textObject.GetComponent<FloatingText>();
        floatingText.SetText(text);
        floatingText.SetStyle(false);
        floatingText.SetColor(eventColor);
    }
}