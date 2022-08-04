using IoTHubTrigger = Microsoft.Azure.WebJobs.EventHubTriggerAttribute;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventHubs;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Azure.AI.AnomalyDetector.Models;
using Azure.AI.AnomalyDetector;
using Azure.Identity;
using Azure;
using System;
using System.Collections.Generic;
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

                    var sensorReading = JsonSerializer.Deserialize<SensorData>(Encoding.UTF8.GetString(message.Body));
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