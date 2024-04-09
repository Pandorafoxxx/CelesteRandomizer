using System;
using System.Collections.Generic;

namespace Celeste.Mod.Randomizer
{
    public class RandoModuleSettings : EverestModuleSettings
    {
        [SettingIgnore]
        public int StartCounter { get; set; }

        [SettingIgnore]
        public Dictionary<uint, long> BestTimes { get; set; }

        [SettingIgnore]
        public Dictionary<string, RecordTuple> BestSetSeedTimes { get; set; }

        [SettingIgnore]
        public Dictionary<string, RecordTuple> BestRandomSeedTimes { get; set; }

        [SettingIgnore]
        public string CurrentVersion { get; set; }

        [SettingIgnore]
        public RandoSettings SavedSettings { get; set; }

        public bool FastMenu { get; set; }
        public bool LazyLoading { get; set; } = true;
    }

    public struct RecordTuple
    {
        public long Item1;
        public string Item2;

        public static RecordTuple Create(long a, string b)
        {
            return new RecordTuple
            {
                Item1 = a,
                Item2 = b,
            };
        }
    }

}
