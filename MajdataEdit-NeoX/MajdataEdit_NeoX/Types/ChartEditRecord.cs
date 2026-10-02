using System;

namespace MajdataEdit_NeoX.Types;

public sealed class ChartEditRecord
{
    public string ChartPath { get; set; } = string.Empty;
    public int SelectedDifficulty { get; set; }
    public double TrackTime { get; set; }
    public TimeSpan TotalEditDuration { get; set; }
}