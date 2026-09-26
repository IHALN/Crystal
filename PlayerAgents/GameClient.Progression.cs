using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public sealed partial class GameClient
{
    private static string NormalizeDropName(string value)
    {
        return new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    public bool IsSpellBookDrop(string droppedName)
    {
        string normalized = NormalizeDropName(droppedName);
        if (normalized.Length == 0)
            return false;

        return ItemInfoDict.Values.Any(info =>
            info.Type == ItemType.Book &&
            (NormalizeDropName(info.Name) == normalized ||
             NormalizeDropName(info.FriendlyName) == normalized));
    }

    public async Task SendHomeForInactivityAsync()
    {
        Log("Position unchanged for 60 seconds; forcing @HOME");
        StopMovement();
        await SendChatAsync("@HOME");
    }

    public bool IsExperienceTenTimesSlower(out double currentRate, out double bestKnownRate)
    {
        currentRate = 0;
        bestKnownRate = 0;

        if (_playerClass == null ||
            string.IsNullOrEmpty(_trackedMapFile) ||
            _mapExpPaused ||
            _mapStartTime == DateTime.MinValue)
            return false;

        TimeSpan elapsed = _mapElapsedBeforePause + (DateTime.UtcNow - _mapStartTime);
        if (elapsed < TimeSpan.FromMinutes(5))
            return false;

        currentRate = _mapExpGained / elapsed.TotalHours;
        bestKnownRate = _expRateMemory.GetAll()
            .Where(e => e.Class == _playerClass.Value &&
                        e.Level == _level &&
                        e.ExpPerHour > 0)
            .Select(e => e.ExpPerHour)
            .DefaultIfEmpty(0)
            .Max();

        return bestKnownRate > 0 && currentRate <= bestKnownRate / 10d;
    }

    private bool HasBookItem(int bookIndex)
    {
        bool ContainsBook(IEnumerable<UserItem?>? items)
        {
            return items != null && items.Any(item =>
                item != null &&
                (item.Info?.Index ?? item.ItemIndex) == bookIndex);
        }

        return ContainsBook(_inventory) ||
               ContainsBook(_storage) ||
               ContainsBook(_pendingStorage);
    }

    public bool TryGetBookHuntMap(
        out string? mapFile,
        out string? bookName,
        IReadOnlyCollection<string>? excludedMaps = null)
    {
        mapFile = null;
        bookName = null;
        if (_playerClass == null)
            return false;

        RequiredClass playerClass = _playerClass.Value switch
        {
            MirClass.Warrior => RequiredClass.Warrior,
            MirClass.Wizard => RequiredClass.Wizard,
            MirClass.Taoist => RequiredClass.Taoist,
            MirClass.Assassin => RequiredClass.Assassin,
            MirClass.Archer => RequiredClass.Archer,
            _ => RequiredClass.None
        };

        var excluded = excludedMaps == null
            ? null
            : new HashSet<string>(excludedMaps, StringComparer.OrdinalIgnoreCase);

        var eligibleDrops = _bookDropMemory.GetAll()
            .Where(entry =>
                // Ignore incomplete records created before full book metadata
                // was added. Only verified monster book sightings can hunt.
                entry.BookIndex > 0 &&
                entry.Spell > 0 &&
                entry.Observations > 0 &&
                !string.IsNullOrWhiteSpace(entry.BookName) &&
                !string.IsNullOrWhiteSpace(entry.MapFile) &&
                !string.IsNullOrWhiteSpace(entry.MonsterName) &&
                (excluded == null || !excluded.Contains(entry.MapFile)) &&
                (entry.RequiredClass == RequiredClass.None ||
                 entry.RequiredClass.HasFlag(playerClass)) &&
                (entry.RequiredType != RequiredType.Level ||
                 _level >= entry.RequiredAmount) &&
                !HasMagic((Spell)entry.Spell) &&
                !HasBookItem(entry.BookIndex))
            .ToList();

        var selected = eligibleDrops
            .GroupBy(entry => new { entry.BookIndex, entry.BookName, entry.MapFile })
            .Select(group => new
            {
                group.Key.BookName,
                group.Key.MapFile,
                RequiredLevel = group.Min(entry => entry.RequiredAmount),
                Observations = group.Sum(entry => Math.Max(1, entry.Observations))
            })
            .OrderBy(candidate => candidate.RequiredLevel)
            .ThenByDescending(candidate => candidate.Observations)
            .FirstOrDefault();

        if (selected == null || string.IsNullOrWhiteSpace(selected.MapFile))
            return false;

        mapFile = selected.MapFile;
        bookName = selected.BookName;
        return true;
    }

    private static bool IsEquipmentHuntType(ItemType type)
    {
        return type == ItemType.Ring ||
               type == ItemType.Bracelet ||
               type == ItemType.Necklace ||
               type == ItemType.Weapon ||
               type == ItemType.Armour ||
               type == ItemType.Stone ||
               type == ItemType.Helmet ||
               type == ItemType.Belt ||
               type == ItemType.Boots;
    }

    private bool HasEquipmentItem(int itemIndex)
    {
        bool ContainsItem(IEnumerable<UserItem?>? items)
        {
            if (items == null)
                return false;

            foreach (var item in items)
            {
                if (item == null)
                    continue;
                if ((item.Info?.Index ?? item.ItemIndex) == itemIndex)
                    return true;
                if (ContainsItem(item.Slots))
                    return true;
            }

            return false;
        }

        return ContainsItem(_inventory) ||
               ContainsItem(_equipment) ||
               ContainsItem(_storage) ||
               ContainsItem(_pendingStorage);
    }

    public bool TryGetEquipmentHuntMap(
        out string? mapFile,
        out string? itemName,
        out string? monsterName,
        IReadOnlyCollection<string>? excludedMaps = null)
    {
        mapFile = null;
        itemName = null;
        monsterName = null;
        if (_playerClass == null)
            return false;

        RequiredClass playerClass = _playerClass.Value switch
        {
            MirClass.Warrior => RequiredClass.Warrior,
            MirClass.Wizard => RequiredClass.Wizard,
            MirClass.Taoist => RequiredClass.Taoist,
            MirClass.Assassin => RequiredClass.Assassin,
            MirClass.Archer => RequiredClass.Archer,
            _ => RequiredClass.None
        };

        var eligible = _equipmentDropMemory.GetAll()
            .Where(entry =>
                entry.ItemIndex > 0 &&
                entry.Observations > 0 &&
                IsEquipmentHuntType(entry.ItemType) &&
                !string.IsNullOrWhiteSpace(entry.ItemName) &&
                !string.IsNullOrWhiteSpace(entry.MapFile) &&
                !string.IsNullOrWhiteSpace(entry.MonsterName) &&
                (entry.RequiredClass == RequiredClass.None ||
                 entry.RequiredClass.HasFlag(playerClass)) &&
                entry.RequiredType == RequiredType.Level &&
                _level >= entry.RequiredAmount)
            .ToList();

        if (eligible.Count == 0)
            return false;

        // Fix the target tier before ownership and route checks. This prevents
        // a collected or unreachable top-tier item from making the bot fall
        // back and lock onto a lower-level item.
        byte highestRequiredLevel = eligible.Max(entry => entry.RequiredAmount);
        var highestTier = eligible
            .Where(entry => entry.RequiredAmount == highestRequiredLevel)
            .ToList();

        var unownedItemIndexes = highestTier
            .Select(entry => entry.ItemIndex)
            .Distinct()
            .Where(index => !HasEquipmentItem(index))
            .ToHashSet();

        if (unownedItemIndexes.Count == 0)
            return false;

        var excluded = excludedMaps == null
            ? null
            : new HashSet<string>(excludedMaps, StringComparer.OrdinalIgnoreCase);

        var selected = highestTier
            .Where(entry =>
                unownedItemIndexes.Contains(entry.ItemIndex) &&
                (excluded == null || !excluded.Contains(entry.MapFile)))
            .GroupBy(entry => new
            {
                entry.ItemIndex,
                entry.ItemName,
                entry.MapFile,
                entry.MonsterName
            })
            .Select(group => new
            {
                group.Key.ItemName,
                group.Key.MapFile,
                group.Key.MonsterName,
                Observations = group.Sum(entry => Math.Max(1, entry.Observations)),
                LastSeenUtc = group.Max(entry => entry.LastSeenUtc)
            })
            .OrderByDescending(candidate => candidate.Observations)
            .ThenByDescending(candidate => candidate.LastSeenUtc)
            .FirstOrDefault();

        if (selected == null)
            return false;

        mapFile = selected.MapFile;
        itemName = selected.ItemName;
        monsterName = selected.MonsterName;
        return true;
    }

    public string? GetExpandedExplorationMap()
    {
        if (_playerClass == null)
            return null;

        var entries = _expRateMemory.GetAll();
        var maps = new HashSet<string>(
            entries.Select(e => Path.GetFileNameWithoutExtension(e.MapFile)),
            StringComparer.OrdinalIgnoreCase);

        foreach (string map in _movementMemory.GetKnownMaps())
            maps.Add(Path.GetFileNameWithoutExtension(map));

        string currentMap = Path.GetFileNameWithoutExtension(_currentMapFile);
        maps.RemoveWhere(m => string.IsNullOrWhiteSpace(m) ||
                              string.Equals(m, currentMap, StringComparison.OrdinalIgnoreCase));

        var minimumLevels = entries
            .GroupBy(e => Path.GetFileNameWithoutExtension(e.MapFile), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Min(e => e.Level), StringComparer.OrdinalIgnoreCase);

        var safeCandidates = maps
            .Where(map => !minimumLevels.TryGetValue(map, out ushort minimumLevel) ||
                          minimumLevel <= _level + ExplorationLevelMargin)
            .ToList();

        if (safeCandidates.Count == 0)
            return null;

        var testedAtThisLevel = new HashSet<string>(
            entries.Where(e => e.Class == _playerClass.Value && e.Level == _level)
                   .Select(e => Path.GetFileNameWithoutExtension(e.MapFile)),
            StringComparer.OrdinalIgnoreCase);

        var untested = safeCandidates.Where(map => !testedAtThisLevel.Contains(map)).ToList();
        var pool = untested.Count > 0 ? untested : safeCandidates;
        return pool[_random.Next(pool.Count)];
    }
}
