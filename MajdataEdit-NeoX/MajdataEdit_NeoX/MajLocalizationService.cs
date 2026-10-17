using Avalonia.PropertyGrid.Localization;

namespace MajdataEdit_NeoX;

internal class MajLocalizationService : AssemblyJsonAssetLocalizationService
{
    public MajLocalizationService()
        : base(typeof(MajLocalizationService).Assembly)
    {
    }
}