using IoTHubTrigger = Microsoft.Azure.WebJobs.EventHubTriggerAttribute;

using Newtonsoft.Json;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.EventHubs;
using System.Text;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Siliconvalve.Demo.Model;

namespace Siliconvalve.Demo
{
    public class ProcessDeviceEvent
    {
        [FunctionName("ProcessDeviceEvent")]
        public static async Task Run(
            [IoTHubTrigger("messages/events", Connection = "IOT_HUB_CONNECTION")]EventData[] eventHubMessages, 
            [CosmosDB(
                databaseName: "%AIRDATA_COSMOS_DB%",
                collectionName: "%AIRDATA_COSMOS_COLLECTION%",
                ConnectionStringSetting = "COSMOS_DB_CONNECTION")]
                IAsyncCollector<SensorData> sensortDataOut,
            ILogger log)
        {
            log.LogInformation($"Processing {eventHubMessages.Length} messages.");
          
            try
            {  
                foreach (var message in eventHubMessages)
                {
                    log.LogInformation($"Message enqueue time: {message.SystemProperties.EnqueuedTimeUtc}");

                    var sensorReading = JsonConvert.DeserializeObject<SensorData>(Encoding.UTF8.GetString(message.Body));
                    await sensortDataOut.AddAsync(sensorReading);
                }
            }
            catch (Exception e)
            {
                log.LogError(e,$"Detection error. {e.Message}");
                throw;
            }
        }
    }
}