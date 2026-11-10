# Photo Color Editor · 0.7 Beta

Photo Color Editor is an offline Windows photo editor from **SyncPort.ai**. It opens JPG, JPEG, and PNG and offers automatic color correction, white-balance sampling, Auto Levels, a live luminance histogram, curves, and one-click Soft Light and Sharpen.

- **Windows 11 x64** · Build the single-file `PhotoColorEditor.exe` with the command below.
- **Languages** · Simplified Chinese, Traditional Chinese, and English. The default follows the Windows display language.
- **Try it** · Choose **File → Sample photos** to load a generated image, or download the [warm](samples/warm-mountain.png) and [cool](samples/cool-mountain.png) samples to test opening and editing files.
- **Release notes** · [0.7 Beta](CHANGELOG.md).
- **Project** · [vfyang/PhotoColorEditor](https://github.com/vfyang/PhotoColorEditor)
- **Contact** · [contact@syncport.ai](mailto:contact@syncport.ai)
- **License** · [MIT](LICENSE). Keep the copyright and license notice when redistributing copies or substantial portions.

## 中文使用说明

在 Windows 11 64 位上双击 `dist/PhotoColorEditor.exe`。应用可离线运行，默认跟随 Windows 显示语言。可在「设置 → 语言」选用简体中文、繁體中文或 English，也可在设置中启用大字体。

| Windows 显示语言 | 默认窗口标题与「打开方式」名称 |
| --- | --- |
| 简体中文 | 照片颜色纠正器 |
| 繁體中文 | 照片顏色校正器 |
| 其他语言 | Photo Color Editor |

首次启动时，程序自动为当前账户注册 JPG、JPEG 和 PNG，菜单名称默认跟随 Windows。Windows 11 中右键单张照片，选择「显示更多选项 → 用照片颜色纠正器打开」，也可按住 Shift 右键直接打开完整菜单。

手动点「设置 → 加入 Windows 打开方式」会按当前应用语言和当前 EXE 路径重新注册。选择「跟随系统」后再注册可恢复 Windows 语言。把 EXE 移到其他文件夹后，如果原路径已失效，从新位置启动会更新菜单路径。

1. 打开 JPG 或 PNG 照片，或把一张照片直接拖进窗口。「文件 → 最近打开」保留最近 10 张。
2. 点「自动优化」或「纠正偏色」。如果你知道画面里哪一块本来是白色或灰色，点「选定白平衡点」，再点那块仍能看见细节的区域。
3. 文件名显示在窗口标题栏。「恢复原图」会恢复整张原图、适应窗口，并让你从原图重新调整。用「撤销」可以找回恢复之前的编辑。照片下方可以放大、缩小、适应窗口或居中。放大后按住左键拖动照片，或使用滚轮和滚动条。按住 Ctrl 滚动可缩放。可以撤销最近 21 次调整，并逐步重做。
4. 点「另存为」保存一份调整后的照片。建议名称从最初打开的照片名生成，例如 `OriginalA-已调整.png`、`OriginalA-已调整-2.png`。已有名称会自动跳过。「保存」会先确认是否覆盖当前文件。

打开照片或点「试用示例」后，可用预览上方的「裁剪」「左转 90°」和「右转 90°」。拖动裁剪框的边或角可调整大小，拖动框内可移动选区，拖动框外可重新画框。选好后点「应用裁剪」。裁剪和旋转支持撤销，也会应用到导出文件。

「微调角度」显示辅助网格和实时预览。用「−0.5°」「+0.5°」逐步调整，或输入 −15° 到 +15° 之间的小数，精确到 0.01°。画面会放大以填满旋转产生的空白边角，点「应用角度」后记入撤销历史。「设置 → 主题」可切换浅色或深色。

「亮一点」「暖一点」「冷一点」「色彩更鲜明」每点一次就调整一步。「柔光」和「锐化」各有 5 档，可独立叠加，也可和其他调整一起使用。自动优化会保留你已设好的曝光和白平衡，侧栏显示实际使用的曝光和白平衡数值。手动微调使用加宽的滑轨和高对比度滑块。

## 自动色阶与亮度直方图

「自动色阶」用照片亮度的第 1 和第 99 百分位确定主要色阶范围，再适度扩展。极暗和极亮的像素仍有各自的渐变。已经接近全范围或色调几乎一致的照片会跳过拉伸。该功能用统一的亮度映射处理色彩，重复点击不会叠加。

展开「手动微调」可单独调节「阴影」和「高光」。直方图用虚线显示原图，用实线显示当前预览，并随调整、恢复原图和撤销更新。照片里已经变成纯白或纯黑的区域没有可恢复的细节。

参考 [Adobe Photoshop 色阶](https://helpx.adobe.com/photoshop/desktop/adjust-color/color-corrections/adjust-levels-in-an-image.html)、[GIMP 色阶](https://docs.gimp.org/3.0/en/gimp-tool-levels.html) 和 [OpenCV 直方图均衡化](https://docs.opencv.org/4.11.0/d5/daf/tutorial_py_histogram_equalization.html)。

## 构建和测试

在 Windows 上使用 .NET 10 SDK 构建独立 EXE。

```powershell
dotnet publish src/PhotoColorEditor -c Release -r win-x64 --self-contained true -p:PublishProfile=Windows-x64 -o dist
```

测试程序会在临时目录生成两张合成图，覆盖调色、另存、撤销和命名。`--ui` 另外运行后台桌面流程测试。运行这些测试无需提供个人照片。

```powershell
dotnet run --project tests/PhotoColorEditor.Tests -- --ui
```

在 PowerShell 7 中可单独验证发布文件能从空目录启动并加载自带运行库。

```powershell
& tests/Test-SingleFile.ps1 -Exe dist/PhotoColorEditor.exe
```
