using UnityEngine;

// Central controller for cursor visibility, lock mode, and gameplay input blocking.
public class CursorController : MonoBehaviour
{
    public static CursorController Instance { get; private set; }

    // Cursor modes used by menus, gameplay, and interactive UI screens.
    public enum CursorState
    {
        Menu,
        Gameplay,
        UI
    }

    public CursorState CurrentState { get; private set; } = CursorState.Menu;

    // Gameplay input is blocked whenever the cursor is not locked for play.
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

    // Shows and unlocks the cursor for menu navigation.
    public void SetMenu()
    {
        CurrentState = CursorState.Menu;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // Hides and locks the cursor during active combat.
    public void SetGameplay()
    {
        CurrentState = CursorState.Gameplay;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Shows the cursor for in-match UI such as augment selection.
    public void SetUI()
    {
        CurrentState = CursorState.UI;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}