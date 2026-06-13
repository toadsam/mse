using UnityEngine;

// Switches character visuals, active Animator, and weapon muzzle by character id.
public class PlayerVisuals : MonoBehaviour
{
    [Header("Characters")]
    // Character roots that can be enabled for the selected character id.
    [SerializeField] private GameObject[] characters;

    [Header("Animators")]
    // Animator and muzzle arrays aligned with the character array.
    [SerializeField] private Animator[] animators;

    [Header("Weapon Muzzles")]
    [SerializeField] private Transform[] muzzles;

    // Last active character id used to avoid redundant refresh work.
    private byte lastApplied = 255;

    public int CharacterCount => characters != null ? characters.Length : 0;

    // Checks whether a character id has a valid visual prefab.
    public bool IsValidCharacterId(byte characterId)
    {
        return characters != null &&
               characterId < characters.Length &&
               characters[characterId] != null;
    }

    // Activates only the selected character visual.
    public void Refresh(byte characterId)
    {
        if (lastApplied == characterId)
            return;

        lastApplied = characterId;

        if (characters == null)
            return;

        Debug.Log($"[PlayerVisuals] Refresh CharacterId={characterId}, Count={CharacterCount}");

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null)
                characters[i].SetActive(i == characterId);
        }
    }

    // Returns the Animator that belongs to the selected character.
    public Animator GetActiveAnimator(byte characterId)
    {
        if (animators == null)
            return null;

        if (characterId >= animators.Length)
            return null;

        return animators[characterId];
    }

    // Returns the muzzle transform that belongs to the selected character.
    public Transform GetActiveMuzzle(byte characterId)
    {
        if (muzzles == null)
            return null;

        if (characterId >= muzzles.Length)
            return null;

        return muzzles[characterId];
    }
}