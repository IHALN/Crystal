# Battlefield of Valor

A separate monument-control event on `valor.map`. It uses its own server controller,
packets, HUD, scoreboard, honor currency and NPC actions.

## Configure your server and client

1. Back up the server database before running this build. This Crystal fork's database
   version advances from 117 to 118 to store each character's Honor in the same record as
   inventory. Existing characters start at zero Honor. To roll back to an older
   server executable, restore the pre-upgrade database backup as well.
2. Put `valor.map` in the server/client map folders and add a map database entry with
   **FileName `valor`**. Any unused map database index works. All supplied coordinates
   must be walkable. The asset is supplied locally by the server owner, not this repo.
3. Leave `NoFight`, `NoTeleport` and `RequiredGroup` off. Enable `NoMount`,
   `NoRandom`, `NoRecall`, `NoEscape`, `NoTownTeleport`, `NoDropPlayer` and
   `NoDropMonster`. Set `NoReconnect` with return map `0`. Make small safe zones
   at the team spawns only; keep monument and monster wave locations outside safe zones.
   The event also prevents spawn zones from replacing a player's town bind.
4. Create the three monument **database indices** below. Set their `Image` values to
   350 for Sun, 349 for Moon, and 351 for Lightning. The event looks up indices
   575–577 and overrides their HP: Sun 300, Moon 100 and Lightning 100. Their own
   stationary AI takes one HP per successful physical hit, like `ChestnutTree`;
   they do not regenerate, attack, move or take poison damage. Also create monster
   templates with positive HP and
   the exact names in the wave list below, including `MinotaurKing`. The event uses
   normal monster AI for waves and bosses; event spawns do not award normal XP,
   drops, quests or death scripts. Do not add permanent map respawns for them.
5. Put the supplied `349.Lib`, `350.Lib` and `351.Lib` in the client's configured
   monster library directory. Their standing images are frames 0–1. Moon and Sun
   use frames 10–17 (neutral), 20–27 (Blue), and 30–37 (Red) for their looping
   ownership effect. Lightning uses 2–9 (neutral), 10–17 (Blue), and 18–25 (Red).
   Keep `Data/Title.Lib` (image 725) and `Data/Prguse2.Lib` (Valor HUD images
   969–1121) in the client. Update both Shared.dll and the
   server/client executables together because the new packet is used by both.
6. Place a new NPC named `Valor_Registration` on map `0`, at **327,258**, and assign
   the script below. Use your normal NPC script directory and editor FileName.
7. Copy `ValorSettings.json` beside the server executable. Configure reward items
   before adding reward links to the NPC script. Settings load when registration
   opens; do not edit them during a running event. Stop/restart an event to apply changes.

| Object | Database index | Position |
| --- | --- | --- |
| Blue spawn/respawn | — | 50,42 |
| Red spawn/respawn | — | 354,360 |
| SunMonument | 575 | 201,198 |
| MoonMonument | 576 | 77,196 |
| LightningMonument | 577 | 324,207 |
| Red side wave and MinotaurKing | Name lookup | Around 339,122 |
| Blue side wave and MinotaurKing | Name lookup | Around 64,281 |

The server requires one template for each name: `RedBoar`, `BlackBoar`, `Zombie1`,
`Zombie2`, `Zombie3`, `Zombie4`, `IceMinotaur`, `Minotaur`, `FlamingWooma`,
`WoomaSoldier`, `VioletKekTal`, `BlueHoroBlaster`, `GiantRat`, `WedgeMoth`, `Tongs`,
`BlackMaggot`, `GiantWorm`, `WhimperingBee`, and `MinotaurKing`. Monster database
indices 578 and 579 are no longer required. The regular monsters use available
walkable cells within 12 cells of each supplied side point; ensure room for 18
monsters outside safe zones on each side. The translated image refers to six
locations, but only the two side coordinates have been supplied; the wave uses
those two centers until the other precise coordinates are available.

## Registration NPC script

```text
[@MAIN]
Battlefield of Valor: control the three monuments.\
Registration lasts one minute. At least two players must register.\
<Open registration/@open>\
<Register/@register>\
<My Honor/@honor>\
<Close/@exit>

[@open]
#ACT
VALOROPEN

[@register]
#ACT
VALORREGISTER

[@honor]
#ACT
VALORHONOR
```

`VALOROPEN` opens registration without registering the speaker. Opening is available
to players through the configured NPC. Registration has no level restriction and
requires two connected, alive eligible players at countdown expiry. This Crystal fork
does not include the other flag battlefield; its cross-event exclusion checks have been
omitted. Team assignment balances size and total
levels. The default maximum is 100 participants.

GM command: **`@ValorStart`** opens the same one-minute registration window from
anywhere. Announcement is exactly:

