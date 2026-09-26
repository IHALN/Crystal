using System;
using System.IO;
using System.Linq;

public sealed class EquipmentDropEntry
{
    public int ItemIndex { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public ItemType ItemType { get; set; }
    public string MapFile { get; set; } = string.Empty;
    public string MonsterName { get; set; } = string.Empty;
    public RequiredClass RequiredClass { get; set; }
    public RequiredType RequiredType { get; set; }
    public byte RequiredAmount { get; set; }
    public int Observations { get; set; }
    public DateTime LastSeenUtc { get; set; }
}

public sealed class EquipmentDropMemoryBank : MemoryBankBase<EquipmentDropEntry>
{
    public EquipmentDropMemoryBank(string path)
        : base(path, "Global\\EquipmentDropMemoryBankMutex")
    {
    }

    public void RecordMonsterDrop(int itemIndex, string itemName, ItemType itemType,
        string mapFile, string monsterName, RequiredClass requiredClass,
        RequiredType requiredType, byte requiredAmount)
    {
        if (itemIndex <= 0 ||
            string.IsNullOrWhiteSpace(itemName) ||
            string.IsNullOrWhiteSpace(mapFile) ||
            string.IsNullOrWhiteSpace(monsterName))
            return;

        string normalizedMap = Path.GetFileNameWithoutExtension(mapFile);
        lock (_lock)
        {
            ReloadIfUpdated();
            var entry = _entries.FirstOrDefault(e =>
                e.ItemIndex == itemIndex &&
                string.Equals(e.MapFile, normalizedMap, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.MonsterName, monsterName, StringComparison.OrdinalIgnoreCase));

            DateTime now = DateTime.UtcNow;
            bool newObservation = entry == null ||
                now - entry.LastSeenUtc >= TimeSpan.FromSeconds(10);

            if (entry == null)
            {
                entry = new EquipmentDropEntry
                {
                    ItemIndex = itemIndex,
                    ItemName = itemName,
                    ItemType = itemType,
                    MapFile = normalizedMap,
                    MonsterName = monsterName
                };
                _entries.Add(entry);
            }

            entry.ItemName = itemName;
            entry.ItemType = itemType;
            entry.RequiredClass = requiredClass;
            entry.RequiredType = requiredType;
            entry.RequiredAmount = requiredAmount;

            // Many agents can see the same floor object. Count it only once
            // during the observation window.
            if (newObservation)
            {
                entry.Observations++;
                entry.LastSeenUtc = now;
                Save();
            }
        }
    }
}
