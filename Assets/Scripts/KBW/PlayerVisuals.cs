using UnityEngine;

public class PlayerVisuals : MonoBehaviour
{
    [SerializeField] private GameObject characterA;
    [SerializeField] private GameObject characterB;

    [SerializeField] private Animator animatorA;
    [SerializeField] private Animator animatorB;

    [Header("Weapon Muzzle")]
    [SerializeField] private Transform muzzleA;
    [SerializeField] private Transform muzzleB;

    private byte _lastApplied = 255;

    public void Refresh(byte characterId)
    {
        if (_lastApplied == characterId) return;
        _lastApplied = characterId;

        if (characterA) characterA.SetActive(characterId == 0);
        if (characterB) characterB.SetActive(characterId == 1);
    }

    public Animator GetActiveAnimator(byte characterId)
    {
        return characterId == 0 ? animatorA : animatorB;
    }

    public Transform GetActiveMuzzle(byte characterId)
    {
        if (characterId == 0)
            return muzzleA;

        if (characterId == 1)
            return muzzleB;

        return null;
    }
}