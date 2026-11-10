using System.Globalization;
using System.IO;
using System.Text.Json;

namespace PhotoColorEditor;

internal static class Strings
{
    public static readonly IReadOnlyDictionary<string, (string Simplified, string Traditional, string English)> All =
        new Dictionary<string, (string, string, string)>
        {
            ["Title"] = ("照片颜色纠正器", "照片顏色校正器", "Photo Color Editor"),
            ["About"] = ("关于", "關於", "About"), ["AboutTitle"] = ("关于 {0}", "關於 {0}", "About {0}"),
            ["Version"] = ("版本 {0}", "版本 {0}", "Version {0}"),
            ["ProjectRepository"] = ("项目仓库", "專案儲存庫", "Project repository"),
            ["Contact"] = ("联系我们", "聯絡我們", "Contact us"), ["Close"] = ("关闭", "關閉", "Close"),
            ["LinkFailed"] = ("无法打开链接。", "無法開啟連結。", "Could not open the link."),
            ["File"] = ("文件", "檔案", "File"), ["Edit"] = ("编辑", "編輯", "Edit"), ["Settings"] = ("设置", "設定", "Settings"),
            ["Theme"] = ("主题", "主題", "Theme"), ["LightTheme"] = ("浅色", "淺色", "Light"), ["DarkTheme"] = ("深色", "深色", "Dark"),
            ["Recent"] = ("最近打开", "最近開啟", "Recent photos"), ["NoRecent"] = ("还没有最近打开的照片", "沒有最近開啟的照片", "No recent photos"),
            ["RecentMissing"] = ("找不到 {0}。它可能已被移动或删除。", "找不到 {0}。它可能已被移動或刪除。", "Could not find {0}. It may have been moved or deleted."),
            ["DropHere"] = ("松开鼠标打开照片", "放開滑鼠以開啟照片", "Release to open photo"),
            ["DropSingle"] = ("请一次拖入一张 JPG 或 PNG 照片。", "請一次拖入一張 JPG 或 PNG 照片。", "Drop one JPG or PNG photo at a time."),
            ["ZoomIn"] = ("放大", "放大", "Zoom in"), ["ZoomOut"] = ("缩小", "縮小", "Zoom out"),
            ["FitPhoto"] = ("适应窗口", "符合視窗", "Fit to window"), ["CenterPhoto"] = ("居中", "置中", "Center"),
            ["ZoomLevel"] = ("相对于适应窗口的缩放比例", "相對於符合視窗的縮放比例", "Zoom relative to fit"),
            ["PanHelp"] = ("按住左键拖动照片", "按住滑鼠左鍵拖曳照片", "Drag the photo to pan"),
            ["Samples"] = ("示例照片", "範例照片", "Sample photos"),
            ["TrySample"] = ("试用示例", "試用範例", "Try a sample"),
            ["WarmSample"] = ("暖色山景", "暖色山景", "Warm mountain scene"),
            ["CoolSample"] = ("冷色山景", "冷色山景", "Cool mountain scene"),
            ["SampleOpened"] = ("已打开内置示例。可另存为照片。", "已開啟內建範例。可另存為照片。", "Built-in sample opened. Use Save As to keep a copy."),
            ["Crop"] = ("裁剪", "裁剪", "Crop"), ["ApplyCrop"] = ("应用裁剪", "套用裁剪", "Apply crop"),
            ["CropHint"] = ("拖动边或角调整大小，拖动框内移动选区，然后点「应用裁剪」。", "拖曳邊或角調整大小，拖曳框內移動選區，再按「套用裁剪」。", "Drag an edge or corner to resize. Drag inside to move the crop, then choose Apply crop."),
            ["RotateLeft"] = ("左转 90°", "左轉 90°", "Rotate left 90°"),
            ["RotateRight"] = ("右转 90°", "右轉 90°", "Rotate right 90°"),
            ["FineRotate"] = ("微调角度", "微調角度", "Straighten"),
            ["AngleLabel"] = ("角度（°）", "角度（°）", "Angle (°)"),
            ["AngleLeft"] = ("左转 0.5 度", "左轉 0.5 度", "Rotate left 0.5 degrees"),
            ["AngleRight"] = ("右转 0.5 度", "右轉 0.5 度", "Rotate right 0.5 degrees"),
            ["ApplyAngle"] = ("应用角度", "套用角度", "Apply angle"),
            ["AngleHelp"] = ("可输入 −15° 到 +15°，精确到 0.01°。照片会放大以填满空白边角。", "可輸入 −15° 到 +15°，精確到 0.01°。照片會放大以填滿空白邊角。", "Enter −15° to +15° in 0.01° increments. The image zooms in to fill empty corners."),
            ["AngleInvalid"] = ("请输入 −15 到 +15 之间的数字。", "請輸入 −15 到 +15 之間的數字。", "Enter a number from −15 to +15."),
            ["AutoLevels"] = ("自动色阶", "自動色階", "Auto Levels"),
            ["AutoLevelsTip"] = ("根据亮度直方图扩展暗部和亮部，保留极端明暗的渐变。", "根據亮度直方圖擴展暗部和亮部，保留極暗與極亮處的漸層。", "Expand the tonal range from the luminance histogram while protecting extreme tones."),
            ["LevelsStable"] = ("当前照片无需扩展色阶。", "目前的照片無須擴展色階。", "The current photo does not need a tonal stretch."),
            ["Shadows"] = ("阴影", "陰影", "Shadows"), ["Highlights"] = ("高光", "高光", "Highlights"),
            ["LightHistogram"] = ("亮度直方图 · 暗 → 亮", "亮度直方圖 · 暗 → 亮", "Luminance histogram · Dark → Light"),
            ["HistogramBefore"] = ("虚线 原图", "虛線 原圖", "Dashed: original"), ["HistogramCurrent"] = ("实线 当前预览", "實線 目前預覽", "Solid: current preview"),
            ["Language"] = ("语言", "語言", "Language"), ["LargeText"] = ("大字体", "大字體", "Large text"),
            ["SystemLanguage"] = ("跟随系统", "跟隨系統", "Follow Windows"),
            ["RegisterOpenWith"] = ("加入 Windows 打开方式", "加入 Windows 開啟方式", "Add to Windows Open with"),
            ["OpenWithReady"] = ("已加入照片右键菜单。在 Windows 11 中可从「显示更多选项」找到。", "已加入照片右鍵選單。在 Windows 11 中可從「顯示更多選項」找到。", "Added to the photo context menu. On Windows 11, choose Show more options to see it."),
            ["OpenWithCommandLabel"] = ("用{0}打开", "使用{0}開啟", "Open with {0}"),
            ["OpenWithFailed"] = ("无法加入 Windows 打开方式。请检查当前账户的权限。", "無法加入 Windows 開啟方式。請確認目前帳戶的權限。", "Could not add the editor to Open with. Check permissions for this Windows account."),
            ["Open"] = ("打开照片", "開啟照片", "Open photo"), ["Undo"] = ("撤销", "復原", "Undo"), ["Redo"] = ("重做", "重做", "Redo"),
            ["Save"] = ("保存", "儲存", "Save"), ["SaveAs"] = ("另存为", "另存新檔", "Save as"),
            ["EmptyPrompt"] = ("把 JPG 或 PNG 照片拖到这里", "將 JPG 或 PNG 照片拖曳到這裡", "Drop a JPG or PNG photo here"),
            ["PhotoPreview"] = ("照片预览", "照片預覽", "Photo preview"), ["Original"] = ("原图", "原圖", "Original"),
            ["ResetOriginal"] = ("恢复原图", "恢復原圖", "Reset to original"),
            ["ResetOriginalTip"] = ("恢复整张原图，可以重新调整。用撤销找回刚才的调整。", "恢復整張原圖，可以重新調整。用復原找回剛才的調整。", "Restore the full original and start editing again. Use Undo to recover your previous edits."),
            ["ResetOriginalApplied"] = ("已恢复原图，可以重新调整", "已恢復原圖，可以重新調整", "Original restored. You can start editing again."),
            ["Edited"] = ("调整后", "調整後", "Edited"), ["Quick"] = ("一键调整", "一鍵調整", "Quick adjustments"),
            ["AutoOptimize"] = ("自动优化", "自動最佳化", "Auto enhance"), ["AutoBalance"] = ("纠正偏色", "校正偏色", "Fix color cast"),
            ["ExposureValue"] = ("曝光 {0:+0.00;-0.00;0.00} EV", "曝光 {0:+0.00;-0.00;0.00} EV", "Exposure {0:+0.00;-0.00;0.00} EV"),
            ["AutoBalanceValue"] = ("自动白平衡 · 红 {0:0.00} 绿 {1:0.00} 蓝 {2:0.00}", "自動白平衡 · 紅 {0:0.00} 綠 {1:0.00} 藍 {2:0.00}", "Auto white balance · R {0:0.00} G {1:0.00} B {2:0.00}"),
            ["PickedBalanceValue"] = ("取样白平衡 · 红 {0:0.00} 绿 {1:0.00} 蓝 {2:0.00}", "取樣白平衡 · 紅 {0:0.00} 綠 {1:0.00} 藍 {2:0.00}", "Sampled white balance · R {0:0.00} G {1:0.00} B {2:0.00}"),
            ["Brighten"] = ("亮一点", "亮一點", "Brighter"), ["Warm"] = ("暖一点", "暖一點", "Warmer"),
            ["Cool"] = ("冷一点", "冷一點", "Cooler"),
            ["Vivid"] = ("色彩更鲜明", "色彩更鮮明", "More vivid"), ["Pick"] = ("选定白平衡点", "選定白平衡點", "Pick white balance point"),
            ["SoftLight"] = ("柔光", "柔光", "Soft Light"), ["Sharpen"] = ("锐化", "銳化", "Sharpen"),
            ["StepTip"] = ("每点一次调整一步。用撤销退回上一步。", "每按一次調整一步。用復原回到上一步。", "Each click adjusts one step. Use Undo to go back a step."),
            ["SoftLightTip"] = ("每点一次增加一级柔光，增强明暗层次。用撤销退回上一级。", "每按一次增加一級柔光，加強明暗層次。用復原回到上一級。", "Each click adds one level of gentle tonal contrast. Use Undo to go back a level."),
            ["SharpenTip"] = ("每点一次增加一级锐化，增强边缘清晰度。用撤销退回上一级。", "每按一次增加一級銳化，加強邊緣清晰度。用復原回到上一級。", "Each click adds one level of edge clarity. Use Undo to go back a level."),
            ["CancelPick"] = ("取消取样", "取消取樣", "Cancel sampling"), ["FineTune"] = ("手动微调", "手動微調", "Fine tune"),
            ["Brightness"] = ("亮度", "亮度", "Brightness"), ["Warmth"] = ("冷暖", "色溫", "Temperature"),
            ["Tint"] = ("色调（绿 ↔ 紫）", "色調（綠 ↔ 紫）", "Tint (green ↔ magenta)"), ["Saturation"] = ("饱和度", "飽和度", "Saturation"),
            ["CurvesAndHistogram"] = ("曲线与颜色分布", "曲線與色彩分布", "Curves and color distribution"),
            ["Histogram"] = ("RGB 颜色分布 · 暗 → 亮", "RGB 色彩分布 · 暗 → 亮", "RGB distribution · Dark → Light"),
            ["CurveChannel"] = ("曲线通道", "曲線色版", "Curve channel"), ["Master"] = ("整体", "整體", "Overall"),
            ["Red"] = ("红色", "紅色", "Red"), ["Green"] = ("绿色", "綠色", "Green"), ["Blue"] = ("蓝色", "藍色", "Blue"),
            ["CurveHelp"] = ("点击添加，拖动调整。Enter 添加中点，空格选点，方向键微调，Delete 删除。", "點一下以新增控制點，拖曳可調整。Enter 新增中點，空白鍵選點，方向鍵微調，Delete 刪除。", "Click to add, drag to adjust. Enter adds a midpoint, Space selects, arrows move, and Delete removes."),
            ["ResetCurve"] = ("恢复此曲线", "重設此曲線", "Reset this curve"),
            ["PickHelp"] = ("请点选原本是白色或灰色、还能看见细节的位置。也可用方向键移动圆圈，再按 Enter。", "請選取原本為白色或灰色、仍看得見細節的位置。也可以用方向鍵移動圓圈，再按 Enter。", "Click a detailed area that should be white or gray. You can also move the circle with the arrow keys and press Enter."),
            ["PickInvalid"] = ("请重新选择有细节的白色或浅灰色区域。", "請重新選擇看得見細節的白色或淺灰色區域。", "Choose a different white or light gray area with visible detail."),
            ["PickApplied"] = ("已用所选点校正偏色", "已用所選取樣點校正偏色", "Color cast corrected using the selected point"),
            ["AutoInsufficient"] = ("这张照片缺少可靠的中性区域，可试试选定白平衡点。", "這張照片缺少可靠的中性色區域，可試著選定白平衡點。", "No reliable neutral area found. Try picking a white balance point."),
            ["Loading"] = ("正在打开 {0}…", "正在開啟 {0}…", "Opening {0}…"), ["Saving"] = ("正在保存 {0}…", "正在儲存 {0}…", "Saving {0}…"),
            ["Opened"] = ("已打开 {0}", "已開啟 {0}", "Opened {0}"), ["Saved"] = ("已保存到 {0}", "已儲存到 {0}", "Saved to {0}"),
            ["Steps"] = ("可撤销 {0} 步 · 可重做 {1} 步", "可復原 {0} 步 · 可重做 {1} 步", "Undo {0} · Redo {1}"), ["Unsaved"] = ("有未保存修改", "有未儲存的變更", "Unsaved changes"),
            ["Adjusted"] = ("已调整照片", "已調整照片", "Photo adjusted"), ["Undone"] = ("已撤销", "已復原", "Undid adjustment"),
            ["Redone"] = ("已重做", "已重做", "Redid adjustment"), ["NoChange"] = ("照片已是这个效果", "照片已套用這個效果", "Photo already has this adjustment"),
            ["BeforeSaving"] = ("要覆盖以下文件吗？\n{0}", "要覆寫下列檔案嗎？\n{0}", "Replace this file?\n{0}"),
            ["Transparency"] = ("保存为 JPG 时，透明区域会填成白色。要继续吗？", "另存為 JPG 時，透明區域會填成白色。要繼續嗎？", "Saving as JPG fills transparent areas with white. Continue?"),
            ["LeaveTitle"] = ("照片还没有保存", "照片尚未儲存", "Your edits are unsaved"),
            ["LeaveMessage"] = ("请为 {0} 选择接下来的操作。", "請選擇如何處理 {0} 的變更。", "Choose what to do with edits to {0}."),
            ["Discard"] = ("放弃修改", "捨棄變更", "Discard edits"), ["KeepEditing"] = ("继续编辑", "繼續編輯", "Keep editing"),
            ["Cancel"] = ("取消", "取消", "Cancel"), ["Continue"] = ("继续", "繼續", "Continue"), ["Replace"] = ("覆盖保存", "覆寫檔案", "Replace file"),
            ["Conflict"] = ("文件已在其他程序中修改、移动或删除。请另存为，或重新打开原文件。", "檔案已在其他程式中變更、移動或刪除。請另存新檔，或重新開啟原始檔案。", "The file was changed, moved, or deleted. Save a copy or reopen the original file."),
            ["OpenFailure"] = ("打不开 {0}。请确认文件是可读取的 JPG 或 PNG，再试一次。", "無法開啟 {0}。請確認檔案是可讀取的 JPG 或 PNG，再試一次。", "Could not open {0}. Check that it is a readable JPG or PNG and try again."),
            ["TooLarge"] = ("照片超过 6400 万像素。请先缩小尺寸再打开。", "照片超過 6,400 萬像素。請先縮小尺寸再開啟。", "This photo exceeds 64 megapixels. Resize it before opening."),
            ["SaveFailure"] = ("无法保存到 {0}。请检查文件是否被占用、位置是否可写，或选择另一个位置。", "無法儲存到 {0}。請確認檔案未被占用、位置可寫入，或選擇其他位置。", "Could not save to {0}. Check that the file is unlocked and the location is writable, or choose a different location."),
            ["PreviewFailure"] = ("无法刷新预览。可以撤销刚才的调整后再试。", "無法更新預覽。請復原剛才的調整後再試一次。", "Could not refresh the preview. Undo the last adjustment and try again."),
            ["RetryOpen"] = ("重新选择", "重新選擇", "Choose another file"), ["ChooseLocation"] = ("另选位置", "選擇其他位置", "Choose another location"),
            ["PictureFilter"] = ("照片 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png", "照片 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png", "Photos (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"),
            ["ExportFilter"] = ("JPG 照片 (*.jpg)|*.jpg;*.jpeg|PNG 照片 (*.png)|*.png", "JPG 照片 (*.jpg)|*.jpg;*.jpeg|PNG 照片 (*.png)|*.png", "JPG photo (*.jpg)|*.jpg;*.jpeg|PNG photo (*.png)|*.png"),
            ["CopySuffix"] = ("-已调整", "-已調整", "-edited")
        };

