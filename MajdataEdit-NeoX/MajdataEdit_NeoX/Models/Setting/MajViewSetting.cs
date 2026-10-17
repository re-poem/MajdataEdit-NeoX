using MajdataEdit_NeoX.Lang;
using MajdataEdit_NeoX.Types;
using Newtonsoft.Json;
using PropertyModels.ComponentModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MajdataEdit_NeoX.Models.Setting;

// 线格式契约：字段命名 / 默认值 / 类型必须与 ViewX 端 MajViewSetting 完全一致。
// JSON 键由 [JsonProperty] 锁定；其余属性仅为 Edit 端 UI 元数据，不参与线格式。
public partial class MajViewSetting
{
    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_TapSpeed)]
    [Range(0, int.MaxValue)]
    [JsonProperty("tapSpeed")]
    public float TapSpeed { get; set; } = 7.5f;

    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_TouchSpeed)]
    [Range(0, int.MaxValue)]
    [JsonProperty("touchSpeed")]
    public float TouchSpeed { get; set; } = 7.5f;

    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_SmoothSlideAnime)]
    [JsonProperty("smoothSlideAnime")]
    public bool SmoothSlideAnime { get; set; } = true;

    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_BackgroundDim)]
    [Range(0, 1)]
    [JsonProperty("backgroundDim")]
    public float BackgroundDim { get; set; } = 0.7f;

    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_BackgroundOutsideDim)]
    [Range(0, 1)]
    [JsonProperty("backgroundOutsideDim")]
    public float BackgroundOutsideDim { get; set; } = 0.3f;

    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_ComboStatusType)]
    [JsonProperty("comboStatusType")]
    public BgInfoDisplay ComboStatusType { get; set; } = BgInfoDisplay.Combo;


    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_AutoMode)]
    [JsonProperty("autoMode")]
    public AutoPlayMode AutoMode { get; set; } = AutoPlayMode.Enable;

    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_ShowHand)]
    [JsonProperty("showHand")]
    public bool ShowHand { get; set; } = false;

    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_UIType)]
    [JsonProperty("uiType")]
    public UIType UIType { get; set; } = UIType.Legacy;

    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_GlobalAudioOffset)]
    [JsonProperty("globalAudioOffset")]
    public double GlobalAudioOffset { get; set; } = 0;

    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_LegacySlideLayer)]
    [JsonProperty("legacySlideLayer")]
    public bool LegacySlideLayer { get; set; } = false;

    [Category(LangKeys.Cat_View_PlayBackSetting)]
    [DisplayName(LangKeys.Set_MineAutoSlide)]
    [JsonProperty("mineAutoSlide")]
    public bool MineAutoSlide { get; set; } = true;


    [Category(LangKeys.Cat_View_OutputSetting)]
    [DisplayName(LangKeys.Set_OutputFps)]
    [Range(1, int.MaxValue)]
    [JsonProperty("outputFps")]
    public int OutputFps { get; set; } = 60;

    [Category(LangKeys.Cat_View_OutputSetting)]
    [DisplayName(LangKeys.Set_ExportQuality)]
    [JsonProperty("exportQuality")]
    public ExportQuality ExportQuality { get; set; } = ExportQuality.High;

    [Category(LangKeys.Cat_View_OutputSetting)]
    [DisplayName(LangKeys.Set_ResizeBg)]
    [JsonProperty("resizeBg")]
    public bool ResizeBg { get; set; } = false;
}

public enum BgInfoDisplay
{
    [EnumDisplayName("None")] None,
    [EnumDisplayName("Combo")] Combo,
    [EnumDisplayName("Achievement - (Deluxe, 101)")] Achievement_101,
    [EnumDisplayName("Achievement - (Deluxe, 100)")] Achievement_100,
    [EnumDisplayName("Achievement + (Deluxe)")] Achievement,
    [EnumDisplayName("Achievement + (Classic)")] AchievementClassical,
    [EnumDisplayName("Achievement - (Classic, 100)")] AchievementClassical_100,
    [EnumDisplayName("Deluxe Score +")] DXScore,
    [EnumDisplayName("Deluxe Score -")] DXScore_Dec,
    [EnumDisplayName("S Border")] S_Border,
    [EnumDisplayName("SS Border")] SS_Border,
    [EnumDisplayName("SSS Border")] SSS_Border,
}

public enum UIType
{
    Legacy,
    TrgUI
}

public enum ExportQuality
{
    Low = 0,
    Medium = 1,
    High = 2,
    Ultra = 3,
}
public enum AutoPlayMode
{
    Enable,
    DJAutoButton,
    DJAutoSensor,
    Random,
    Disable
}
