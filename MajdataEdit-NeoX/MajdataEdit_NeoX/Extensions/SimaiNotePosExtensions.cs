using Cimai;
using System;
using System.Collections.Generic;
using System.Text;

namespace MajdataEdit_NeoX.Extensions;

public static class SimaiNotePosExtensions
{
    extension(SimaiNotePos pos)
    {
        public int GetPosNum() => pos switch
        {
            SimaiNotePos.NONE => 0,
            >= SimaiNotePos.BTN1 and <= SimaiNotePos.BTN8 => (int)pos,
            >= SimaiNotePos.A1 and <= SimaiNotePos.A8 => (int)(pos - SimaiNotePos.A1 + 1),
            >= SimaiNotePos.B1 and <= SimaiNotePos.B8 => (int)(pos - SimaiNotePos.B1 + 1),
            SimaiNotePos.C1 or SimaiNotePos.C2 => 1,
            >= SimaiNotePos.D1 and <= SimaiNotePos.D8 => (int)(pos - SimaiNotePos.D1 + 1),
            >= SimaiNotePos.E1 and <= SimaiNotePos.E8 => (int)(pos - SimaiNotePos.E1 + 1),
            _ => -1,
        };
    }
}
