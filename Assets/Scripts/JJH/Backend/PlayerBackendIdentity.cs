using UnityEngine;

public class PlayerBackendIdentity : MonoBehaviour
{
    [SerializeField] private long backendUserId;
    [SerializeField] private string backendNickname;

    public long BackendUserId => backendUserId;
    public string BackendNickname => backendNickname;
    public bool HasBackendUser => backendUserId > 0;

    public void SetBackendUser(long userId, string nickname)
    {
        backendUserId = userId;
        backendNickname = nickname ?? string.Empty;
    }

    public void SetCurrentLoggedInUser()
    {
        if (!BackendSession.IsLoggedIn)
        {
            Clear();
            return;
        }

        SetBackendUser(BackendSession.UserId, BackendSession.Nickname);
    }

    public void Clear()
    {
        backendUserId = 0;
        backendNickname = string.Empty;
    }
}
