using System.ComponentModel;
using Newtonsoft.Json;

namespace LilyMarket
{
    [Serializable]
    public class Settings
    {
        public int MaxBalanceUsage;
        public DelaysContainer Delays;
        public ProductTarget[] Targets;

        public Settings()
        {
            Targets = new List<ProductTarget>
            {
                new()
                {
                    Text = "example1",
                    StartPage = 1,
                    PagesCount = 2,
                    UnitPrice = 1_000_000,
                    ScrollsCount = 5
                },
                new()
                {
                    Text = "example2",
                    StartPage = 1,
                    PagesCount = 3,
                    UnitPrice = 2_000_000,
                    ScrollsCount = 5
                }
            }.ToArray();
            
            Delays = new DelaysContainer();
            Delays.AfterMoveDelays = new DelaysContainer.AfterMoveDelaysContainer();
            MaxBalanceUsage = 0;
        }

        [Serializable]
        public class DelaysContainer
        {
            [DefaultValue(4000)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int TimeoutRestart;
            [DefaultValue(1500)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int OnFirstPage;
            [DefaultValue(32)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int MouseAfterMove;
            [DefaultValue(32)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int MouseAfterClick;
            [DefaultValue(80)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int AfterScroll;
            [DefaultValue(64)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int AfterHold;
            [DefaultValue(16)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int DoubleCheckTimeout;

            public AfterMoveDelaysContainer AfterMoveDelays;
            
            [Serializable]
            public class AfterMoveDelaysContainer
            {
                [DefaultValue(48)]
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
                public int SearchTextBox;
                [DefaultValue(28)]
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
                public int SearchButton;
                [DefaultValue(28)]
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
                public int Scroll;
                [DefaultValue(48)]
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
                public int ScrollDrop;
                [DefaultValue(28)]
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
                public int Slot;
                [DefaultValue(48)]
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
                public int BuySlot;
                [DefaultValue(48)]
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
                public int OkConfirm;
                [DefaultValue(48)]
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
                public int Page;
            }
        }

        [NonSerialized] internal const string Path = "settings.json";
        public void Save() => File.WriteAllText(Path, JsonConvert.SerializeObject(this, Formatting.Indented));

        public static async Task<Settings> Load()
        {
            if (!File.Exists(Path) || File.ReadAllText(Path) == string.Empty)
            {
                var settings = new Settings();
                settings.Save();
                return settings;
            }
            try
            {
                var text = await File.ReadAllTextAsync(Path);
                return JsonConvert.DeserializeObject<Settings>(text);
            }
            catch
            {
                var settings = new Settings();
                return settings;
            }
        }
    }
}