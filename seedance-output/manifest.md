# Seedance Output Manifest

Generated sprite sheet images and their corresponding monster/action mappings.

## Generation Parameters (common)

| Parameter | Value |
|-----------|-------|
| Input reference | `seedance-input/rule.png` |
| Method | `dreamina image2image` |
| Resolution | 2K (2560x1440) |
| Ratio | 16:9 |
| Model | 5.0 |
| Frames per sheet | 5 |
| Cost per sheet | ~3 credits |

## Active Sprite Sheets (used for final frames)

| File | Monster | Action | submit_id | Cropped to |
|------|---------|--------|-----------|------------|
| `02056980887dfec1_image_1.png` | Slime | Walk | `02056980887dfec1` | `slime_walk_1~5.png` |
| `14528700af3b98bb_image_1.png` | Slime | Attack | `14528700af3b98bb` | `slime_attack_1~5.png` |
| `21e35c1953f479c4_image_1.png` | Slime | Death | `21e35c1953f479c4` | `slime_death_1~5.png` |
| `ec71e3360706d230_image_1.png` | Skeleton | Walk | `ec71e3360706d230` | `skeleton_walk_1~5.png` |
| `1d0783fca3bd6727_image_1.png` | Skeleton | Attack | `1d0783fca3bd6727` | `skeleton_attack_1~5.png` |
| `7e3bbba0f065b855_image_1.png` | Skeleton | Death | `7e3bbba0f065b855` | `skeleton_death_1~5.png` |
| `402fe21c7ce8f5be_image_1.png` | Orc | Walk | `402fe21c7ce8f5be` | `orc_walk_1~5.png` |
| `e5ffea309f657647_image_1.png` | Orc | Attack | `e5ffea309f657647` | `orc_attack_1~5.png` |
| `c4b2f3f152fff796_image_1.png` | Orc | Death | `c4b2f3f152fff796` | `orc_death_1~5.png` |
| `bb020d2e41589e28_image_1.png` | Elite | Walk | `bb020d2e41589e28` | `elite_walk_1~5.png` |
| `c01ae43f7917d7df_image_1.png` | Elite | Attack | `c01ae43f7917d7df` | `elite_attack_1~5.png` |
| `131096df62ae2828_image_1.png` | Elite | Death | `131096df62ae2828` | `elite_death_1~5.png` |
| `3417f63b8732f9dd_image_1.png` | Boss | Walk | `3417f63b8732f9dd` | `boss_walk_1~5.png` |
| `4ebea2b0bb3dba23_image_1.png` | Boss | Attack | `4ebea2b0bb3dba23` | `boss_attack_1~5.png` |
| `8bda35bafedf4c3f_image_1.png` | Boss | Death | `8bda35bafedf4c3f` | `boss_death_1~5.png` |

## Deprecated (not used)

These files were generated during prompt iteration but had quality issues (gray rectangle borders triggered by "fill frame area" wording). Kept for reference only.

| File | Monster | Action | Issue |
|------|---------|--------|-------|
| `e3a0cf5d6244cba5_image_1.png` | Slime | Attack (v5) | Gray rectangle borders around each frame |
| `3892be075e0bad8a_image_1.png` | Slime | Death (v6) | Gray rectangle borders around each frame |

## Post-Processing

All active sprite sheets were processed by `scripts/split_sprite_sheet.py`:

1. Detect 5 black rectangle regions from `seedance-input/rule.png` (each 400x650px)
2. Crop each frame at the detected coordinates
3. Remove white background (threshold > 230) and neutral gray pixels (180-235 range)
4. Remove gray rectangle frame lines within 8px margin of frame edges
5. Auto-crop to content bounding box
6. Scale to fit 80x130 using nearest-neighbor interpolation
7. Center on 80x130 transparent canvas
8. Save as transparent PNG to `client/Assets/Sprites/Enemies/`

## Output Location

Final frames: `client/Assets/Sprites/Enemies/<monster>_<action>_<1-5>.png`

Total: 5 monsters x 3 actions x 5 frames = **75 frames**
