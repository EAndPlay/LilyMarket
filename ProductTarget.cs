using System.ComponentModel;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace LilyMarket;

[JsonSerializable(typeof(ProductTarget))]
public class ProductTarget
{
    [DefaultValue("example_")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Text;
    [DefaultValue(1)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int StartPage;
    [DefaultValue(2)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int PagesCount;
    [DefaultValue(1_000_000)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int UnitPrice;
    [DefaultValue(5)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int ScrollsCount;
    [DefaultValue(0)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int MinUnitPrice; // for safety (tesseract is shit)
    [DefaultValue(0)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int MinPrice;
    [DefaultValue(100)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int MaxAvailableCount;
}