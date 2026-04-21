using UnityEngine;

public class PlayerVisuals : MonoBehaviour
{
    [SerializeField] private GameObject characterA;
    [SerializeField] private GameObject characterB;

    private byte _lastApplied = 255;

    public void Refresh(byte characterId)
    {
        if (_lastApplied == characterId) return;
        _lastApplied = characterId;

        if (characterA) characterA.SetActive(characterId == 0);
        if (characterB) characterB.SetActive(characterId == 1);
    }
}