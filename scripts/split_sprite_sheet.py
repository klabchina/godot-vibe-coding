#!/usr/bin/env python3
"""
split_sprite_sheet.py
从 rule.png 自动检测 5 个黑色区域坐标，按相同坐标从生成的 sprite sheet 中裁切，
等比缩放到 80×130，去除白色/浅灰色背景，保存为透明 PNG。

用法:
    python3 scripts/split_sprite_sheet.py <sprite_sheet.png> <character> <action> [output_dir] [rule_path]

示例:
    python3 scripts/split_sprite_sheet.py ./seedance-output/455ad0105f056f95_image_1.png slime attack
    python3 scripts/split_sprite_sheet.py ./seedance-output/cceedecf65e9ce8b_image_1.png slime death
    python3 scripts/split_sprite_sheet.py <sprite_sheet.png> archer attack "client/Assets/Sprites/Roles" "seedance-input/rule_10.png"
"""

import sys
from pathlib import Path

import numpy as np
from PIL import Image


# ── 常量 ──────────────────────────────────────────────────────────────────────
RULE_PNG_PATH  = "seedance-input/rule_10.png"
TARGET_W       = 80     # 最终帧宽度
TARGET_H       = 130    # 最终帧高度
BG_THRESH      = 230    # 背景阈值 (R,G,B 均 > 此值视为背景白色)
GRAY_LOW       = 180    # 灰色线条下限
GRAY_HIGH      = 235    # 灰色线条上限
GRAY_DIFF      = 15     # R/G/B 最大差值（中性灰判定）
BLACK_THRESH   = 30     # 黑色阈值 (<30 视为黑色)
# ───────────────────────────────────────────────────────────────────────────────


def detect_black_rects(rule_path: str) -> list[tuple[int, int, int, int]]:
    """
    从 rule.png 中检测黑色矩形区域，返回 [(left, top, right, bottom), ...] 列表。
    ★ 顺序规则：先按行段从上到下（上行1-5，再下行6-10），
    行段内按 x 从左到右（1→2→3→4→5）。
    适用于两行布局（rule_10.png）的模板。
    """
    img = Image.open(rule_path).convert("RGB")
    arr = np.array(img)

    # 黑色像素掩码
    is_black = (arr[:, :, 0] < BLACK_THRESH) & (arr[:, :, 1] < BLACK_THRESH) & (arr[:, :, 2] < BLACK_THRESH)

    # 找所有黑色行段（可能有多个连续区域）
    row_has_black = is_black.any(axis=1)
    black_row_segments = []
    in_black = False
    seg_start = 0
    for y in range(arr.shape[0]):
        if row_has_black[y] and not in_black:
            seg_start = y
            in_black = True
        elif not row_has_black[y] and in_black:
            black_row_segments.append((seg_start, y))
            in_black = False
    if in_black:
        black_row_segments.append((seg_start, arr.shape[0]))

    if not black_row_segments:
        raise ValueError(f"rule.png 中未检测到黑色区域: {rule_path}")

    print(f"  检测到 {len(black_row_segments)} 个黑色行段:")
    for i, (t, b) in enumerate(black_row_segments):
        print(f"    段{i+1}: y={t}-{b}")

    # 对每个行段，检测该段内的 x 方向矩形
    # ★ 顺序：先按行段从上到下（上行1-5，再下行6-10），行段内从左到右
    ordered_rects = []
    for (y_top, y_bottom) in black_row_segments:
        row_rects = []
        mid_y = (y_top + y_bottom) // 2
        row_data = is_black[mid_y]

        in_black = False
        x_start = 0
        for x in range(len(row_data)):
            if row_data[x] and not in_black:
                x_start = x
                in_black = True
            elif not row_data[x] and in_black:
                row_rects.append((x_start, y_top, x, y_bottom))
                in_black = False
        if in_black:
            row_rects.append((x_start, y_top, len(row_data), y_bottom))

        # 行段内已按 x 从左到右排列，直接 extend
        ordered_rects.extend(row_rects)

    return ordered_rects


