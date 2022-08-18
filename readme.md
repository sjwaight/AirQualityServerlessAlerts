# Air Quality Alerts Pipeline

This repository contains an Azure Function app with two Functions.

- ProcessDeviceEvent: reads incoming events from an Azure IoT Hub (via the IoT Hub's Event Hub compatible endpoint) and writes the events to an Azure Cosmos DB Collection setup to support the SQL API.
- CheckForAdverseConditions: reads new records from Cosmos DB's change feed and uses this as a trigger to read a batch of air quality events from Cosmos DB which are then submitted to Azure Anomaly Detector for determine if adverse air conditions may exist. If an anomaly is found, the Function calls out to a Twilio API and sends an SMS to pre-determined mobile phone number.

Further information coming shortly.

### Sample local.settings.config

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName={your_acount};AccountKey={your_account_key};EndpointSuffix=core.windows.net",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "IOT_HUB_CONNECTION": "Endpoint=sb://{your_iothub_endpoint}.servicebus.windows.net/;SharedAccessKeyName=iothubowner;SharedAccessKey={your_iot_hub_key};EntityPath=iothub-ehub-{your_custom_endpoint}",
    "ANOMALY_DETECTOR_ENDPOINT": "https://{your_cog_services_account}.cognitiveservices.azure.com/",
    "ANOMALY_DETECTOR_KEY": "{your_detector_api_key}",
    "ANOMALY_DETECTOR_MODEL_ID" : "{your_detector_model_id}",
    "COSMOS_DB_CONNECTION": "AccountEndpoint=https://{your_cosmos_db_acount}.documents.azure.com:443/;AccountKey={your_cosmos_db_key}",
    "AIRDATA_COSMOS_DB": "airdata",
    "AIRDATA_COSMOS_COLLECTION": "airdataitems",
    "AIRDATA_COSMOS_SPROC": "GetAirQualityRecords",
    "AIRDATA_COSMOS_PARTITION_KEY": "{your_device_id}",
    "ALERT_FLAG_COLLECTION": "alertcontroller",
    "ALERT_FLAG_ITEM_ID": "alertflag",
    "COSMOS_HOST": "https://{your_cosmos_db_acount}.documents.azure.com:443/",
    "COSMOS_KEY": "{your_cosmos_db_key}",
    "TWILIO_ACCOUNT_SID": "{your_twilio_account_id}",
    "TWILIO_AUTH_TOKEN": "{your_twilio_account_token}",
    "MESSAGE_RECIPIENT": "{your_recipient_mobile}",
    "MESSAGE_SENDER": "{your_twilio_managed_sender_mobile}"
  }
}
```

### Cosmos DB Stored Procedure

This stored procedue is very basic and simply returns all documents in the collection. It assumes you have a TTL on the collection to ensure it never grows over a reasonable size (for our purpose we don't need more than 50 documents).

```javascript
// SAMPLE STORED PROCEDURE
function sample(prefix) {
    var collection = getContext().getCollection();

    // Query documents and take 1st item.
    var isAccepted = collection.queryDocuments(
        collection.getSelfLink(),
        'SELECT * FROM c',
    function (err, feed, options) {
        if (err) throw err;

        // Check the feed and if empty, set the body to 'no docs found', 
        // else take 1st element from feed
        if (!feed || !feed.length) {
            var response = getContext().getResponse();
            response.setBody('no docs found');
        }
        else {
            var response = getContext().getResponse();
            response.setBody(JSON.stringify(feed));
        }
    });

    if (!isAccepted) throw new Error('The query was not accepted by the server.');
}
```