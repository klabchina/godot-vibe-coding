#!/usr/bin/env python3
"""
split_sprite_sheet.py
从 rule.png 自动检测 5 个黑色区域坐标，按相同坐标从生成的 sprite sheet 中裁切，
等比缩放到 80×130，去除白色背景，保存为透明 PNG。

用法:
    python3 scripts/split_sprite_sheet.py <sprite_sheet.png> <action_name> [output_dir]

示例:
    python3 scripts/split_sprite_sheet.py ./seedance-output/archer_idle_sheet_v2.png idle
"""

import sys
from pathlib import Path

import numpy as np
from PIL import Image


# ── 常量 ──────────────────────────────────────────────────────────────────────
RULE_PNG_PATH  = "seedance-input/rule.png"
TARGET_W       = 80     # 最终帧宽度
TARGET_H       = 130    # 最终帧高度
WHITE_THRESH   = 240    # 白色阈值 (>240 视为白色)
BLACK_THRESH   = 30     # 黑色阈值 (<30 视为黑色)
# ───────────────────────────────────────────────────────────────────────────────


def detect_black_rects(rule_path: str) -> list[tuple[int, int, int, int]]:
    """
    从 rule.png 中检测黑色矩形区域，返回 [(left, top, right, bottom), ...] 列表，
    按 left 从左到右排序。
    """
    img = Image.open(rule_path).convert("RGB")
    arr = np.array(img)

    # 黑色像素掩码
    is_black = (arr[:, :, 0] < BLACK_THRESH) & (arr[:, :, 1] < BLACK_THRESH) & (arr[:, :, 2] < BLACK_THRESH)

    # 找黑色行范围
    row_has_black = is_black.any(axis=1)
    black_rows = np.where(row_has_black)[0]
    if len(black_rows) == 0:
        raise ValueError(f"rule.png 中未检测到黑色区域: {rule_path}")
    y_top = int(black_rows[0])
    y_bottom = int(black_rows[-1])

    # 在中间行扫描，找每个连续黑色段的 x 范围
    mid_row = (y_top + y_bottom) // 2
    row_data = is_black[mid_row]

    rects = []
    in_black = False
    x_start = 0
    for x in range(len(row_data)):
        if row_data[x] and not in_black:
            x_start = x
            in_black = True
        elif not row_data[x] and in_black:
            rects.append((x_start, y_top, x, y_bottom + 1))  # right/bottom 为不含边界
            in_black = False
    if in_black:
        rects.append((x_start, y_top, len(row_data), y_bottom + 1))

    return rects


def remove_white_background(img: Image.Image) -> Image.Image:
    """将白色像素 (R,G,B 均 > WHITE_THRESH) 的 alpha 设为 0。"""
    arr = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = arr[x, y]
            if r > WHITE_THRESH and g > WHITE_THRESH and b > WHITE_THRESH:
                arr[x, y] = (r, g, b, 0)
    return img


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


def split_sheet(sheet_path: str, action_name: str, output_dir: str, rule_path: str) -> list[str]:
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

    # ── 步骤3: 等比缩放到 80×130 ─────────────────────────────────────────────
    scaled = [scale_to_fit(f, TARGET_W, TARGET_H) for f in raw_frames]
    for i, s in enumerate(scaled):
        print(f"  帧 {i+1}: 缩放后 {s.width}×{s.height}")

    # ── 步骤4: 去除白色背景 ──────────────────────────────────────────────────
    cleaned = [remove_white_background(s.copy()) for s in scaled]

    # ── 步骤5: 居中放置在 80×130 透明画布上 ──────────────────────────────────
    final_frames = [center_on_canvas(c, TARGET_W, TARGET_H) for c in cleaned]

    # ── 保存 ─────────────────────────────────────────────────────────────────
    out_dir = Path(output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    paths = []
    for i, frame in enumerate(final_frames, start=1):
        out_path = out_dir / f"archer_{action_name}_{i}.png"
        frame.save(out_path, "PNG")
        paths.append(str(out_path))
        print(f"  ✓ 帧 {i}: {out_path}  ({frame.width}×{frame.height})")

    return paths


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)

    sheet_path  = sys.argv[1]
    action_name = sys.argv[2]
    output_dir  = sys.argv[3] if len(sys.argv) > 3 else "client/Assets/Sprites/Roles"
    rule_path   = sys.argv[4] if len(sys.argv) > 4 else RULE_PNG_PATH

    print(f"\n裁切 sprite sheet: {sheet_path}")
    print(f"动作: {action_name}")
    print(f"输出目录: {output_dir}")
    print(f"参考图: {rule_path}")
    print(f"目标尺寸: {TARGET_W}×{TARGET_H}\n")

    paths = split_sheet(sheet_path, action_name, output_dir, rule_path)
    print(f"\n✅ 完成，共输出 {len(paths)} 帧")
