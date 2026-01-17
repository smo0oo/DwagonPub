using UnityEngine;
using System.Text; // Required

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

    // --- OPTIMIZATION: Cached StringBuilder ---
    private StringBuilder sb = new StringBuilder(64);

    void Awake()
    {
        if (instance != null && instance != this) Destroy(gameObject);
        else instance = this;
    }

    // --- NEW: Generic Text Method ---
    public void ShowText(string text, Vector3 position, Color color)
    {
        SpawnText(text, position, false, color);
    }
    // --------------------------------

    public void ShowAIStatus(string text, Vector3 position)
    {
        SpawnText(text, position, false, aiStatusColor);
    }

    public void ShowDamage(int amount, bool isCrit, DamageEffect.DamageType damageType, Vector3 position)
    {
        // Zero-Garbage String Construction
        sb.Clear();
        sb.Append(amount);

        Color damageColor = (damageType == DamageEffect.DamageType.Physical) ? physicalDamageColor : magicDamageColor;
        SpawnTextBuilder(sb, position, isCrit, isCrit ? critColor : damageColor);
    }

    public void ShowHeal(int amount, bool isCrit, Vector3 position)
    {
        sb.Clear();
        sb.Append('+');
        sb.Append(amount);

        SpawnTextBuilder(sb, position, isCrit, isCrit ? critColor : healColor);
    }

    public void ShowMana(int amount, Vector3 position)
    {
        sb.Clear();
        sb.Append('+');
        sb.Append(amount);
        sb.Append(" Mana");

        SpawnTextBuilder(sb, position, false, manaColor);
    }

    public void ShowOverkill(int amount, Vector3 position)
    {
        sb.Clear();
        sb.Append("Overkill: (");
        sb.Append(amount);
        sb.Append(")");

        SpawnTextBuilder(sb, position, true, overkillColor);
    }

    public void ShowEvent(string text, Vector3 position)
    {
        SpawnText(text, position, false, eventColor);
    }

    // --- Internal Helpers ---

    private void SpawnText(string text, Vector3 pos, bool isCrit, Color color)
    {
        if (floatingTextPrefab == null) return;
        GameObject obj = ObjectPooler.instance.Get(floatingTextPrefab, pos, Quaternion.identity);
        if (obj == null) return;

        FloatingText ft = obj.GetComponent<FloatingText>();
        if (ft != null)
        {
            ft.SetText(text);
            ft.SetStyle(isCrit);
            ft.SetColor(color);
        }
    }

    private void SpawnTextBuilder(StringBuilder text, Vector3 pos, bool isCrit, Color color)
    {
        if (floatingTextPrefab == null) return;
        GameObject obj = ObjectPooler.instance.Get(floatingTextPrefab, pos, Quaternion.identity);
        if (obj == null) return;

        FloatingText ft = obj.GetComponent<FloatingText>();
        if (ft != null)
        {
            ft.SetText(text); // Uses the optimized overload
            ft.SetStyle(isCrit);
            ft.SetColor(color);
        }
    }
}