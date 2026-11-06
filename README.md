# AlwaysEitr

Gives you an eitr pool all the time, so you can use staffs, spells and summons without eating eitr food.

- **Food stacks on top.** Max eitr = pool (`BaseEitr`) + eitr from food. For example, 200 pool + 100 food = 300, which drops back to 200 when the food wears off.
- **Regen speed is adjustable.** `EitrRegenMultiplier` scales the base refill rate. Gear and status effects that change eitr regen still apply on top.
- **Summon limit per staff.** Each summoning staff (Staff of the Dead, Trollstav, modded ones) gets a setting for how many of its summons you can have at once.
- **Client-side only.** Each player who wants it installs it. Servers don't need it.
- **Live reload.** Edit the config while the game runs. A new pool size applies within about a second.

The eitr bar works as in vanilla: it shows while eitr is below max and hides about a second after it fills up.

## Config

The file is `BepInEx/config/com.oddhatter.alwayseitr.cfg`. You can also edit it in r2modman under Config editor.

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch. `false` = vanilla, so eitr comes only from food. |
| `BaseEitr` | `200` | Size of the always-on pool, from 0 to 1000. |
| `EitrRegenMultiplier` | `1.0` | Refill speed, from 0 to 10. `1` = vanilla, `2` = twice as fast. |

### Summons

The `[Summons]` section has one setting per craftable summoning staff, named after the staff as it appears in game
(for example `Dead Raiser`, `Trollstav`). Its description says what it summons and the normal game limit. The settings
appear in the file after you load a world once with this version.

| Setting | Default | Meaning |
|---|---|---|
| `<staff>` | `0` | Most summons from that staff you can have at once, from 1 to 50, whatever your Blood Magic level. `0` = vanilla (the limit grows with Blood Magic). |

Vanilla has two limits: a cast that finds the staff's maximum of its creature nearby does nothing, and when a new summon starts following you, the oldest ones beyond your skill-based limit vanish. A setting other than 0 sets both to that number.

## Install

1. In r2modman, go to Settings → Profile → Import local mod.
2. Pick `Oddhatter-AlwaysEitr-<version>.zip`.
3. Set the author to `Oddhatter`.

The mod needs BepInExPack_Valheim.
