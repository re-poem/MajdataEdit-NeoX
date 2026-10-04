using MajdataEdit_NeoX.Lang;
using System.ComponentModel;

namespace MajdataEdit_NeoX.Models.Setting;

public class MajSetting
{
    [DisplayName(nameof(Resources.Cat_EditSetting))]
    public MajEditSetting EditSetting { get; set; } = new();

    [DisplayName(nameof(Resources.Cat_ViewSetting))]
    public MajViewSetting ViewSetting { get; set; } = new();

    [DisplayName(nameof(Resources.Cat_VolumeSetting))]
    public MajVolumeSetting VolumeSetting { get; set; } = new();

    [Browsable(false)]
    public MajWindowSetting WindowSetting { get; set; } = new();
}
