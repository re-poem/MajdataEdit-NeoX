using MajdataEdit_NeoX.Lang;
using PropertyModels.ComponentModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MajdataEdit_NeoX.Models.Setting;

// 反序列化时把 List<string> 之类的集合整体替换掉，而不是往默认实例里追加
[JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
public class MajSetting
{
    [Category(LangKeys.Cat_EditSetting)]
    [DisplayName("\0")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [ExpandableObjectDisplayMode(IsCategoryBuiltinOrder = NullableBooleanType.Yes)]
    public MajEditSetting EditSetting { get; set; } = new();

    [Category(LangKeys.Cat_ViewSetting)]
    [DisplayName("\0")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [ExpandableObjectDisplayMode(IsCategoryBuiltinOrder = NullableBooleanType.Yes)]
    public MajViewSetting ViewSetting { get; set; } = new();

    [Category(LangKeys.Cat_VolumeSetting)]
    [DisplayName("\0")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [ExpandableObjectDisplayMode(IsCategoryBuiltinOrder = NullableBooleanType.Yes)]
    public MajVolumeSetting VolumeSetting { get; set; } = new();

    [Browsable(false)]
    public MajWindowSetting WindowSetting { get; set; } = new();
}
