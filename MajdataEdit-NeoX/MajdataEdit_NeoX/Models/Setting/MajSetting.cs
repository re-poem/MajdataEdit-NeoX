using MajdataEdit_NeoX.Lang;
using PropertyModels.ComponentModel;
using System.ComponentModel;

namespace MajdataEdit_NeoX.Models.Setting;

public class MajSetting
{
    [DisplayName(LangKeys.Cat_EditSetting)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [ExpandableObjectDisplayMode(IsCategoryBuiltinOrder = NullableBooleanType.Yes)]
    public MajEditSetting EditSetting { get; set; } = new();

    [DisplayName(LangKeys.Cat_ViewSetting)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [ExpandableObjectDisplayMode(IsCategoryBuiltinOrder = NullableBooleanType.Yes)]
    public MajViewSetting ViewSetting { get; set; } = new();

    [DisplayName(LangKeys.Cat_VolumeSetting)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [ExpandableObjectDisplayMode(IsCategoryBuiltinOrder = NullableBooleanType.Yes)]
    public MajVolumeSetting VolumeSetting { get; set; } = new();

    [Browsable(false)]
    public MajWindowSetting WindowSetting { get; set; } = new();
}
