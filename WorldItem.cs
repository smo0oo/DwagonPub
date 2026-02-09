using UnityEngine;
using Unity.VisualScripting;
using UnityEngine.VFX;

[RequireComponent(typeof(Collider))]
public class WorldItem : MonoBehaviour, IInteractable
{
    [Header("Item Data")]
    public ItemData itemData;
    public int quantity = 1;

    [Header("Visual Effects")]
    public GameObject attachedVFX;

    [Header("Game State References")]
    public string inventoryVariableName = "Inventory";

    private bool isBeingPickedUp = false;
    private bool hasStarted = false;

    // --- NEW: Explicit Initialization Method ---
    public void Initialize(ItemData newItemData, int newQuantity)
    {
        itemData = newItemData;
        quantity = newQuantity;

        // Force a refresh immediately
        hasStarted = true;
        RegisterWithManager();
        PlaySpawnEffects();
    }
    // ------------------------------------------

    private void Start()
    {
        // Only run Start logic if Initialize wasn't called manually yet
        if (!hasStarted)
        {
            hasStarted = true;
            RegisterWithManager();
            PlaySpawnEffects();
        }
    }

    private void OnEnable()
    {
        if (hasStarted)
        {
            RegisterWithManager();
            PlaySpawnEffects();
        }
    }

    private void PlaySpawnEffects()
    {
        if (attachedVFX == null) return;

        Color vfxColor = Color.white;
        // Ensure we check for nulls to prevent errors if data is missing
        if (LootLabelManager.instance != null && itemData != null)
        {
            vfxColor = LootLabelManager.instance.GetRarityColor(itemData);
        }

        attachedVFX.SetActive(true);

        VisualEffect vfx = attachedVFX.GetComponent<VisualEffect>();
        if (vfx != null)
        {
            if (vfx.HasVector4("Colour")) vfx.SetVector4("Colour", vfxColor);
            vfx.Reinit();
            vfx.Play();
            return;
        }

        ParticleSystem ps = attachedVFX.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.startColor = vfxColor;
            ps.Clear();
            ps.Play();
        }
    }

    private void RegisterWithManager()
    {
        if (LootLabelManager.instance != null) LootLabelManager.instance.RegisterOrUpdateItem(this);
    }

    private void OnDestroy()
    {
        if (LootLabelManager.instance != null) LootLabelManager.instance.UnregisterItem(this);
    }

    private void OnDisable()
    {
        if (LootLabelManager.instance != null) LootLabelManager.instance.UnregisterItem(this);
    }

    public void Interact(GameObject interactor)
    {
        if (isBeingPickedUp) return;
        isBeingPickedUp = true;

        if (InventoryManager.instance != null)
        {
            bool success = InventoryManager.instance.HandleLoot(itemData, quantity);
            if (success)
            {
                Destroy(gameObject);
                return;
            }
        }

        AddToSpecificInventory(interactor);
    }

    private void AddToSpecificInventory(GameObject interactor)
    {
        if (interactor == null) { isBeingPickedUp = false; return; }

        object inventoryVar = Variables.Object(interactor).Get(inventoryVariableName);
        Inventory playerInventory = null;

        if (inventoryVar is GameObject inventoryGO) playerInventory = inventoryGO.GetComponent<Inventory>();
        else if (inventoryVar is Inventory inventoryComp) playerInventory = inventoryComp;

        if (playerInventory != null && playerInventory.AddItem(itemData, quantity))
        {
            if (InventoryUIController.instance != null && PartyManager.instance != null && interactor == PartyManager.instance.ActivePlayer)
            {
                InventoryUIController.instance.RefreshAllPlayerDisplays(PartyManager.instance.ActivePlayer);
            }
            Destroy(gameObject);
        }
        else
        {
            Debug.Log("Inventory is full or missing.");
            isBeingPickedUp = false;
        }
    }

    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger) col.isTrigger = true;
    }
}