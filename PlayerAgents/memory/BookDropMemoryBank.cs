using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public sealed class BookDropEntry
{
    public int BookIndex { get; set; }
    public string BookName { get; set; } = string.Empty;
    public string MapFile { get; set; } = string.Empty;
    public string MonsterName { get; set; } = string.Empty;
    public short Spell { get; set; }
    public RequiredClass RequiredClass { get; set; }
    public RequiredType RequiredType { get; set; }
    public byte RequiredAmount { get; set; }
    public int Observations { get; set; }
    public DateTime LastSeenUtc { get; set; }
}

public sealed class BookDropMemoryBank : MemoryBankBase<BookDropEntry>
{
    public BookDropMemoryBank(string path)
        : base(path, "Global\\BookDropMemoryBankMutex")
    {
    }

    public void RecordMonsterDrop(int bookIndex, string bookName, string mapFile, string monsterName,
        short spell, RequiredClass requiredClass, RequiredType requiredType, byte requiredAmount)
    {
        if (bookIndex <= 0 ||
            string.IsNullOrWhiteSpace(bookName) ||
            string.IsNullOrWhiteSpace(mapFile) ||
            string.IsNullOrWhiteSpace(monsterName))
            return;

        string normalizedMap = Path.GetFileNameWithoutExtension(mapFile);
        lock (_lock)
        {
            ReloadIfUpdated();
            var entry = _entries.FirstOrDefault(e =>
                e.BookIndex == bookIndex &&
                string.Equals(e.MapFile, normalizedMap, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.MonsterName, monsterName, StringComparison.OrdinalIgnoreCase));

            DateTime now = DateTime.UtcNow;
            bool newObservation = entry == null ||
                now - entry.LastSeenUtc >= TimeSpan.FromSeconds(10);

            if (entry == null)
            {
                entry = new BookDropEntry
                {
                    BookIndex = bookIndex,
                    BookName = bookName,
                    MapFile = normalizedMap,
                    MonsterName = monsterName
                };
                _entries.Add(entry);
            }

            entry.BookName = bookName;
            entry.Spell = spell;
            entry.RequiredClass = requiredClass;
            entry.RequiredType = requiredType;
            entry.RequiredAmount = requiredAmount;

            // Hundreds of agents can see the same floor object. Collapse their
            // reports into one observation instead of multiplying its weight.
            if (newObservation)
            {
                entry.Observations++;
                entry.LastSeenUtc = now;
                Save();
            }
        }
    }

    public IReadOnlyList<BookDropEntry> GetDropsForBook(int bookIndex)
    {
        lock (_lock)
        {
            ReloadIfUpdated();
            return _entries
                .Where(e => e.BookIndex == bookIndex)
                .OrderByDescending(e => e.Observations)
                .ThenByDescending(e => e.LastSeenUtc)
                .ToList();
        }
    }
}
