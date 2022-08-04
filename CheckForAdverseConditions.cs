using System;
using System.Collections.Generic;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

using Azure.AI.AnomalyDetector.Models;
using Azure.AI.AnomalyDetector;
using System.Text;
using System.Text.Json;
using Siliconvalve.Demo.Model;
using Azure.Identity;
using Azure;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;

namespace Siliconvalve.Demo
{
    public class CheckForAdverseConditions
    {
        [Disable()]
        [FunctionName("CheckForAdverseConditions")]
        public async Task Run([CosmosDBTrigger(
            databaseName: "%AIRDATA_COSMOS_DB%",
            collectionName: "%AIRDATA_COSMOS_COLLECTION%",
            ConnectionStringSetting = "COSMOS_DB_CONNECTION",
            LeaseCollectionName = "leases")]IReadOnlyList<Document> input,
            ILogger log)
        {
            if (input != null && input.Count > 0)
            {
                log.LogInformation($"Successfully triggered with first document in change feed being {input[0].Id}");

                /////
                // Exec Stored Proc on Cosmos DB to retrieve all records in collection.
                // Note: Assumes source Cosmos DB Collection has low TTL with less than 50 documents stored.
                //       Number of returned documents must exceed the slidingWindow value set in your Anomaly Detector model.
                /////

                IReadOnlyList<SensorData> dataBlock = null;

                using (var dbClient = new DocumentClient(new Uri(Environment.GetEnvironmentVariable("COSMOS_HOST")), Environment.GetEnvironmentVariable("COSMOS_KEY")))  
                {
                    var result = await dbClient.ExecuteStoredProcedureAsync<IReadOnlyList<SensorData>>  
                                                    (UriFactory.CreateStoredProcedureUri(Environment.GetEnvironmentVariable("AIRDATA_COSMOS_DB"), Environment.GetEnvironmentVariable("AIRDATA_COSMOS_COLLECTION"), "readAllRecords"));  
                    
                    if(result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        dataBlock = result.Response;
                    }
                }

                /////
                // Prepare Data to submit to Anomaly Detector API
                /////

                var readingTimes = new List<string>();
                var temperatureReadings = new List<float>();
                var humidityReadings = new List<float>();
                var pm10Readings = new List<float>();
                var pm25Readings = new List<float>();
               
                foreach (var sensorDataItem in dataBlock)
                {
                    // take the highest reading for each
                    var pm25Reading = (sensorDataItem.Pm25ChannelA > sensorDataItem.Pm25ChannelB) ? sensorDataItem.Pm25ChannelA : sensorDataItem.Pm25ChannelB;
                    var pm10Reading = (sensorDataItem.Pm10ChannelA > sensorDataItem.Pm10ChannelB) ? sensorDataItem.Pm10ChannelA : sensorDataItem.Pm10ChannelB;

                    readingTimes.Add(sensorDataItem.ReadingTime);
                    temperatureReadings.Add(sensorDataItem.Temperature);
                    humidityReadings.Add(sensorDataItem.Humidity);
                    pm10Readings.Add(((float)pm10Reading));
                    pm25Readings.Add(((float)pm25Reading));
                }

                var variables = new List<VariableValues>()
                {
                    new VariableValues("temperature", readingTimes, temperatureReadings),   
                    new VariableValues("humidity", readingTimes, humidityReadings),
                    new VariableValues("pm10", readingTimes, pm10Readings),
                    new VariableValues("pm25", readingTimes, pm25Readings)
                };

                /////
                // Call Azure Anomaly Detector API
                /////
            
                var endpointUri = new Uri(Environment.GetEnvironmentVariable("ANOMALY_DETECTOR_ENDPOINT"));
                var credential = new AzureKeyCredential(Environment.GetEnvironmentVariable("ANOMALY_DETECTOR_KEY"));
                var modelId = new Guid(Environment.GetEnvironmentVariable("ANOMALY_DETECTOR_MODEL_ID"));

                //create client
                AnomalyDetectorClient client = new AnomalyDetectorClient(endpointUri, credential);

                try
                {           

                    LastDetectionRequest request = new LastDetectionRequest(variables,dataBlock.Count);
                    LastDetectionResult result = await client.LastDetectAnomalyAsync(modelId,request);

                    if(result.Results != null && result.Results.Count > 0)
                    {
                        if (result.Results[0].Value.IsAnomaly)
                        {
                            log.LogInformation("!! ANOM: The latest data points were identified as anomalous.");
                        }
                        else
                        {
                            log.LogInformation("__ NOANOM: The latest data points were *not* identified as anomalous.");
                        }
                    } 
                    else
                    {
                        log.LogWarning("Received no results back from Anomaly Detector API.");
                    }
                }
                catch (Exception e)
                {
                    log.LogError(e,$"Detection error. {e.Message}");
                }       


            }
        }
    }
}