> BattleField has just begun, you may register at the NPC in BichonWall

Player commands: **`@ValorLeave`** withdraws/leaves; **`@ValorHonor`** displays Honor.

## Match rules

- First to 7,500 faction points wins; after 20 minutes the higher score wins.
  Equal scores, including simultaneous threshold crossing, produce a draw. If a
  faction has no remaining participants, the event ends using the current scores.
- Monuments start neutral. Destroying a neutral/enemy monument gives ownership to
  the final hitter's faction. It reappears after one second at full configured HP.
  The owning faction cannot damage its monument. Ownership remains during reappearance.
- Every scoring tick, Sun grants 15 faction points; Moon and Lightning grant 10
  each. Two owned monuments add 35 bonus faction points; three add 50. The bonuses
  are additive: owning all three grants 85 faction points per tick before personal points.
- Living players near an owned monument earn personal points, which also add to
  faction score. A player earns once per tick even if multiple monument areas overlap.
  Dead players and players on other maps earn nothing. No backlog points are awarded
  after a server stall using the current ownership.
- At battle start, each side receives 18 regular monsters, one of each listed name.
  Their HUD number starts at 18 and drops on each kill. Once all 18 on a side die,
  `MinotaurKing` appears at that side's center. The boss does not respawn. A fresh
  wave starts at the 10 minute mark, replacing any monsters or boss left from the
  first wave; no additional waves spawn.
- The final hitter of a regular monster gains a random 1–3 personal points; no other
  player receives those points. Killing the boss awards 15 personal points and a
  five minute Valor buff to each living, nearby member of the killer's faction.
  Personal awards contribute once per recipient to faction score. Hero, pet and
  poison damage sources resolve to their owning player. Boss proximity defaults to
  8 cells (maximum axis distance) and is adjustable with `BufferRadius`.
- Death revives a player after two seconds at their team spawn, without the normal
  revival prompt, death drops, or murder penalties. No release NPC is needed.
- Attack mode is locked to Valor. Enemy factions can fight; faction allies cannot.
  General chat retains normal nearby range and is delivered to faction allies only.
  The existing shout channel remains visible to both teams.
- At completion, players return to their saved pre-event town bind. Leaving the map
  or logging out ends participation; rejoining an active match is not supported.
- The HUD shows faction scores, a timer, monument damage/health and the attacking
  faction's icon. Score fills reach full width at 7,500 points. Click the HUD to open
  the 15-row paginated scoreboard using `Title.Lib` 725. The scoreboard shows English
  headers and live personal score/kills/deaths. The two numbers beneath the team
  figures count remaining regular wave monsters on each side. End results display completion/victory
  bonuses and persistent Honor balances. Close the sheet with Escape or its top-right
  button. Update the client, server and Shared.dll together when the Valor packet changes.

## Adjustable defaults, not verified Korean values

The supplied translated guide/screenshot does **not** specify the scoring interval,
individual point amount/radius, buff values/proximity, exact Honor formula, reward
prices, tie handling or player respawn delay. The implementation uses:

| Setting | Default |
| --- | --- |
| Scoring interval | 5 seconds |
| Personal points | 1 per qualifying tick |
| Monument area | Maximum axis distance 8 cells (a square), not exact island geometry |
| Completion Honor | 50 |
| Victory Honor | 100 additional |
| Buffer stats | +10 min/max DC, MC, SC, AC and MAC |
| Boss buff range | 8 cells, configurable with `BufferRadius` |
| Buffer duration | 300 seconds; replaced on another boss kill, removed on death/exit |

Honor is **per character**, capped at 200,000. Completers receive personal score +
50, with another 100 for winners. Leavers receive personal score only. Kills/deaths
are recorded but do not independently award score/Honor. Faction ownership points
are not automatically credited to every individual's Honor. The Bonus scoreboard
column shows the pre-cap completion/victory bonus; Honor shows the actual balance.

## Reward exchange

The guide does not supply reward names or prices, so the initial reward list is empty.
Set `Rewards` in `ValorSettings.json`, for example using **your real ItemInfo name**:

```json
"Rewards": [ { "Item": "YourRewardItemName", "HonorCost": 1000 } ]
```

Then add a priced link to the NPC MAIN page and this action page:

```text
[@reward0]
#ACT
VALORREWARD 0
```

Indices are zero-based. Each exchange gives one fresh item, checks inventory capacity
and weight, and uses the configured server-side price. Honor and inventory are saved
through the same existing character database persistence.

## In-game verification

Testing requires your map/assets/database: register two players,
capture/re-capture all three monuments, verify score ticks and ally protection,
kill 18 regular monsters on each side, verify both boss rewards, check the second
wave at 10 minutes, die/respawn, leave/logout, complete a match and reconnect to
verify Honor, then test a configured reward with full/empty inventory.
