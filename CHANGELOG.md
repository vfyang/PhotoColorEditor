# Release notes

## 0.7.0-beta

Photo Color Editor runs offline on Windows 11 x64. The self-contained `PhotoColorEditor.exe` opens and saves JPG, JPEG, and PNG files. See the [README](README.md) for build and test commands.

- **Color correction.** Auto Enhance, automatic white balance, and white-balance sampling. The adjustment panel shows the exposure and RGB white-balance gains in use.
- **Tone and detail.** Auto Levels, a live before-and-after luminance histogram, RGB curves, manual shadow and highlight controls, and cumulative one-click adjustments. Soft Light and Sharpen each have five independent levels.
- **Framing.** Resize and move the crop frame, rotate in 90° turns, or straighten with ±0.5° buttons and typed angles precise to 0.01°. Straightening fills the frame by zooming in to cover the corners.
- **Editing history.** Undo and redo up to 21 steps per editing window. **Reset to original** restores the full photo and lets you start editing again. Undo recovers the previous edit.
- **File handling.** Drag in a photo, reopen any of the ten most recent files, and save copies with a suggested name that skips existing files. The app registers a per-user Windows **Open with** command on first launch.
- **Interface.** Large-text mode, light and dark themes, Simplified Chinese, Traditional Chinese, and English. The app follows the Windows display language by default. Two [generated sample images](samples/README.md) are available in the app and in this repository.