def remove_background(img: Image.Image) -> Image.Image:
    """将白色背景和中性灰像素的 alpha 设为 0。
    - 白色: R,G,B 均 > BG_THRESH
    - 中性灰: R,G,B 在 GRAY_LOW~GRAY_HIGH 且彼此差值 < GRAY_DIFF
    """
    arr = np.array(img)  # shape: (H, W, 4) for RGBA
    r = arr[:, :, 0].astype(int)
    g = arr[:, :, 1].astype(int)
    b = arr[:, :, 2].astype(int)

    # 白色背景
    is_white = (r > BG_THRESH) & (g > BG_THRESH) & (b > BG_THRESH)

    # 中性灰 (R≈G≈B, 非角色色彩)
    is_neutral_gray = (
        (r > GRAY_LOW) & (r < GRAY_HIGH) &
        (g > GRAY_LOW) & (g < GRAY_HIGH) &
        (b > GRAY_LOW) & (b < GRAY_HIGH) &
        (np.abs(r - g) < GRAY_DIFF) &
        (np.abs(g - b) < GRAY_DIFF) &
        (np.abs(r - b) < GRAY_DIFF)
    )

    arr[is_white | is_neutral_gray, 3] = 0

    return Image.fromarray(arr)


def remove_rect_frame_lines(img: Image.Image, margin: int = 8) -> Image.Image:
    """
    去除模型生成的灰色矩形框线。
    框线特征：沿帧边缘的灰色细线 (R=G=B ∈ [150,235])，位于帧的最外层 margin 像素内。
    只去除边缘区域的灰色像素，保留帧内部角色的所有像素。
    """
    arr = np.array(img)  # RGBA
    h, w = arr.shape[:2]

    # 灰色掩码: R≈G≈B 且在灰色范围内 (非白非黑)
    r, g, b = arr[:, :, 0].astype(int), arr[:, :, 1].astype(int), arr[:, :, 2].astype(int)
    avg = (r + g + b) / 3
    is_gray = (avg > 140) & (avg < 236) & (np.abs(r - g) < 20) & (np.abs(g - b) < 20) & (np.abs(r - b) < 20)

    # 只在帧边缘区域去除灰色像素
    edge_mask = np.zeros((h, w), dtype=bool)
    edge_mask[:margin, :] = True       # 上边
    edge_mask[h-margin:, :] = True     # 下边
    edge_mask[:, :margin] = True       # 左边
    edge_mask[:, w-margin:] = True     # 右边

    # 去除边缘灰色像素
    to_remove = is_gray & edge_mask
    arr[to_remove, 3] = 0

    return Image.fromarray(arr)


def auto_crop_content(img: Image.Image) -> Image.Image:
    """自动裁剪到非透明内容的最小包围框，去除帧内多余边距。"""
    arr = np.array(img)
    alpha = arr[:, :, 3]

    rows = np.any(alpha > 0, axis=1)
    cols = np.any(alpha > 0, axis=0)

    if not rows.any():
        return img  # 全透明，返回原图

    y_min, y_max = np.where(rows)[0][[0, -1]]
    x_min, x_max = np.where(cols)[0][[0, -1]]

    return img.crop((int(x_min), int(y_min), int(x_max) + 1, int(y_max) + 1))


def scale_to_fit(img: Image.Image, max_w: int, max_h: int) -> Image.Image:
    """等比缩放，使图像适配 max_w × max_h，不超出。"""
    scale = min(max_w / img.width, max_h / img.height)
    new_w = max(1, int(img.width * scale))
    new_h = max(1, int(img.height * scale))
    return img.resize((new_w, new_h), Image.NEAREST)


