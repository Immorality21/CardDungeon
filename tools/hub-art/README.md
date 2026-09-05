# Hub placeholder art

Two standalone Python scripts (stdlib only — no Pillow) that regenerate the hub town's
**placeholder** sprites into `Assets/Sprites/Hub/`. Run from the repo root:

```
python tools/hub-art/gen_hub_backdrop.py     # hub_backdrop.png
python tools/hub-art/gen_hub_buildings.py    # lot_foundation.png + bld_*.png
```

They exist so the town was playable and readable before any art was drawn, which is the bargain
`docs/plans/HUB.md` §7 phase 3 assumed. **They are disposable**: the moment a real sprite is dropped
onto a `BuildingSO`'s `LevelSprites` / `AvailableSprite`, the generator for that lot is dead weight.

## Two things they know that you need to know too

- **The backdrop is 320×180, exactly ¼ of `HubSO.ReferenceSize` (1280×720)**, so a lot position in
  `Hub.asset` maps to a whole pixel in the backdrop. `gen_hub_backdrop.py` pins its horizon and its
  clear ground to the authored lot layout — change one and re-read the other.
- **A `.meta` is only written when there is none.** The GUID inside it is what every reference to the
  sprite uses, `BuildingSO` fields included; regenerating it silently orphans them all, and the
  symptom is a town quietly rendering its flat placeholders again with nothing in the console. That
  happened once during authoring, which is why the guard is there.

## Replacing them with real art

Nothing in code needs to change. Import the sprite, drop it on the field, and delete the generator
call — the view reads whatever `BuildingOps.SpriteFor` returns.

- `AvailableSprite` — the unbuilt plot. Shared across every lot today (`lot_foundation.png`); give
  each building its own the moment they should differ.
- `LevelSprites[0..n]` — one per level, element 0 being level 1.
- `AbsentSprite` — deliberately **null**, so a locked lot falls back to the flat slab plus its glyph.
  Author one if a locked lot should read as something more than "not yet".

Sizes are free: `BuildingSO.DrawOffset` / `DrawSize` place the sprite independently of the box it is
clicked in, so art may overhang its neighbours as much as it likes.