    public static string Get(string key, string language, params object[] args)
    {
        var pair = All[key];
        string value = language switch
        {
            AppLanguage.Simplified => pair.Simplified,
            AppLanguage.Traditional => pair.Traditional,
            _ => pair.English
        };
        return args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args);
    }
}

internal sealed record Preferences(bool LargeText = false, string[]? RecentPhotos = null, string Language = AppLanguage.System, bool DarkTheme = false)
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhotoColorEditor", "settings.json");

    public Preferences WithRecentPhoto(string path) => this with
    {
        RecentPhotos = new[] { Path.GetFullPath(path) }.Concat(RecentPhotos ?? []).Distinct(PathComparer).Take(10).ToArray()
    };

    public Preferences WithoutRecentPhoto(string path) => this with
    {
        RecentPhotos = (RecentPhotos ?? []).Where(value => !PathComparer.Equals(path, value)).ToArray()
    };

    public static Preferences Load(string? settingsPath = null)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(settingsPath ?? FilePath));
            var value = json.RootElement.Deserialize<Preferences>() ?? new();
            string language = AppLanguage.Normalize(value.Language);
            if (json.RootElement.ValueKind == JsonValueKind.Object && !json.RootElement.TryGetProperty("Language", out _) &&
                json.RootElement.TryGetProperty("English", out var oldEnglish) && oldEnglish.ValueKind == JsonValueKind.True)
                language = AppLanguage.English;
            return value with { Language = language, RecentPhotos = value.RecentPhotos?.Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(PathComparer).Take(10).ToArray() };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Save(string? settingsPath = null)
    {
        string target = settingsPath ?? FilePath;
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(this));
            File.Move(temporary, target, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }
}
