﻿namespace Dmrsv.Data
{
    [Obsolete]
    public class FilterOption
    {
        public int Except { get; set; } = 5;
        public Mode Mode { get; set; } = Mode.Freestyle;
        public Aider Aider { get; set; } = Aider.Off;
        public Level Level { get; set; } = Level.Off;
    }
}