def center_on_canvas(img: Image.Image, canvas_w: int, canvas_h: int) -> Image.Image:
    """将图像居中放置在透明画布上。"""
    canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
    x = (canvas_w - img.width) // 2
    y = (canvas_h - img.height) // 2
    canvas.paste(img, (x, y))
    return canvas


def split_sheet(sheet_path: str, character: str, action: str, output_dir: str, rule_path: str) -> list[str]:
    """裁切主函数，返回各帧输出路径列表。"""

    # ── 步骤1: 从 rule.png 检测黑色区域坐标 ──────────────────────────────────
    rects = detect_black_rects(rule_path)
    print(f"  从 rule.png 检测到 {len(rects)} 个黑色区域:")
    for i, (l, t, r, b) in enumerate(rects):
        print(f"    区域 {i+1}: ({l}, {t}) → ({r}, {b})  尺寸 {r-l}×{b-t}")

    # ── 步骤2: 按坐标从 sprite sheet 裁切 ────────────────────────────────────
    sheet = Image.open(sheet_path).convert("RGBA")
    print(f"  sprite sheet 尺寸: {sheet.width}×{sheet.height}")

    raw_frames: list[Image.Image] = []
    for i, (left, top, right, bottom) in enumerate(rects):
        frame = sheet.crop((left, top, right, bottom))
        raw_frames.append(frame)
        print(f"  帧 {i+1}: 裁切 ({left}, {top}) → ({right}, {bottom})  尺寸 {frame.width}×{frame.height}")

    # ── 步骤3: 去除白色/浅灰色背景 ─────────────────────────────────────────
    cleaned = [remove_background(f) for f in raw_frames]

    # ── 步骤3.5: 去除灰色矩形框线 ──────────────────────────────────────────
    cleaned = [remove_rect_frame_lines(c) for c in cleaned]

    # ── 步骤4: 自动裁剪到内容包围框（去除灰色边框残留的空白） ──────────────
    cropped = [auto_crop_content(c) for c in cleaned]
    for i, c in enumerate(cropped):
        print(f"  帧 {i+1}: 内容裁剪后 {c.width}×{c.height}")

    # ── 步骤5: 等比缩放到 80×130 ─────────────────────────────────────────────
    scaled = [scale_to_fit(c, TARGET_W, TARGET_H) for c in cropped]
    for i, s in enumerate(scaled):
        print(f"  帧 {i+1}: 缩放后 {s.width}×{s.height}")

    # ── 步骤6: 居中放置在 80×130 透明画布上 ──────────────────────────────────
    final_frames = [center_on_canvas(s, TARGET_W, TARGET_H) for s in scaled]

    # ── 保存 ─────────────────────────────────────────────────────────────────
    out_dir = Path(output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    paths = []
    for i, frame in enumerate(final_frames, start=1):
        out_path = out_dir / f"{character}_{action}_{i}.png"
        frame.save(out_path, "PNG")
        paths.append(str(out_path))
        print(f"  ✓ 帧 {i}: {out_path}  ({frame.width}×{frame.height})")

    return paths


if __name__ == "__main__":
    if len(sys.argv) < 4:
        print(__doc__)
        sys.exit(1)

    sheet_path  = sys.argv[1]
    character   = sys.argv[2]
    action      = sys.argv[3]
    output_dir  = sys.argv[4] if len(sys.argv) > 4 else "client/Assets/Sprites/Roles"
    rule_path   = sys.argv[5] if len(sys.argv) > 5 else RULE_PNG_PATH

    print(f"\n裁切 sprite sheet: {sheet_path}")
    print(f"角色: {character}")
    print(f"动作: {action}")
    print(f"输出目录: {output_dir}")
    print(f"参考图: {rule_path}")
    print(f"目标尺寸: {TARGET_W}×{TARGET_H}\n")

    paths = split_sheet(sheet_path, character, action, output_dir, rule_path)
    print(f"\n完成，共输出 {len(paths)} 帧")
