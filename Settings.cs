using Newtonsoft.Json;

namespace LilyMarket
{
    [Serializable]
    public class Settings
    {
        public int StartPage;
        public int PagesCount;
        public int MinimalUnitPrice;
        public int ScrollsCount;
        public bool Screenshots;

        public Settings()
        {
            StartPage = 1;
            PagesCount = 1;
            MinimalUnitPrice = 0;
            ScrollsCount = 5;
            Screenshots = false;
        }

        [NonSerialized] internal const string Path = "settings.json";
        public void Save() => File.WriteAllText(Path, JsonConvert.SerializeObject(this, Formatting.Indented));

        public static async Task<Settings> Load()
        {
            if (!File.Exists(Path) || File.ReadAllText(Path) == string.Empty) return new Settings();
            try
            {
                var text = await File.ReadAllTextAsync(Path);
                return JsonConvert.DeserializeObject<Settings>(text);
            }
            catch
            {
                return new Settings();
            }
        }
    }
}