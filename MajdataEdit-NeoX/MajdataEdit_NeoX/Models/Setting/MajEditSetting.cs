using MajdataEdit_NeoX.Lang;
using PropertyModels.ComponentModel;
using PropertyModels.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace MajdataEdit_NeoX.Models.Setting;

public class MajEditSetting
{
    [Category(LangKeys.Cat_Edit_BaseSetting)]
    [DisplayName(LangKeys.Set_Language)]
    public string Language { get; set; } = "en-US";

    [Category(LangKeys.Cat_Edit_BaseSetting)]
    [DisplayName(LangKeys.Set_PlayerIps)]
    public List<string> PlayerIps { get; set; } = ["127.0.0.1"];

    [Category(LangKeys.Cat_Edit_BaseSetting)]
    [DisplayName(LangKeys.Set_AutoCheckUpdatesOnStartup)]
    public bool AutoCheckUpdatesOnStartup { get; set; } = true;


    [Category(LangKeys.Cat_Edit_AppearanceSetting)]
    [DisplayName(LangKeys.Set_WaveAnimated)]
    public bool WaveAnimated { get; set; } = true;

    [Category(LangKeys.Cat_Edit_AppearanceSetting)]
    [DisplayName(LangKeys.Set_AutoFocusView)]
    public bool AutoFocusView { get; set; } = false;

    [Category(LangKeys.Cat_Edit_AppearanceSetting)]
    [DisplayName(LangKeys.Set_FontSize)]
    [Range(1, 100)]
    public float FontSize { get; set; } = 14f;

    [Category(LangKeys.Cat_Edit_AppearanceSetting)]
    [DisplayName(LangKeys.Set_WordWrap)]
    public bool WordWrap { get; set; } = true;



    [Category(LangKeys.Cat_Edit_AppearanceSetting)]
    [DisplayName(LangKeys.Set_BackgroundImagePath)]
    [PathBrowsable(Filters = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*")]
    public string BackgroundImagePath { get; set; } = "xxlb.png";


}
