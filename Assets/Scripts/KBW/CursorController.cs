using UnityEngine;

public class CursorController : MonoBehaviour
{
    public static CursorController Instance { get; private set; }

    public enum CursorState
    {
        Menu,
        Gameplay,
        UI
    }

    public CursorState CurrentState { get; private set; } = CursorState.Menu;

    public bool BlocksGameplayInput => CurrentState != CursorState.Gameplay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SetMenu()
    {
        CurrentState = CursorState.Menu;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void SetGameplay()
    {
        CurrentState = CursorState.Gameplay;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void SetUI()
    {
        CurrentState = CursorState.UI;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}