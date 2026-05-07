# Wuxia UI Kit

> Generated: 2026-04-29  
> Style: dark wuxia ink scroll, cinnabar seal, muted parchment, bamboo green accents.

## Assets

| File | Use |
|---|---|
| `panel_scroll_dark.png` | Main large UI panel, suitable for pause menu / character panel / shop window. |
| `panel_note_dark.png` | Smaller info card panel, suitable for notes, item details, status summaries. |
| `button_normal.png` | Normal menu button. |
| `button_selected.png` | Highlighted / selected menu button. |
| `button_danger.png` | Dangerous action button, such as return to menu or delete save. |
| `divider_ink.png` | Thin ink divider line. |
| `mark_cinnabar.png` | Cinnabar selection marker or seal accent. |
| `icons_core_sheet.png` | Core icon sheet: settings, controls, return, save, inventory, equipment, skill, key. |

## Import Notes

- All PNG files are configured as Unity Sprites.
- Panels and buttons have Sprite Border values for `Image Type: Sliced`.
- Text should be rendered by Unity UI / TextMeshPro, not baked into the image.
- `icons_core_sheet.png` is currently imported as a single Sprite sheet reference. Slice into separate icons later if needed.

## Recommended First Use

Apply these to `PauseUI.cs` after layout is stable:

- Main menu box: `panel_scroll_dark.png`
- Jianghu note card: `panel_note_dark.png`
- Continue button: `button_selected.png` or `button_normal.png`
- Settings / controls buttons: `button_normal.png`
- Return button: `button_danger.png`
- Footer and note dividers: `divider_ink.png`
- Selected marker: `mark_cinnabar.png`
