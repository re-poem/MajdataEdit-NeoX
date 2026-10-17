using System;

namespace MajdataEdit_NeoX.Models.IO;

public readonly record struct ChartEditRecord(
    int SelectedDifficulty,
    double TrackTime,
    TimeSpan TotalEditDuration,
    string ChartPath);
