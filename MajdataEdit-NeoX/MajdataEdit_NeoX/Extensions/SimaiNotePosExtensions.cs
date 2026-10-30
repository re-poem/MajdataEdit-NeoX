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
            <= SimaiNotePos.BTN9 => (int)pos,
            <= SimaiNotePos.A8 => (int)pos - (int)SimaiNotePos.A1 + 1,
            <= SimaiNotePos.B8 => (int)pos - (int)SimaiNotePos.B1 + 1,
            SimaiNotePos.C1 or SimaiNotePos.C2 => 1,
            <= SimaiNotePos.D8 => (int)pos - (int)SimaiNotePos.D1 + 1,
            <= SimaiNotePos.E8 => (int)pos - (int)SimaiNotePos.E1 + 1,
            _ => -1,
        };
    }
}
