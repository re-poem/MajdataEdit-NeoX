using MajdataEdit_NeoX.Lang;
using Newtonsoft.Json;
using PropertyModels.ComponentModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MajdataEdit_NeoX.Models.Setting;

// 线格式契约：字段命名 / 默认值必须与 ViewX 端 MajVolumeSetting 完全一致。
// JSON 键由 [JsonProperty] 锁定；其余属性仅为 Edit 端 UI 元数据，不参与线格式。
public partial class MajVolumeSetting
{
    [DisplayName(nameof(SampleType.Track))]
    [Range(0, 1)]
    [Trackable(0, 1, Increment = 0.01f)]
    [JsonProperty("track")]
    public float Track { get; set; } = 0.9f;

    [DisplayName(nameof(SampleType.Answer))]
    [Range(0, 1)]
    [Trackable(0, 1, Increment = 0.01f)]
    [JsonProperty("answer")]
    public float Answer { get; set; } = 0.9f;

    [DisplayName(nameof(SampleType.Tap))]
    [Range(0, 1)]
    [Trackable(0, 1, Increment = 0.01f)]
    [JsonProperty("tap")]
    public float Tap { get; set; } = 0.9f;

    [DisplayName(nameof(SampleType.Slide))]
    [Range(0, 1)]
    [Trackable(0, 1, Increment = 0.01f)]
    [JsonProperty("slide")]
    public float Slide { get; set; } = 0.9f;

    [DisplayName(nameof(SampleType.Break))]
    [Range(0, 1)]
    [Trackable(0, 1, Increment = 0.01f)]
    [JsonProperty("break")]
    public float Break { get; set; } = 0.9f;

    [DisplayName(nameof(SampleType.BreakSlide))]
    [Range(0, 1)]
    [Trackable(0, 1, Increment = 0.01f)]
    [JsonProperty("breakSlide")]
    public float BreakSlide { get; set; } = 0.9f;

    [DisplayName(nameof(SampleType.Ex))]
    [Range(0, 1)]
    [Trackable(0, 1, Increment = 0.01f)]
    [JsonProperty("ex")]
    public float Ex { get; set; } = 0.9f;

    [DisplayName(nameof(SampleType.Touch))]
    [Range(0, 1)]
    [Trackable(0, 1, Increment = 0.01f)]
    [JsonProperty("touch")]
    public float Touch { get; set; } = 0.9f;

    [DisplayName(nameof(SampleType.Hanabi))]
    [Range(0, 1)]
    [Trackable(0, 1, Increment = 0.01f)]
    [JsonProperty("hanabi")]
    public float Hanabi { get; set; } = 0.9f;
}
public enum SampleType
{
    Track,
    Answer,
    Tap,
    Slide,
    Break,
    BreakSlide,
    Ex,
    Touch,
    Hanabi
}
