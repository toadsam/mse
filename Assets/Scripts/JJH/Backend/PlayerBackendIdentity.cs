using UnityEngine;

// file: Assets/Scripts/JJH/Backend/PlayerBackendIdentity.cs
// Component that tags a scene object with the backend user identity it represents.
public class PlayerBackendIdentity : MonoBehaviour
{
    [SerializeField] private long backendUserId;
    [SerializeField] private string backendNickname;

    public long BackendUserId => backendUserId;
    public string BackendNickname => backendNickname;
    public bool HasBackendUser => backendUserId > 0;

    // Binds an explicit backend user identity to this object.
    public void SetBackendUser(long userId, string nickname)
    {
        backendUserId = userId;
        backendNickname = nickname ?? string.Empty;
    }

    // Copies the current logged-in backend user into this component for local ownership tagging.
    public void SetCurrentLoggedInUser()
    {
        if (!BackendSession.IsLoggedIn)
        {
            Clear();
            return;
        }

        SetBackendUser(BackendSession.UserId, BackendSession.Nickname);
    }

    // Resets the identity so this object no longer points at a backend user.
    public void Clear()
    {
        backendUserId = 0;
        backendNickname = string.Empty;
    }
}
