#!/usr/bin/env python3
"""
split_sprite_sheet.py
将 2560×1440 的 sprite sheet（16:9，5帧水平排列）裁切为 5 张独立的 128×360 透明背景 PNG。

用法:
    python3 scripts/split_sprite_sheet.py <sprite_sheet.png> <action_name> [output_dir]

示例:
    python3 scripts/split_sprite_sheet.py ./seedance-output/archer_idle_spritesheet.png idle
"""

import sys
import os
from pathlib import Path

from PIL import Image


# ── 常量 ──────────────────────────────────────────────────────────────────────
SPRITE_SHEET_W = 2560   # 16:9 2K 宽度
SPRITE_SHEET_H = 1440   # 16:9 2K 高度
FRAME_COUNT    = 5
TARGET_W       = 128    # 最终帧宽度
TARGET_H       = 360    # 最终帧高度
WHITE_THRESH   = 240    # 白色阈值 (>240 视为白色)
PADDING        = 5      # 裁剪内边距
HEIGHT_DIFF_THRESH = 5  # 脚底对齐容差（像素）
# ───────────────────────────────────────────────────────────────────────────────


def split_sheet(sheet_path: str, action_name: str, output_dir: str) -> list[str]:
    """裁切主函数，返回各帧输出路径列表。"""
    sheet = Image.open(sheet_path).convert("RGBA")
    assert sheet.width == SPRITE_SHEET_W and sheet.height == SPRITE_SHEET_H, \
        f"期望尺寸 {SPRITE_SHEET_W}×{SPRITE_SHEET_H}，实际 {sheet.width}×{sheet.height}"

    frame_w = SPRITE_SHEET_W // FRAME_COUNT   # 每帧区域宽度 = 512

    # ── 步骤1: 分离5个原始帧区域 ────────────────────────────────────────────
    raw_frames: list[Image.Image] = []
    for i in range(FRAME_COUNT):
        left = i * frame_w
        frame = sheet.crop((left, 0, left + frame_w, SPRITE_SHEET_H))
        raw_frames.append(frame)

    # ── 步骤2: 白色→透明 ─────────────────────────────────────────────────────
    def remove_white(img: Image.Image) -> Image.Image:
        arr = img.load()
        for y in range(img.height):
            for x in range(img.width):
                r, g, b, a = arr[x, y]
                if r > WHITE_THRESH and g > WHITE_THRESH and b > WHITE_THRESH:
                    arr[x, y] = (r, g, b, 0)
        return img

    cleaned = [remove_white(f.copy()) for f in raw_frames]

    # ── 步骤3: 裁剪到内容边界框 + PADDING ────────────────────────────────────
    def crop_to_content(img: Image.Image) -> tuple[Image.Image, tuple]:
        """返回裁剪后图像及其非透明区域边界 (left, top, right, bottom)。"""
        bbox = img.getbbox()
        if bbox is None:
            return img, (0, 0, img.width, img.height)
        left, top, right, bottom = bbox
        left   = max(0, left - PADDING)
        top    = max(0, top - PADDING)
        right  = min(img.width, right + PADDING)
        bottom = min(img.height, bottom + PADDING)
        return img.crop((left, top, right, bottom)), (left, top, right, bottom)

    cropped = [crop_to_content(f)[0] for f in cleaned]

    # ── 步骤4: 脚底锚点对齐 ──────────────────────────────────────────────────
    # 找出每帧中最底部非透明像素的 y 坐标
    def get_bottom_y(img: Image.Image) -> int:
        arr = img.load()
        for y in range(img.height - 1, -1, -1):
            for x in range(img.width):
                if arr[x, y][3] > 0:
                    return y
        return img.height - 1

    bottom_ys = [get_bottom_y(f) for f in cropped]
    max_bottom = max(bottom_ys)

    # 如果高度差异超过阈值，以脚底为锚点统一缩放
    def rescale_by_foot(img: Image.Image, ref_bottom: int, tgt_bottom: int) -> Image.Image:
        """将 img 缩放，使 ref_bottom 对齐到 tgt_bottom。"""
        scale = (tgt_bottom + 1) / (ref_bottom + 1)
        new_w = max(1, int(img.width * scale))
        new_h = max(1, int(img.height * scale))
        resized = img.resize((new_w, new_h), Image.NEAREST)
        return resized

    if max(bottom_ys) - min(bottom_ys) > HEIGHT_DIFF_THRESH:
        print(f"  ⚠️ 脚底高度差异 {max(bottom_ys) - min(bottom_ys)} px，执行脚底锚点对齐...")
        rescaled = [
            rescale_by_foot(cropped[i], bottom_ys[i], max_bottom)
            for i in range(FRAME_COUNT)
        ]
    else:
        print(f"  ✓ 脚底高度差异 {max(bottom_ys) - min(bottom_ys)} px，无需对齐")
        rescaled = cropped

    # ── 步骤5: 缩放到 360px 高，宽≤128px，NEAREST ───────────────────────────
    def scale_to_target(img: Image.Image) -> Image.Image:
        scale = TARGET_H / img.height
        new_w = int(img.width * scale)
        new_w = min(new_w, TARGET_W)   # 限制最大宽度
        # 如果超宽，等比缩放到 TARGET_W
        if new_w > TARGET_W:
            scale_w = TARGET_W / img.width
            new_w = TARGET_W
            new_h = int(img.height * scale_w)
        else:
            new_h = TARGET_H
        resized = img.resize((new_w, new_h), Image.NEAREST)
        return resized

    scaled = [scale_to_target(r) for r in rescaled]

    # ── 步骤6: 居中放置在 128×360 透明画布上 ────────────────────────────────
    def center_on_canvas(img: Image.Image) -> Image.Image:
        canvas = Image.new("RGBA", (TARGET_W, TARGET_H), (0, 0, 0, 0))
        x = (TARGET_W - img.width) // 2
        y = (TARGET_H - img.height) // 2
        canvas.paste(img, (x, y))
        return canvas

    final_frames = [center_on_canvas(s) for s in scaled]

    # ── 保存 ─────────────────────────────────────────────────────────────────
    out_dir = Path(output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    paths = []
    for i, frame in enumerate(final_frames, start=1):
        out_path = out_dir / f"archer_{action_name}_{i}.png"
        frame.save(out_path, "PNG")
        paths.append(str(out_path))
        print(f"  ✓ 帧 {i}: {out_path}")

    return paths


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)

    sheet_path  = sys.argv[1]
    action_name = sys.argv[2]
    output_dir  = sys.argv[3] if len(sys.argv) > 3 else "client/Assets/Sprites/Roles"

    print(f"\n裁切 sprite sheet: {sheet_path}")
    print(f"动作: {action_name}")
    print(f"输出目录: {output_dir}")
    print(f"目标尺寸: {TARGET_W}×{TARGET_H}\n")

    paths = split_sheet(sheet_path, action_name, output_dir)
    print(f"\n✅ 完成，共输出 {len(paths)} 帧")
