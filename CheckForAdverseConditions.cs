using System;
using System.Collections.Generic;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Azure.AI.AnomalyDetector.Models;
using Azure.AI.AnomalyDetector;
using Newtonsoft.Json;
using Siliconvalve.Demo.Model;
using Azure;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.Documents.Client;
using Twilio.Rest.Api.V2010.Account;
using Twilio;

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
                            [CosmosDB(
                                databaseName: "%AIRDATA_COSMOS_DB%",
                                collectionName: "%ALERT_FLAG_COLLECTION%",
                                ConnectionStringSetting = "COSMOS_DB_CONNECTION",
                                PartitionKey = "%ALERT_FLAG_ITEM_ID%",
                                Id = "%ALERT_FLAG_ITEM_ID%"
                                )]AlertControlFlag alertSetting,
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

                var options = new RequestOptions { PartitionKey = new PartitionKey((Environment.GetEnvironmentVariable("AIRDATA_COSMOS_PARTITION_KEY"))) };

                using (var dbClient = new DocumentClient(new Uri(Environment.GetEnvironmentVariable("COSMOS_HOST")), Environment.GetEnvironmentVariable("COSMOS_KEY")))  
                {
                    var result = await dbClient.ExecuteStoredProcedureAsync<string>  
                                                    (UriFactory.CreateStoredProcedureUri(Environment.GetEnvironmentVariable("AIRDATA_COSMOS_DB"), 
                                                                                         Environment.GetEnvironmentVariable("AIRDATA_COSMOS_COLLECTION"), 
                                                                                         Environment.GetEnvironmentVariable("AIRDATA_COSMOS_SPROC")), options);  
                    if(result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        dataBlock = JsonConvert.DeserializeObject<SensorData[]>(result.Response);
                    }
                }

                if(dataBlock != null)
                {

                    /////
                    // Prepare Data to submit to Anomaly Detector API
                    /////

                    var readingTimes = new List<string>() { "", "",""}; 
                    var temperatureReadings = new List<float>() { 1.23f, 2.21f, 1.1f };
                    var humidityReadings = new List<float>();
                    var pm10Readings = new List<float>();
                    var pm25Readings = new List<float>();
                
                    foreach (var sensorDataItem in dataBlock)
                    {
                        // take the highest reading for each
                        var pm25Reading = (sensorDataItem.Pm25ChannelA > sensorDataItem.Pm25ChannelB) ? sensorDataItem.Pm25ChannelA : sensorDataItem.Pm25ChannelB;
                        var pm10Reading = (sensorDataItem.Pm10ChannelA > sensorDataItem.Pm10ChannelB) ? sensorDataItem.Pm10ChannelA : sensorDataItem.Pm10ChannelB;

                        readingTimes.Add(sensorDataItem.ReadTime.ToString());
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

                    AnomalyDetectorClient client = new AnomalyDetectorClient(endpointUri, credential);

                    try
                    {           
                        // use last 10 data points for detection purposes
                        LastDetectionRequest request = new LastDetectionRequest(variables, 10);
                        LastDetectionResult result = await client.LastDetectAnomalyAsync(modelId, request);

                        if(result.Results != null && result.Results.Count > 0)
                        {
                            // see if any of the returned results contains an anomaly
                            bool containsAnomaly = result.Results.Any(r => r.Value.IsAnomaly);

                            if (containsAnomaly)
                            {
                                log.LogInformation("!! ANOM: The latest data points were identified as anomalous.");

                                if(alertSetting == null)
                                {
                                    log.LogInformation("!! ANOM: Sending SMS alert.");

                                    TwilioClient.Init(Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID"),Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN"));

                                    var message = MessageResource.Create(
                                        body: "Air Quality Alert! Check conditions and stay inside if unsafe!",
                                        from: new Twilio.Types.PhoneNumber(Environment.GetEnvironmentVariable("MESSAGE_SENDER")),
                                        to: new Twilio.Types.PhoneNumber(Environment.GetEnvironmentVariable("MESSAGE_RECIPIENT"))
                                    );

                                    using (var dbClient = new DocumentClient(new Uri(Environment.GetEnvironmentVariable("COSMOS_HOST")), Environment.GetEnvironmentVariable("COSMOS_KEY")))  
                                    {
                                        log.LogInformation("!! ANOM: Set alert flag to avoid re-sending alert on next data point.");

                                        // set flag in DB to stop re-sending for a period of time (determined by TTL on document in this collection)
                                        var response = await dbClient.CreateDocumentAsync(
                                                                    UriFactory.CreateDocumentCollectionUri(Environment.GetEnvironmentVariable("AIRDATA_COSMOS_DB"), 
                                                                                                           Environment.GetEnvironmentVariable("ALERT_FLAG_COLLECTION")), new AlertControlFlag { Id = Environment.GetEnvironmentVariable("ALERT_FLAG_ITEM_ID") });
                                        
                                        if(response.StatusCode != System.Net.HttpStatusCode.Created)
                                        {
                                            log.LogError($"Failed to create alert flag in Cosmos DB for Activity ID: {response.ActivityId}");
                                        } 
                                    }
                                }
                                else
                                {
                                    log.LogInformation("!! ANOM: Anomaly detected, but not sending SMS alert as alerts have been silenced.");
                                }
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
                else
                {
                     log.LogWarning("Unable to read or populate air quality data from Cosmos DB!");
                }      
            }
            else
            {
                log.LogWarning("Azure Function was triggered, but trigger payload was null or empty!");
            }
        }
    }
}