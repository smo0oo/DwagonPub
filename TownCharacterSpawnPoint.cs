using UnityEngine;

public class TownCharacterSpawnPoint : MonoBehaviour
{
    [Tooltip("The index of the party member who should spawn here (e.g., 1 for the second party member, 2 for the third). Do not use 0.")]
    public int partyMemberIndex;
}