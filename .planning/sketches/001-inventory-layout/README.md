---
sketch: 001
name: inventory-layout
question: "Where does a compact, real-time inventory sit, and how does the character read as two hand drop targets?"
winner: null
tags: [layout, inventory, hands, hud]
---

# Sketch 001: Inventory Layout

## Design Question
The game keeps running while the inventory is open. Where should a compact inventory sit so the crosshair and threats stay visible, and how should the character be shown as two hand drop targets (left and right) for dragging items onto?

## How to View
open .planning/sketches/001-inventory-layout/index.html

## Variants
- **A: Right-edge column** has a 4×2 grid on top and a small figure below, with L and R hand targets beside its hands. It's closest to a paper doll.
- **B: Bottom strip (hotwheel+)** is a single 8-slot row with the L and R hands at each end. It replaces the Hotwheel while open, so it's the least intrusive.
- **C: Split around crosshair** puts a 2-column grid on the left edge and the figure and hands on the right, leaving the centre clear.

All three share the same rules, which mirror `Inventory`, `HandRig` and `Item.equipInfo`:
- There are 8 slots holding an item and a stack count, and stacks merge.
- A two-handed item always leads with `defaultHand`, and the other hand shows "supporting".
- Dropping onto a full hand swaps the displaced item back to where the dragged item came from.
- An empty off-hand shows "steadies X" for items with `supportHand`.
- Stackables (fruit, cells) can't be held.

## Controls
- <kbd>Tab</kbd> or <kbd>I</kbd> toggles the inventory.
- Drag to a hand to equip, or drag to a slot to move or stack.
- Double-click equips to the default hand, and <kbd>Shift</kbd> plus double-click equips to the other hand.
- Right-click a held item to stow it.
- The toolbar (bottom right) resets items, cycles between normal, full and empty, and flips `defaultHand`.

## What to Look For
- Is the crosshair area still clear, and can you track the moving red enemies while dragging?
- How far does the mouse travel from a grid slot to a hand? B and C are short hops; A is a vertical drag.
- Does a small figure help (A and C), or are two labelled hand boxes enough (B)?
- Is the swap onto an occupied hand predictable, especially when dropping a two-handed item?
