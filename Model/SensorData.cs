#nullable enable

namespace Siliconvalve.Demo.Model
{
     using System.Text.Json.Serialization;
       
    public class SensorData
    {

        [JsonPropertyName("name")]
        public string? SensorName { get; set; }
        [JsonPropertyName("readtime")]
        public string? ReadingTime { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("pressure")]
        public double PressureMillibars { get; set; }

        [JsonPropertyName("humidity")]
        public int Humidity { get; set; }
        
        [JsonPropertyName("temperature")]
        public int Temperature { get; set; }

        [JsonPropertyName("pm25atma")]
        public double Pm25ChannelA { get; set; }

        [JsonPropertyName("pm25atmb")]
        public double Pm25ChannelB { get; set; }

        [JsonPropertyName("pm100atma")]
        public double Pm10ChannelA { get; set; }

        [JsonPropertyName("pm100atmb")]
        public double Pm10ChannelB { get; set; }
   }
}