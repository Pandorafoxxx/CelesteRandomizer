﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Monocle;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace Celeste.Mod.Randomizer
{
    public class RandoConfigFile
    {
        // null means the side does not exist. zero rooms means the side needs to be lazy-loaded
        public List<RandoConfigRoom> ASide { get; set; }
        public List<RandoConfigRoom> BSide { get; set; }
        public List<RandoConfigRoom> CSide { get; set; }

        public static RandoConfigFile LoadAll(AreaData area, bool lazy = true)
        {
            var result = LoadSingle($"Config/{area.SID}.rando", false);
            if (result != null)
            {
                return result;
            }

            result = new RandoConfigFile();
            var partialResult = LoadSingle($"Config/{area.SID}.A.rando", lazy);
            if (partialResult?.ASide != null)
            {
                result.ASide = partialResult.ASide;
            }
            partialResult = LoadSingle($"Config/{area.SID}.B.rando", lazy);
            if (partialResult?.BSide != null)
            {
                result.BSide = partialResult.BSide;
            }
            partialResult = LoadSingle($"Config/{area.SID}.C.rando", lazy);
            if (partialResult?.CSide != null)
            {
                result.CSide = partialResult.CSide;
            }

            if (result.ASide == null && result.BSide == null && result.CSide == null)
            {
                // is this necessary? not sure
                return null;
            }
            return result;
        }

        public static RandoConfigFile LoadSingle(string fullPath, bool lazy = true)
        {
            Logger.Log("randomizer", $"Loading config from {fullPath}");
            if (!Everest.Content.TryGet(fullPath, out ModAsset asset))
            {
                Logger.Log("randomizer", "...not found");
                return null;
            }
            else if (lazy)
            {
                return new RandoConfigFile
                {
                    ASide = new List<RandoConfigRoom>(),
                    BSide = new List<RandoConfigRoom>(),
                    CSide = new List<RandoConfigRoom>(),
                };
            }
            else
            {
                using (StreamReader reader = new StreamReader(asset.Stream))
                {
                    try
                    {
                        return YamlHelper.Deserializer.Deserialize<RandoConfigFile>(reader);
                    }
                    catch (YamlException e)
                    {
                        throw new Exception($"Error parsing {fullPath}", e);
                    }
                }
            }
        }

        public static Dictionary<string, RandoConfigRoom> LazyReload(AreaKey key)
        {
            char side;
            switch (key.Mode)
            {
                case AreaMode.Normal:
                    side = 'A';
                    break;
                case AreaMode.BSide:
                    side = 'B';
                    break;
                case AreaMode.CSide:
                    side = 'C';
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var path = $"Config/{key.GetSID()}.{side}.rando";
            var result = LoadSingle(path, false);
            return result?.GetRoomMapping(key.Mode);
        }

        public static void YamlSkeleton(MapData map, bool doUnknown = true)
        {
            foreach (LevelData lvl in map.Levels)
            {
                if (lvl.Dummy)
                {
                    continue;
                }
                List<Hole> holes = RandoLogic.FindHoles(lvl);
                if (holes.Count > 0)
                {
                    Logger.Log("randomizer", $"  - Room: \"{lvl.Name}\"");
                    Logger.Log("randomizer", "    Holes:");
                }
                ScreenDirection lastDirection = ScreenDirection.Up;
                int holeIdx = -1;
                foreach (Hole hole in holes)
                {
                    if (hole.Side == lastDirection)
                    {
                        holeIdx++;
                    }
                    else
                    {
                        holeIdx = 0;
                        lastDirection = hole.Side;
                    }

                    LevelData targetLvl = map.GetAt(hole.LowCoord(lvl.Bounds)) ?? map.GetAt(hole.HighCoord(lvl.Bounds));
                    var unknown = targetLvl == null || targetLvl.Dummy;
                    if (unknown && !doUnknown)
                    {
                        continue;
                    }

                    Logger.Log("randomizer", $"    - Side: {hole.Side}");
                    Logger.Log("randomizer", $"      Idx: {holeIdx}");
                    Logger.Log("randomizer", "      Kind: " + (unknown ? "unknown" : "inout"));
                }
            }
        }

        public static void YamlSkeleton(AreaData area, bool doUnknown = true)
        {
            if (area.Mode[0] != null)
            {
                Logger.Log("randomizer", "ASide:");
                YamlSkeleton(area.Mode[0].MapData, doUnknown);
            }
            if (area.Mode.Length > 1 && area.Mode[1] != null)
            {
                Logger.Log("randomizer", "BSide:");
                YamlSkeleton(area.Mode[1].MapData, doUnknown);
            }
            if (area.Mode.Length > 2 && area.Mode[2] != null)
            {
                Logger.Log("randomizer", "CSide:");
                YamlSkeleton(area.Mode[2].MapData, doUnknown);
            }
        }

        [Command("rando_skeleton", "Dumps a starting point for a rando.yaml configuration for a given map to log.txt")]
        public static void YamlSkeletonCommand(string sid, bool doUnknown = true)
        {
            var area = AreaData.Get(sid);
            YamlSkeleton(area, doUnknown);
        }

        public Dictionary<String, RandoConfigRoom> GetRoomMapping(AreaMode mode)
        {
            List<RandoConfigRoom> rooms;
            switch (mode)
            {
                case AreaMode.Normal:
                default:
                    rooms = this.ASide;
                    break;
                case AreaMode.BSide:
                    rooms = this.BSide;
                    break;
                case AreaMode.CSide:
                    rooms = this.CSide;
                    break;
            }

            if (rooms == null)
            {
                return null;
            }

            var result = new Dictionary<String, RandoConfigRoom>();
            foreach (RandoConfigRoom room in rooms)
            {
                result.Add(room.Room, room);
            }

            return result;
        }
    }

    public class RandoConfigRoom
    {
        public String Room;
        public List<RandoConfigCollectable> Collectables = new List<RandoConfigCollectable>();
        public List<RandoConfigHole> Holes { get; set; } = new List<RandoConfigHole>();
        public List<RandoConfigRoom> Subrooms { get; set; }
        public List<RandoConfigInternalEdge> InternalEdges { get; set; }

        public bool End
        {
            get => this.ReqEnd != null;
            set => this.ReqEnd = value ? new RandoConfigReq() : null;
        }

        public RandoConfigReq ReqEnd { get; set; }
        public bool Hub { get; set; }
        public List<RandoConfigEdit> Tweaks { get; set; }
        public RandoConfigCoreMode Core { get; set; }
        public List<RandoConfigRectangle> ExtraSpace { get; set; }
        public float? Worth;
        public bool SpinnersShatter;
        public List<string> Flags;
    }

    public class RandoConfigRectangle
    {
        public int X, Y;
        public int Width, Height;
    }

    public class RandoConfigHole
    {
        public ScreenDirection Side { get; set; }
        public int Idx { get; set; }
        public int? LowBound { get; set; }
        public int? HighBound { get; set; }
        public bool? HighOpen { get; set; }

        public RandoConfigReq ReqIn { get; set; }
        public RandoConfigReq ReqOut { get; set; }
        public RandoConfigReq ReqBoth
        {
            get => null;

            set
            {
                this.ReqIn = value;
                this.ReqOut = value;
            }
        }
        public HoleKind Kind { get; set; }
        public int? Launch;
        public bool New;
        public RandoConfigHole Split;
    }

    public class RandoConfigCollectable
    {
        public int? Idx;
        public int? X;
        public int? Y;
        public bool MustFly;
    }

    public class RandoConfigInternalEdge
    {
        public String To { get; set; }
        public String Warp { get; set; }
        public RandoConfigReq ReqIn { get; set; }
        public RandoConfigReq ReqOut { get; set; }
        public RandoConfigReq ReqBoth
        {
            get => null;

            set
            {
                this.ReqIn = value;
                this.ReqOut = value;
            }
        }

        public enum SplitKind
        {
            TopToBottom,
            BottomToTop,
            LeftToRight,
            RightToLeft,
        }

        public SplitKind? Split;

        public int? Collectable;
        public bool CustomWarp;
    }

    public class RandoConfigReq
    {
        public List<RandoConfigReq> And;
        public List<RandoConfigReq> Or;

        public Difficulty Difficulty = Difficulty.Normal;
        public NumDashes? Dashes;
        public bool Key;
        public int? KeyholeID;
        public string Flag;
    }

    public class RandoConfigEdit
    {
        public String Name { get; set; }
        public int? ID { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public RandoConfigDecalType Decal { get; set; }
        public RandoConfigUpdate Update { get; set; }
    }

    public class RandoConfigUpdate
    {
        public bool Remove { get; set; }
        public bool Add { get; set; }
        public bool Default { get; set; }

        public float? X { get; set; }
        public float? Y { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public float? ScaleX { get; set; }
        public float? ScaleY { get; set; }
        public List<RandoConfigNode> Nodes { get; set; }
        public Dictionary<string, string> Values { get; set; }
        public string Tile;
    }

    public enum RandoConfigDecalType
    {
        None, FG, BG,
    }

    public class RandoConfigNode
    {
        public int Idx { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
    }

    public class RandoConfigCoreMode
    {
        private Session.CoreModes? left, right, up, down;
        public Session.CoreModes All = Session.CoreModes.None;

        public Session.CoreModes Left
        {
            get => left ?? All;
            set => left = value;
        }

        public Session.CoreModes Right
        {
            get => right ?? All;
            set => right = value;
        }

        public Session.CoreModes Up
        {
            get => up ?? All;
            set => up = value;
        }

        public Session.CoreModes Down
        {
            get => down ?? All;
            set => down = value;
        }
    }

    public class RandoMetadataFile
    {
        public List<string> CollectableNames = new List<string>();
        public List<RandoMetadataMusic> Music = new List<RandoMetadataMusic>();
        public List<RandoMetadataCampaign> Campaigns = new List<RandoMetadataCampaign>();
        public List<RandoMetadataBackground> Backgrounds = new List<RandoMetadataBackground>();
        public List<RandoMetadataBackground> BgEffects = new List<RandoMetadataBackground>();
        public List<RandoMetadataBackground> FgEffects = new List<RandoMetadataBackground>();

        [YamlIgnore] public Dictionary<string, RandoMetadataRuleset> RulesetsDict = new Dictionary<string, RandoMetadataRuleset>();

        public List<RandoMetadataRuleset> Rulesets
        {
            get => new List<RandoMetadataRuleset>(this.RulesetsDict.Values);
            set
            {
                foreach (var r in value)
                {
                    if (String.IsNullOrEmpty(r.Name))
                    {
                        throw new Exception("Rulesets must have Name specified");
                    }
                    if (this.RulesetsDict.ContainsKey(r.Name))
                    {
                        throw new Exception($"Ruleset name '{r.Name}' is duplicated");
                    }
                    this.RulesetsDict[r.Name] = r;
                }
            }
        }

        public void Add(RandoMetadataFile other)
        {
            this.CollectableNames.AddRange(other.CollectableNames);
            this.Music.AddRange(other.Music.Where(t => t.IsLoaded()));
            this.Campaigns.AddRange(other.Campaigns);
            this.Backgrounds.AddRange(other.Backgrounds);
            this.BgEffects.AddRange(other.BgEffects);
            this.FgEffects.AddRange(other.FgEffects);
            this.Rulesets = other.Rulesets; // rely on setter behavior to use this as an updater
        }

        public static RandoMetadataFile LoadAll()
        {
            var result = new RandoMetadataFile();

            Regex r = new Regex("^[^\\\\/]+:(/|\\\\).*$");
            foreach (var kv in Everest.Content.Map.Where(kv => !r.IsMatch(kv.Key) && Path.GetFileName(kv.Value.PathVirtual) == "rando" && kv.Value.Type == typeof(AssetTypeYaml)))
            {
                Logger.Log("randomizer", $"Found metadata {kv.Value.PathVirtual} in {kv.Value.Source.Name}");
                result.Add(Load(kv.Value));
            }
            return result;
        }

        private static RandoMetadataFile Load(ModAsset asset)
        {
            // do not catch errors, they should crash on load
            using (StreamReader reader = new StreamReader(asset.Stream))
            {
                return YamlHelper.Deserializer.Deserialize<RandoMetadataFile>(reader);
            }
        }
    }

    public class RandoMetadataMusic
    {
        public string Name;
        private float weight = 1;
        public Dictionary<string, int> Parameters = new Dictionary<string, int>();

        public float Weight
        {
            get => this.weight;
            set => this.weight = (value >= 0 && value <= 3) ? value : 1f;
        }

        public bool IsLoaded() => Audio.GetEventDescription(this.Name) != null;
    }

    public class RandoMetadataCampaign
    {
        public string Name;
        public List<RandoMetadataLevelSet> LevelSets;
    }

    public class RandoMetadataLevelSet
    {
        public string Name;
        public string ID;
    }

    public class RandoMetadataRuleset
    {
        public string Name;
        private string longName;

        public string LongName
        {
            get => this.longName ?? "Ruleset " + this.Name;
            set => this.longName = value;
        }

        public List<RandoSettings.AreaKeyNotStupid> EnabledMaps = null;
        public bool RepeatRooms = false;
        public bool EnterUnknown = false;
        public bool Variants = false;
        public ShineLights Lights = ShineLights.Hubs;
        public Darkness Darkness = Darkness.Never;

        public LogicType Algorithm = LogicType.Pathway;
        public MapLength Length = MapLength.Short;
        public NumDashes Dashes = NumDashes.One;
        public Difficulty Difficulty = Difficulty.Normal;
        public DifficultyEagerness DifficultyEagerness = DifficultyEagerness.Medium;
        public StrawberryDensity Strawberries = StrawberryDensity.None;
        public int Lives = 0;
    }

    public class RandoMetadataBackground
    {
        public string Texture;
        public string Effect;
        public int CoverTop;
        public int CoverBottom;
        public bool Opaque;
        public bool LoopX = true, LoopY;
        public bool FlipX, FlipY;
        public bool NeedsColor;
        public float ScrollFactorX = 1f, ScrollFactorY = 1f;
        public bool ProvidesWind;
        public float Alpha = 1f;
        public float SpeedX, SpeedY;
        public int OffX, OffY;
        public string BlendMode = "alphablend";

        public RandoMetadataBackground AndThen;
    }
}
