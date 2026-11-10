# Photo Color Editor · 0.7 Beta

English · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-Hant.md)

**For 64-bit Windows PCs with an x64 processor.** This beta was tested on Windows 11. It may also run on Windows 10, where app compatibility is still unverified. [Microsoft’s Windows compatibility information](https://learn.microsoft.com/en-us/dotnet/core/install/windows#supported-versions)

To check your PC, open **Start → Settings → System → About**. Look for your version under **Windows specifications** and **x64-based processor** next to **System type**. [Microsoft help](https://support.microsoft.com/en-us/windows/experience/find-information-about-your-windows-device)

## Download and open the app

1. **[Download Photo Color Editor for Windows (ZIP)](https://github.com/vfyang/PhotoColorEditor/releases/download/v0.7.0-beta/PhotoColorEditor-0.7.0-beta-win-x64.zip)**.
2. Open **File Explorer → Downloads**. Right-click the downloaded ZIP file, choose **Extract All…**, then click **Extract**. [Microsoft help with ZIP files](https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/zip-and-unzip-files)
3. Open the new folder and double-click **PhotoColorEditor.exe**. Windows may display it as **PhotoColorEditor**.

The download includes everything the app needs. To use it again, open the same folder and double-click the app. You can edit photos offline after downloading.

**Want larger text?** In the app, choose **Settings → Large text**. Photo Color Editor is designed with older adults in mind, with high-contrast controls and clearly labeled one-click adjustments.

![Photo Color Editor with a sailboat painting on the left and one-click adjustments, white balance, and a live histogram on the right](docs/images/photo-color-editor-sample-2-en.png)

*Actual editor screenshot with sample-2.jpg open and Auto Enhance applied.*

[See the original and edited sample photos side by side](samples/README.md)

## Edit your first photo

1. Click **Open photo** and pick a JPG, JPEG, or PNG. To practice, choose **File → Sample photos**.
2. Start with **Auto enhance** or **Fix color cast**. If you know an area should be white or gray, choose **Pick white balance point** and select a patch with visible detail.
3. Refine the result with the quick adjustments or **Fine tune**. **Reset to original** brings back the full photo so you can start again. Undo and redo support up to 21 steps per window.
4. Choose **Save as** to keep a separate copy. The suggested filename skips existing copies. **Save** asks before replacing the current file.

## Editing tools

- **Color and tone.** View the live luminance histogram, see the applied exposure and white balance, or use Auto Levels, RGB curves, shadows, and highlights.
- **One-click adjustments.** Repeat an adjustment to increase it step by step. Soft Light and Sharpen each have five levels and can be used together.
- **Crop and straighten.** Move or resize the crop box, rotate 90°, or straighten in ±0.5° steps. Type an angle from −15° to +15° for 0.01° precision. Straightening zooms in to fill the corners.
- **Photo preview.** Zoom, fit, center, or drag a zoomed photo to pan. Hold Ctrl while scrolling to zoom.
- **Appearance.** Enable **Settings → Large text**, or choose a light or dark theme. The app follows the Windows display language by default and supports English, Simplified Chinese, and Traditional Chinese.

On first launch, the app adds a command to the menu that appears when you right-click a photo in File Explorer. On Windows 11, choose **Show more options** to find it. **Settings → Add to Windows Open with** updates the menu entry if you move the app.

## Help and information

- [0.7 Beta release notes](https://github.com/vfyang/PhotoColorEditor/releases/tag/v0.7.0-beta)
- [MIT license](LICENSE)
- SyncPort.ai · [contact@syncport.ai](mailto:contact@syncport.ai)
