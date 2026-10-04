using MajdataEdit_NeoX.Lang;
using PropertyModels.ComponentModel;
using PropertyModels.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MajdataEdit_NeoX.Models.Setting;

public class MajEditSetting
{
    [DisplayName(nameof(Resources.Set_Language))]
    public string Language { get; set; } = "en-US";

    [DisplayName(nameof(Resources.Set_FontSize))]
    [Range(1, 100)]
    public float FontSize { get; set; } = 14f;

    [DisplayName(nameof(Resources.Set_AutoCheckUpdatesOnStartup))]
    public bool AutoCheckUpdatesOnStartup { get; set; } = true;

    [DisplayName(nameof(Resources.Set_WaveAnimated))]
    public bool WaveAnimated { get; set; } = true;


    [DisplayName(nameof(Resources.Set_WordWrap))]
    public bool WordWrap { get; set; } = true;

    [DisplayName(nameof(Resources.Set_BackgroundImagePath))]
    [PathBrowsable(Filters = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*")]
    public string BackgroundImagePath { get; set; } = "xxlb.png";

    [DisplayName(nameof(Resources.Set_AutoFocusView))]
    public bool AutoFocusView { get; set; } = false;
}
