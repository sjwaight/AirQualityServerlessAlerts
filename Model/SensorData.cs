#nullable enable

namespace Siliconvalve.Demo.Model
{
    using Newtonsoft.Json;
    using System;
       
    public class SensorData
    {

        [JsonProperty("name")]
        public string? SensorName { get; set; }

        [JsonProperty("readingtime")]
        public DateTime? ReadingTime { get; set; }

        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        [JsonProperty("longitude")]
        public double Longitude { get; set; }

        [JsonProperty("pressure")]
        public double PressureMillibars { get; set; }

        [JsonProperty("humidity")]
        public int Humidity { get; set; }
        
        [JsonProperty("temperature")]
        public int Temperature { get; set; }

        [JsonProperty("pm25atma")]
        public double Pm25ChannelA { get; set; }

        [JsonProperty("pm25atmb")]
        public double Pm25ChannelB { get; set; }

        [JsonProperty("pm100atma")]
        public double Pm10ChannelA { get; set; }

        [JsonProperty("pm100atmb")]
        public double Pm10ChannelB { get; set; }
   }
}