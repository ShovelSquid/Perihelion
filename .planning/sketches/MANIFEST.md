# Sketch Manifest

## Design Direction
Survival-grid pragmatic: a dense, no-frills inventory built for speed. The game keeps running in real time while it's open, so the panel is compact and stays out of the crosshair area. The world, threats and the reticle stay visible. The character is reduced to two big hand drop targets (left and right), not a detailed paper doll. Rules (swap, two-handed takes both hands, `defaultHand`) are communicated through slot highlights, not tooltips. Maps onto the existing `Inventory` (8 slots of item plus stack), `HandRig` (`right`/`left` HandSlot, `defaultHand`, two-handed hand1/support) and `Hotwheel`.

## Reference Points
- Escape from Tarkov: the inventory stays open in a live raid, dense grid, drag to equip
- Minecraft: slot grid, stack counts in the corner, quick-move shortcuts

## Sketches

| # | Name | Design Question | Winner | Tags |
|---|------|----------------|--------|------|
| 001 | inventory-layout | Where does a compact, real-time inventory sit, and how does the character read as two hand drop targets? | — | layout, inventory, hands, hud |
| 002 | hand-drop-rules | How does drag-and-drop feedback communicate swaps, two-handed items and `defaultHand`? | — | interaction, drag-drop, equip |
