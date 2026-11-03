# Hausi

Wear a trophy like gear, and the creatures of its biome give you more. Their trophy stops dropping
while you do.

Hausi is Old Norse for a skull. Every player ends up with a chest of trophies that nothing asks for,
and the game's only use for most of them is a shelf. This gives them a job without adding a resource,
a menu or a socket system, and the trophy block is what keeps it honest. The bonus is paid for in the
one thing the bigger bonus is made of, so you choose between farming a biome and finishing its set.

## How it works

A trophy slot sits beside the trinket. Right click a trophy and it gets the same checkmark as gear, and
it hangs at your left hip where you can see it and everyone else can too. A stack is worn whole.

| Worn | Chance of one more, per dropped unit | Reads as |
| --- | --- | --- |
| One trophy | 25% | 1.25x |
| That biome's sash | 50% | 1.5x |
| Upgraded sash | every time | 2x |

- **Its own biome only.** A Black Forest trophy does nothing to a Boar, and a wrong-biome trophy blocks
  nothing either.
- **Each unit rolls by itself.** A Boar dropping 2 meat rolls twice. Trophies are never boosted, and
  while a matching item is worn that kill drops no trophy.
- **The last blow decides.** Your slot changes your kills. Nobody else's drops are touched, and yours
  are not touched by theirs, whoever's machine the creature happens to belong to. A tame's or a trap's
  kill is nobody's and stays as the game made it.
- **Trophies and sashes do not wear out.** The block is the price.

### Which biome

Read off the game's own tables, not a list here, so a mod's creatures land in the right place for free.
A creature's biome is its own trophy's. A creature with no trophy, like a Greyling, has none and gets no
boost, because the alternatives were worse: the biome it died in would make a stray Greydwarf a Meadows
creature.

The tables cannot see a creature that only stands in a location or a dungeon. Seven ordinary ones are
named in `BiomeOverrides` by default (Fuling shaman, Growth, Ulv, Cultist, Rancid Remains, Surtling, Charred
Warlock). Hildir's quest bosses and the rest are left out: worn, they do nothing.
`hausi sets` in the console lists every set and everything left over.

### The sash

Every trophy of a biome plus its boss's, at a workbench, makes that biome's sash, worn in the same slot.
Seven exist: Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, Ashlands. A sash needs a boss
trophy, the Ocean has no boss and the Frozen King drops no trophy, so those two biomes have trophies but no
sash.

Offer at a boss's altar wearing that biome's sash and it comes back harder, and the sash is not used up.
Harder means Vandi's stars and nothing else: this mod asks and Vandi decides, so there is no second
difficulty system. That fight drops a relic, which with a material from the same biome upgrades the sash
at a level 2 workbench to the true double, using the item's quality as vanilla does.

The Queen counts. She stands in her room the first time, and comes back to an offering of 3 Seeker Soldier
trophies at the altar there.

[Vandi](https://github.com/Ezomic/valheim-vandi) is optional and is recommended if you want the altar. The
altar needs a Vandi that has `AskHarder` and `IsHarderSummon` (LHM-87, not published yet). Without one, the slot still boosts drops,
offerings summon the plain boss, and no relic drops.

### The Infusion Table (proof recipe)

Hausi carries the suite's shared Infusion Table, built from the Hammer for 10 Wood, 8 Stone and 4 Flint
beside a workbench. It has one recipe, there only so the table can be tested: a Boar trophy and a Deer Hide,
with the basin filled, make the Meadows relic. This is a proof and not a feature. The relic normally comes
from the harder boss fight, so the recipe lets a player skip it, and it is expected to change before release.

The table opens its own window rather than the workbench's recipe list (Core's `InfusionWindow.cs`, LHM-95).
Right-click the Boar trophy and the Deer Hide in your inventory to put them in the Base and Infusing slots,
fill the basin by using Resin, a Greydwarf Eye or Guck on the table, and press Infuse. Empty basin tips out
a wrong fill, and a click on a slot takes its item back out.

The table is a prefab, so a client without the mod cannot join a server that has it.

## Installing

Needs BepInEx. Nothing else. Through a mod manager it is one install. By hand, put `Hausi.dll` and every
`.obj` and `.png` beside it in `BepInEx/plugins/Hausi/`.

Then start the game once and quit. That first run writes the config file. It does not exist before the mod
has loaded, which is the usual reason people think it is broken.

**Uninstalling leaves a stale worn mark.** A trophy or sash that was worn when you removed Hausi keeps its
checkmark in the save, and the game treats a checked item as worn gear, so on death it stays in your pack
instead of going to the grave. Take it off before uninstalling.

**Prefab names are permanent.** Removing Hausi from a server deletes every sash and relic in every pack and
chest that holds one the next time it loads. Trophies are vanilla and are safe.

## Settings

The file is `BepInEx/config/ezomic.valheim.hausi.cfg`. Open it in any text editor. Every setting has a
comment above it, so the file explains itself.

Note that changing a default in a new version does nothing on a machine that has already run the mod.
BepInEx writes every entry on first run and the saved value wins.

## Multiplayer

**Everyone needs it.** The server refuses a client that does not have it, at the same build. It registers
prefabs, and a creature's drops are decided on whichever machine owns that creature, reading what the
killer wears off the killer's own player data.

If [Core](https://github.com/Ezomic/valheim-core) is installed, this mod registers with its version gate and
the host's settings apply to everyone connected to it, in memory only - your own config file is never
written to and comes back the moment you disconnect. Where a trophy hangs on you and your logging are yours
and stay yours. Without Core the mod still runs; what is lost is the enforcement.

**Trust model.** What a player wears, and its quality, is written by that player's own client and read by
whichever machine owns the creature. The server has no copy of a pack to check it against, so a modified
client can claim a sash it does not own. The mod only accepts a trophy or one of its own sashes there and
only boosts that player's own kills, which is the whole of what a cheater gains.

## Building

The project compiles shared source from the sibling [Core](https://github.com/Ezomic/valheim-core) repo, so
`core` must be cloned beside this one (`..\core`) before `dotnet build` works. Release builds carry no
console command; the `hausi` command that the scenarios use exists in Debug builds only.

## Bugs and ideas

Both go to the site. [longhouse.thijssensoftware.nl/bugs](https://longhouse.thijssensoftware.nl/bugs)
is for anything broken, and [longhouse.thijssensoftware.nl/ideas](https://longhouse.thijssensoftware.nl/ideas)
is for what a mod should do next. You can vote on other people's ideas there as well.

Signing in takes a Steam or Discord account. I work from that list, so the votes decide what
I pick up next.

## Licence

MIT. See `LICENSE`.
