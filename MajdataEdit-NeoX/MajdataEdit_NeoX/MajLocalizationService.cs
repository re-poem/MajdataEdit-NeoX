using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.PropertyGrid.Localization;
using Avalonia.PropertyGrid.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_NeoX.Lang;
using PropertyModels.Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MajdataEdit_NeoX;

internal class MajLocalizationService : AssemblyJsonAssetLocalizationService
{
    public MajLocalizationService()
        : base(typeof(MajLocalizationService).Assembly)
    {
    }
}