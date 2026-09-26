using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using C = ClientPackets;

public sealed partial class GameClient
{
    public const string GuildHornName = "WoomaHorn";
    public const string GuildBossName = "WoomaTaurus";
    public const string GuildAdministratorName = "Administrator";
    public const string GuildTravelBoardName = "BichonWall_Board";
    public const string GuildAdministratorMap = "0122";
    public const uint GuildCreationGold = 1_000_000;
    public const ushort GuildCreationLevel = 22;

    private string _guildName = string.Empty;
    private int _guildRankId = -1;
    private int _guildMemberCount;
    private bool _guildInvitesEnabled;
    private readonly HashSet<string> _guildMembers = new(StringComparer.OrdinalIgnoreCase);

    public string GuildName => _guildName;
    public bool IsInGuild => !string.IsNullOrWhiteSpace(_guildName);
    public bool IsGuildLeader => IsInGuild && _guildRankId == 0;
    public int GuildMemberCount => _guildMemberCount;
    public IReadOnlyCollection<string> GuildMembers => _guildMembers;
    private static string NormalizeGuildToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
            if (char.IsLetterOrDigit(ch))
                builder.Append(char.ToUpperInvariant(ch));
        return builder.ToString();
    }

    private static bool IsWoomaHorn(ItemInfo? info)
    {
        if (info == null)
            return false;

        string expected = NormalizeGuildToken(GuildHornName);
        return NormalizeGuildToken(info.FriendlyName) == expected ||
               NormalizeGuildToken(info.Name).StartsWith(expected, StringComparison.Ordinal);
    }

    public bool HasWoomaHorn
    {
        get
        {
            if (_inventory == null)
                return false;

            foreach (var item in _inventory)
            {
                if (item == null)
                    continue;
                if (IsWoomaHorn(item.Info))
                    return true;
                if (ItemInfoDict.TryGetValue(item.ItemIndex, out var indexedInfo) &&
                    IsWoomaHorn(indexedInfo))
                    return true;
            }

            return false;
        }
    }

    public bool CanCreateGuild =>
        !IsInGuild && Level >= GuildCreationLevel &&
        Gold >= GuildCreationGold && HasWoomaHorn;

    internal async Task EnsureGuildInvitesEnabledAsync()
    {
        if (IsInGuild || _guildInvitesEnabled)
            return;

        await SendChatAsync("@allowguild");
        _guildInvitesEnabled = true;
    }

    public Task InviteToGuildAsync(string playerName)
    {
        if (!IsGuildLeader || string.IsNullOrWhiteSpace(playerName))
            return Task.CompletedTask;

        return SendAsync(new C.EditGuildMember
        {
            ChangeType = 0,
            Name = playerName,
            RankName = string.Empty,
            RankIndex = 0
        });
    }

    internal Task AcceptGuildInviteAsync(bool accept)
    {
        if (IsInGuild)
            accept = false;
        return SendAsync(new C.GuildInvite { AcceptInvite = accept });
    }

    internal Task RequestGuildMembersAsync()
    {
        if (!IsInGuild)
            return Task.CompletedTask;
        return SendAsync(new C.RequestGuildInfo { Type = 1 });
    }

    internal void UpdateGuildStatus(string name, int rankId, int memberCount)
    {
        _guildName = name;
        _guildRankId = rankId;
        _guildMemberCount = memberCount;
        if (!IsInGuild)
        {
            _guildMembers.Clear();
            _guildInvitesEnabled = false;
        }
    }

    internal void ReplaceGuildMembers(IEnumerable<GuildRank> ranks)
    {
        _guildMembers.Clear();
        foreach (var member in ranks.SelectMany(rank => rank.Members).Where(member => member.Online))
            _guildMembers.Add(member.Name);
        _guildMembers.Add(PlayerName);
        _guildMemberCount = Math.Max(_guildMemberCount, _guildMembers.Count);
    }

    public async Task<bool> UseHomeForGuildAsync()
    {
        if (_stream == null)
            return false;

        string previousMap = CurrentMapFile;
        Log($"Guild route: sending @Home from {Path.GetFileNameWithoutExtension(previousMap)}");
        UpdateAction("guild creation: using @Home");

        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
        var changed = WaitForMapChangeAsync(waitForNextMap: true, cts.Token);
        await SendChatAsync("@Home");

        try
        {
            await changed;
            Log($"Guild route: @Home arrived on {Path.GetFileNameWithoutExtension(CurrentMapFile)}");
            return !string.Equals(previousMap, CurrentMapFile, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            Log("Guild route: @Home did not change map within 15 seconds");
            return false;
        }
    }

    public async Task<bool> TravelToGuildPalaceAtBoardAsync(uint npcId)
    {
        if (_stream == null || npcId == 0)
            return false;

        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
            var interaction = new NPCInteraction(this, npcId);

            Log($"Guild route: opening {GuildTravelBoardName} object {npcId}");
            var mainPage = await interaction.BeginAsync(cts.Token);
            var useButton = mainPage.Buttons.FirstOrDefault(button =>
                button.Key.Equals("@main-1", StringComparison.OrdinalIgnoreCase) ||
                button.Text.Equals("Use", StringComparison.OrdinalIgnoreCase));

            if (useButton == null)
            {
                Log($"Guild route: {GuildTravelBoardName} has no Use/@main-1 button");
                return false;
            }

            Log($"Guild route: selecting {useButton.Key} (Use)");
            var destinationPage = await interaction.SelectAsync(useButton.Key, cts.Token);
            var palaceButton = destinationPage.Buttons.FirstOrDefault(button =>
                button.Key.Equals("@go-palace", StringComparison.OrdinalIgnoreCase) ||
                button.Text.Contains("Bichon Inner Wall", StringComparison.OrdinalIgnoreCase));

            if (palaceButton == null)
            {
                Log($"Guild route: @main-1 has no Bichon Inner Wall/@go-palace button");
                return false;
            }

            Log($"Guild route: selecting {palaceButton.Key} (Bichon Inner Wall)");
            var mapChanged = WaitForMapChangeAsync(waitForNextMap: true, cts.Token);
            await CallNPCAsync(npcId, palaceButton.Key);
            await mapChanged;

            string arrivedMap = Path.GetFileNameWithoutExtension(CurrentMapFile);
            Log($"Guild route: board arrived on map {arrivedMap}");
            return string.Equals(arrivedMap, GuildAdministratorMap, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            Log($"Guild route: timed out using {GuildTravelBoardName}");
            return false;
        }
    }

    public async Task<bool> CreateGuildAtNpcAsync(uint npcId)
    {
        if (!CanCreateGuild)
        {
            Log($"Guild creation blocked: guild='{GuildName}', level={Level}, gold={Gold}, horn={HasWoomaHorn}");
            return false;
        }

        if (npcId == 0)
        {
            Log("Guild creation blocked: Administrator object ID was not resolved");
            return false;
        }

        UpdateAction("creating guild...");
        Log($"Opening {GuildAdministratorName} object {npcId} before guild creation");

        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
            var interaction = new NPCInteraction(this, npcId);
            var page = await interaction.BeginAsync(cts.Token);
            var createButton = page.Buttons.FirstOrDefault(button =>
                button.Key.Equals("@CREATEGUILD", StringComparison.OrdinalIgnoreCase));

            if (createButton == null)
            {
                Log($"{GuildAdministratorName} object {npcId} does not advertise @CREATEGUILD");
                return false;
            }

            Log($"Selecting {createButton.Key} on {GuildAdministratorName} object {npcId}");
            await CallNPCAsync(npcId, createButton.Key);
            return true;
        }
        catch (OperationCanceledException)
        {
            Log($"Timed out opening {GuildAdministratorName} object {npcId}");
            return false;
        }
    }

    internal async Task SubmitGuildNameAsync()
    {
        if (IsInGuild || !HasWoomaHorn)
            return;

        var builder = new StringBuilder("Guild");
        foreach (char ch in PlayerName)
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            if (builder.Length >= 20)
                break;
        }

        string name = builder.ToString();
        if (name.Length < 3)
            name = $"Guild{ObjectId}";

        await SendAsync(new C.GuildNameReturn { Name = name });
        Log($"Submitted guild name {name}");
    }
}
