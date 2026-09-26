public sealed partial class GameClient
{
    public void RememberPendingSocialRequest(string targetName, string requestType)
    {
        _pendingSocialMemory.Remember(PlayerName, targetName, requestType);
    }

    public bool HasPendingSocialRequest(string targetName, string requestType)
    {
        return _pendingSocialMemory.Has(PlayerName, targetName, requestType);
    }

    public void ClearPendingSocialRequest(string targetName, string requestType)
    {
        _pendingSocialMemory.Clear(PlayerName, targetName, requestType);
    }
}
