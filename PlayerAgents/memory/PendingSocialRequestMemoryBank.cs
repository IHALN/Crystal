using System;
using System.Collections.Generic;
using System.Linq;

public sealed class PendingSocialRequestEntry
{
    public string BotName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public DateTime RequestedUtc { get; set; }
}

public sealed class PendingSocialRequestMemoryBank : MemoryBankBase<PendingSocialRequestEntry>
{
    public PendingSocialRequestMemoryBank(string path)
        : base(path, "Global\\PendingSocialRequestMemoryBankMutex")
    {
    }

    public void Remember(string botName, string targetName, string requestType)
    {
        if (string.IsNullOrWhiteSpace(botName) ||
            string.IsNullOrWhiteSpace(targetName) ||
            string.IsNullOrWhiteSpace(requestType))
            return;

        lock (_lock)
        {
            ReloadIfUpdated();
            var entry = _entries.FirstOrDefault(e =>
                e.BotName.Equals(botName, StringComparison.OrdinalIgnoreCase) &&
                e.TargetName.Equals(targetName, StringComparison.OrdinalIgnoreCase) &&
                e.RequestType.Equals(requestType, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                entry = new PendingSocialRequestEntry
                {
                    BotName = botName,
                    TargetName = targetName,
                    RequestType = requestType
                };
                _entries.Add(entry);
            }

            entry.RequestedUtc = DateTime.UtcNow;
            Save();
        }
    }

    public bool Has(string botName, string targetName, string requestType)
    {
        lock (_lock)
        {
            ReloadIfUpdated();
            return _entries.Any(e =>
                e.BotName.Equals(botName, StringComparison.OrdinalIgnoreCase) &&
                e.TargetName.Equals(targetName, StringComparison.OrdinalIgnoreCase) &&
                e.RequestType.Equals(requestType, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Clear(string botName, string targetName, string requestType)
    {
        lock (_lock)
        {
            ReloadIfUpdated();
            int removed = _entries.RemoveAll(e =>
                e.BotName.Equals(botName, StringComparison.OrdinalIgnoreCase) &&
                e.TargetName.Equals(targetName, StringComparison.OrdinalIgnoreCase) &&
                e.RequestType.Equals(requestType, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
                Save();
        }
    }
}
