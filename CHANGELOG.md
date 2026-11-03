# Changelog

## 1.0.0 - unreleased

The Infusion Table is Debug only. Its proof recipe (a Boar trophy and a Deer Hide make the Meadows relic) lets
a player skip the harder boss fight, so a release ships no table, no recipe and no `piece_ezomic_infusion`
prefab, and the csproj links Core's `InfusionTable.cs` and `InfusionWindow.cs` and their Unity UI references in
Debug only. Debug builds keep it because `scenarios/hausi-infusion-table.txt` tests the shared table and its
window. Release prep: version 1.0.0, a package icon, and an entry in the packaging scripts.

First version. Trophies finally do something: right click one and it is worn in a slot of its own, hanging
at your hip where other players can see it, and the creatures of its biome give more while it is. One trophy
is 1.25x, that biome's sash is 1.5x and an upgraded sash is a true double, rolled per dropped unit. While a
matching item is worn that kill drops no trophy, which is the price, and only the player landing the last
blow is affected.

Seven sashes, each crafted from every trophy of its biome plus its boss's. Offering at a boss's altar in that
biome's sash asks Vandi for a harder fight, and that fight drops the relic that upgrades the sash. The altar
half needs a Vandi that has `AskHarder` and `IsHarderSummon`; without one, offerings stay plain and the rest
works. The Queen's room has an altar too, where she is offered 3 Seeker Soldier trophies after the first fight.

Two things in the build are worth a future reader's time. The killer's worn item is read off the killer's own
ZDO and never off the local slot, because the creature's owner is often not the killer: reading the local slot
passes every single-player test and hands out the wrong player's bonus on a server. And the worn model is
placed through the body mesh's bind poses, because reading the bone's live rotation left a trophy put on
mid-stride hanging at that angle for good.
