using MajdataEdit_NeoX.Lang;
using PropertyModels.ComponentModel;
using System.ComponentModel;

namespace MajdataEdit_NeoX.Models.Setting;

public class MajSetting
{
    [Category(LangKeys.Cat_EditSetting)]
    [DisplayName("")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [ExpandableObjectDisplayMode(IsCategoryBuiltinOrder = NullableBooleanType.Yes)]
    public MajEditSetting EditSetting { get; set; } = new();

    [Category(LangKeys.Cat_ViewSetting)]
    [DisplayName("")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [ExpandableObjectDisplayMode(IsCategoryBuiltinOrder = NullableBooleanType.Yes)]
    public MajViewSetting ViewSetting { get; set; } = new();

    [Category(LangKeys.Cat_VolumeSetting)]
    [DisplayName("")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [ExpandableObjectDisplayMode(IsCategoryBuiltinOrder = NullableBooleanType.Yes)]
    public MajVolumeSetting VolumeSetting { get; set; } = new();

    [Browsable(false)]
    public MajWindowSetting WindowSetting { get; set; } = new();
}
