using UnityEngine;

public class PlayerVisuals : MonoBehaviour
{
    [Header("Characters")]
    [SerializeField] private GameObject[] characters;

    [Header("Animators")]
    [SerializeField] private Animator[] animators;

    [Header("Weapon Muzzles")]
    [SerializeField] private Transform[] muzzles;

    private byte lastApplied = 255;

    public int CharacterCount => characters != null ? characters.Length : 0;

    public bool IsValidCharacterId(byte characterId)
    {
        return characters != null &&
               characterId < characters.Length &&
               characters[characterId] != null;
    }

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

    public Animator GetActiveAnimator(byte characterId)
    {
        if (animators == null)
            return null;

        if (characterId >= animators.Length)
            return null;

        return animators[characterId];
    }

    public Transform GetActiveMuzzle(byte characterId)
    {
        if (muzzles == null)
            return null;

        if (characterId >= muzzles.Length)
            return null;

        return muzzles[characterId];
    }
}