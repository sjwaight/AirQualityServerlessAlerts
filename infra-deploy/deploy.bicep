param sites_swairmonitor_name string = 'airmonitor'
param account_iothub_airmonitor string = '${uniqueString(resourceGroup().id)}airmon'
param storageaccount_airmonitor_name string = '${uniqueString(resourceGroup().id)}airmon'
param hosting_plan_name string = 'ASP-airmon-${uniqueString(resourceGroup().id)}'
param account_cosmos_airmonitor string = 'swairmonitor'
param account_airanomdetect string = 'swairanomdetect'
param account_airanomdetect_container_data string = 'processeddata'
param streamingjobs_swairiottocsv_name string = 'swairiottocsv'
param deploy_location string = 'westus'

// Anomaly Detector Account (no model deployed)
resource anomaly_detector_cog_services_account 'Microsoft.CognitiveServices/accounts@2022-03-01' = {
  name: account_airanomdetect
  location: deploy_location
  sku: {
    name: 'F0'
  }
  kind: 'AnomalyDetector'
  identity: {
    type: 'None'
  }
  properties: {
    customSubDomainName: account_airanomdetect
    networkAcls: {
      defaultAction: 'Allow'
      virtualNetworkRules: []
      ipRules: []
    }
    publicNetworkAccess: 'Enabled'
  }
}

// IoT Hub
resource iothubs_airdata 'Microsoft.Devices/IotHubs@2022-04-30-preview' = {
  name: account_iothub_airmonitor
  location: deploy_location
  sku: {
    name: 'F1'
    capacity: 1
  }
  identity: {
    type: 'None'
  }
  properties: {
    ipFilterRules: []
    eventHubEndpoints: {
      events: {
        retentionTimeInDays: 1
        partitionCount: 2
      }
    }
    routing: {
      endpoints: {
        serviceBusQueues: []
        serviceBusTopics: []
        eventHubs: []
        storageContainers: []
        cosmosDBSqlCollections: []
      }
      routes: []
      fallbackRoute: {
        name: '$fallback'
        source: 'DeviceMessages'
        condition: 'true'
        endpointNames: [
          'events'
        ]
        isEnabled: true
      }
    } 
    messagingEndpoints: {
      fileNotifications: {
        lockDurationAsIso8601: 'PT1M'
        ttlAsIso8601: 'PT1H'
        maxDeliveryCount: 10
      }
    }
    enableFileUploadNotifications: false
    cloudToDevice: {
      maxDeliveryCount: 10
      defaultTtlAsIso8601: 'PT1H'
      feedback: {
        lockDurationAsIso8601: 'PT1M'
        ttlAsIso8601: 'PT1H'
        maxDeliveryCount: 10
      }
    }
    features: 'GWV2'
    disableLocalAuth: false
    allowedFqdnList: []
  }
}

// Cosmos DB Account, database and containers
resource cosmos_account_airmonitor 'Microsoft.DocumentDB/databaseAccounts@2022-05-15' = {
  name: account_cosmos_airmonitor
  location: deploy_location
  kind: 'GlobalDocumentDB'
  identity: {
    type: 'None'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    isVirtualNetworkFilterEnabled: false
    virtualNetworkRules: []
    disableKeyBasedMetadataWriteAccess: false
    enableFreeTier: true
    enableAnalyticalStorage: false
    analyticalStorageConfiguration: {
      schemaType: 'WellDefined'
    }
    databaseAccountOfferType: 'Standard'
    defaultIdentity: 'FirstPartyIdentity'
    networkAclBypass: 'None'
    disableLocalAuth: false
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
      maxIntervalInSeconds: 5
      maxStalenessPrefix: 100
    }
    locations:[]
    cors: []
    capabilities: []
    ipRules: []
    backupPolicy: {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: 240
        backupRetentionIntervalInHours: 8
        backupStorageRedundancy: 'Geo'
      }
    }
    networkAclBypassResourceIds: []
  }

  resource cosmos_sqldb_airdata 'sqlDatabases' = {
    name: 'airdata'
    properties: {
      resource: {
        id: 'airdata'
      }
    }

    resource cosmos_sqldb_default_settings 'throughputSettings' = {
      name: 'default'
      properties: {
        resource: {
          throughput: 400
        }
      }
    }

    resource cosmos_sqldb_container_airdata 'containers' = {
      name: 'airdataitems'
      properties: {
          resource: {
            id: 'airdataitems'
            indexingPolicy: {
              indexingMode: 'consistent'
              automatic: true
              includedPaths: [
                {
                  path: '/*'
                }
              ]
              excludedPaths: [
                {
                  path: '/"_etag"/?'
                }
              ]
            }
            partitionKey: {
              paths: [
                '/SensorName'
              ]
              kind: 'Hash'
            }
            uniqueKeyPolicy: {
              uniqueKeys: []
            }
            conflictResolutionPolicy: {
              mode: 'LastWriterWins'
              conflictResolutionPath: '/_ts'
            }
          }
        }
      

        resource cosmos_sqldb_container_airdata_sproc 'storedProcedures' = {
          name: 'GetAirQualityRecords'
          properties: {
            resource: {
              id: 'GetAirQualityRecords'
              body: 'function sample(prefix) {\n    var collection = getContext().getCollection();\n\n    // Query documents and take 1st item.\n    var isAccepted = collection.queryDocuments(\n        collection.getSelfLink(),\n        \'SELECT * FROM c\',\n    function (err, feed, options) {\n        if (err) throw err;\n\n        // Check the feed and if empty, set the body to \'no docs found\', \n        // else take 1st element from feed\n        if (!feed || !feed.length) {\n            var response = getContext().getResponse();\n            response.setBody(\'no docs found\');\n        }\n        else {\n            var response = getContext().getResponse();\n            response.setBody(JSON.stringify(feed));\n        }\n    });\n\n    if (!isAccepted) throw new Error(\'The query was not accepted by the server.\');\n}'
            }
          }
        }
      }

    resource cosmos_sqldb_container_alertcontroller 'containers' = {
      name: 'alertcontroller'
      properties: {
        resource: {
          id: 'alertcontroller'
          indexingPolicy: {
            indexingMode: 'consistent'
            automatic: true
            includedPaths: [
              {
                path: '/*'
              }
            ]
            excludedPaths: [
              {
                path: '/"_etag"/?'
              }
            ]
          }
          partitionKey: {
            paths: [
              '/id'
            ]
            kind: 'Hash'
          }
          defaultTtl: 1200
          uniqueKeyPolicy: {
            uniqueKeys: []
          }
          conflictResolutionPolicy: {
            mode: 'LastWriterWins'
            conflictResolutionPath: '/_ts'
          }
        }
      }
    }

    resource cosmos_sqldb_container_airdataleases 'containers' = {
      name: 'leases'
        properties: {
          resource: {
            id: 'leases'
            indexingPolicy: {
              indexingMode: 'consistent'
              automatic: true
              includedPaths: [
                {
                  path: '/*'
                }
              ]
              excludedPaths: [
                {
                  path: '/"_etag"/?'
                }
              ]
            }
            partitionKey: {
              paths: [
                '/id'
              ]
              kind: 'Hash'
            }
            uniqueKeyPolicy: {
              uniqueKeys: []
            }
            conflictResolutionPolicy: {
              mode: 'LastWriterWins'
              conflictResolutionPath: '/_ts'
            }
          }
        }
    }
  }
}

// Storage account and containers
resource storage_airdata 'Microsoft.Storage/storageAccounts@2021-08-01' = {
  name: storageaccount_airmonitor_name
  location: deploy_location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'

  resource storage_airdata_blobs 'blobServices' = {
    name: 'default'

    resource storage_airdata_processeddata 'containers' = {
      name: account_airanomdetect_container_data
      properties: {
        publicAccess: 'None'
      }
    }

    resource storage_airdata_data 'containers' = {
      name: 'data'
      properties: {
        publicAccess: 'None'
      }
    }

    resource storage_airdata_webjobs_secrets 'containers' = {
      name: 'azure-webjobs-secrets'
      properties: {
        publicAccess: 'None'
      }
    }

    resource storage_airdata_webjobs_eventhub 'containers' = {
      name: 'azure-webjobs-eventhub'
      properties: {
        publicAccess: 'None'
      }
    }

    resource storage_airdata_webjobs_hosts 'containers' = {
      name: 'azure-webjobs-hosts'
      properties: {
        publicAccess: 'None'
      }
    }

    resource storage_airdata_default_scm_releases 'containers' = {
      name: 'scm-releases'
      properties: {
        publicAccess: 'None'
      }
    }
  }
}

// Azure Function Consumption Plan
resource dynamic_serverfarm_function 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: hosting_plan_name
  location: deploy_location
  kind: 'linux'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: true
  }
}

// Azure Function
resource sites_swairmonitor_name_resource 'Microsoft.Web/sites@2021-03-01' = {
  name: sites_swairmonitor_name
  location: deploy_location
  kind: 'functionapp'
  properties: {
    serverFarmId: dynamic_serverfarm_function.id
    siteConfig: {
      linuxFxVersion: 'DOTNET|6.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageaccount_airmonitor_name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storage_airdata.listKeys().keys[0].value}'
        }
        {
          name: 'AIRDATA_COSMOS_COLLECTION'
          value: 'airdataitems'
        }
        {
          name: 'AIRDATA_COSMOS_DB'
          value: 'airdata'
        }
        {
          name: 'AIRDATA_COSMOS_PARTITION_KEY'
          value: 'PurpleAir-b70'
        }
        {
          name: 'AIRDATA_COSMOS_SPROC'
          value: 'GetAirQualityRecords'
        }
        {
          name: 'ALERT_FLAG_COLLECTION'
          value: 'alertcontroller'
        }
        {
          name: 'ALERT_FLAG_ITEM_ID'
          value: 'alertflag'
        }
        {
          name: 'ANOMALY_DETECTOR_ENDPOINT'
          value: 'https://${anomaly_detector_cog_services_account.name}.cognitiveservices.azure.com/'
        }
        {
          name: 'ANOMALY_DETECTOR_KEY'
          value: anomaly_detector_cog_services_account.listKeys().key1
        }
        {
          name: 'ANOMALY_DETECTOR_MODEL_ID'
          value: 'PLACEHOLDER'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: '952b15f3-c059-47f4-ad24-27f87095625c'
        }
        {
          name: 'COSMOS_DB_CONNECTION'
          value: 'AccountEndpoint=https://swairmonitor.documents.azure.com:443/;AccountKey=EwWVzu9L3qKw0EIFzx2DjFzqK5VPIWEuELt64FQ8OJk8Plp1sQnvQLCIEnzLR8P9hLo3k8YgFIAozO4pAo24QA==;'
        }
        {
          name: 'COSMOS_HOST'
          value: 'https://${cosmos_account_airmonitor.name}.documents.azure.com:443/'
        }
        {
          name: 'COSMOS_KEY'
          value: cosmos_account_airmonitor.listKeys().primaryMasterKey
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
        {
          name: 'IOT_HUB_CONNECTION'
          value: 'Endpoint=sb://{your_iothub_eventhub}.servicebus.windows.net/;SharedAccessKeyName=iothubowner;SharedAccessKey={your_key};EntityPath=iothub-ehub-{your_entity_paths}'
        }
        {
          name: 'MESSAGE_RECIPIENT'
          value: 'Test'
        }
        {
          name: 'MESSAGE_SENDER'
          value: 'Test'
        }
        {
          name: 'TWILIO_ACCOUNT_SID'
          value: 'Test'
        }
        {
          name: 'TWILIO_AUTH_TOKEN'
          value: 'Test'
        }
      ]
    }
    httpsOnly: true
  }
}

// Stream Analytics Job
resource streamingjobs_swairiottocsv_name_resource 'Microsoft.StreamAnalytics/streamingjobs@2021-10-01-preview' = {
  name: streamingjobs_swairiottocsv_name
  location: deploy_location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    sku: {
      name: 'Standard'
    }
    outputStartMode: 'LastOutputEventTime'
    outputStartTime: '2022-06-15T06:36:33.64Z'
    outputErrorPolicy: 'Stop'
    eventsLateArrivalMaxDelayInSeconds: 5
    dataLocale: 'en-US'
    compatibilityLevel: '1.0'
    contentStoragePolicy: 'SystemAccount'
    jobType: 'Cloud'
  }

  // Input (Event Hub endpoint from IoT Hub)
  resource streamingjobs_swairiottocsv_name_rawiotjsondata 'inputs' = {
    name: 'rawiotjsondata'
    properties: {
      type: 'Stream'
      datasource: {
        type: 'Microsoft.ServiceBus/EventHub'
        properties: {
          eventHubName: reference(iothubs_airdata.id, '2021-03-31').eventHubEndpoints.events.endpoint
          sharedAccessPolicyName: 'service'
          authenticationMode: 'ConnectionString'
        }
      }
      compression: {
        type: 'None'
      }
      serialization: {
        type: 'Json'
        properties: {
          encoding: 'UTF8'
        }
      }
    }
  }

  // Outputs (CSV files in Storage account)
  resource streamingjobs_swairiottocsv_name_humiditydatacsv 'outputs' = {
    name: 'humiditydatacsv'
    properties: {
      datasource: {
        type: 'Microsoft.Storage/Blob'
        properties: {
          blobPathPrefix: 'humidity'
          blobWriteMode: 'Append'
          storageAccounts: [
            {
              accountName: storageaccount_airmonitor_name
            }
          ]
          container: account_airanomdetect_container_data
          pathPattern: 'humidity'
          dateFormat: 'yyyy/MM/dd'
          timeFormat: 'HH'
          authenticationMode: 'Msi'
        }
      }
      timeWindow: '01:00:00'
      serialization: {
        type: 'Csv'
        properties: {
          fieldDelimiter: ','
          encoding: 'UTF8'
        }
      }
    }


  }

  resource streamingjobs_swairiottocsv_name_pm10datacsv 'outputs' = {
    name: 'pm10datacsv'
    properties: {
      datasource: {
        type: 'Microsoft.Storage/Blob'
        properties: {
          blobPathPrefix: 'pm10'
          blobWriteMode: 'Append'
          storageAccounts: [
            {
              accountName: storageaccount_airmonitor_name
            }
          ]
          container: account_airanomdetect_container_data
          pathPattern: 'pm10'
          dateFormat: 'yyyy/MM/dd'
          timeFormat: 'HH'
          authenticationMode: 'Msi'
        }
      }
      timeWindow: '01:00:00'
      serialization: {
        type: 'Csv'
        properties: {
          fieldDelimiter: ','
          encoding: 'UTF8'
        }
      }
    }
  }

  resource streamingjobs_swairiottocsv_name_pm25datacsv 'outputs' = {
    name: 'pm25datacsv'
    properties: {
      datasource: {
        type: 'Microsoft.Storage/Blob'
        properties: {
          blobPathPrefix: 'pm25'
          blobWriteMode: 'Append'
          storageAccounts: [
            {
              accountName: storageaccount_airmonitor_name
            }
          ]
          container: account_airanomdetect_container_data
          pathPattern: 'pm25'
          dateFormat: 'yyyy/MM/dd'
          timeFormat: 'HH'
          authenticationMode: 'Msi'
        }
      }
      timeWindow: '01:00:00'
      serialization: {
        type: 'Csv'
        properties: {
          fieldDelimiter: ','
          encoding: 'UTF8'
        }
      }
    }

  }

  resource streamingjobs_swairiottocsv_name_temperaturedatacsv 'outputs' = {
    name: 'temperaturedatacsv'
    properties: {
      datasource: {
        type: 'Microsoft.Storage/Blob'
        properties: {
          blobPathPrefix: 'temperature'
          blobWriteMode: 'Append'
          storageAccounts: [
            {
              accountName: storageaccount_airmonitor_name
            }
          ]
          container: account_airanomdetect_container_data
          pathPattern: 'temperature'
          dateFormat: 'yyyy/MM/dd'
          timeFormat: 'HH'
          authenticationMode: 'Msi'
        }
      }
      timeWindow: '01:00:00'
      serialization: {
        type: 'Csv'
        properties: {
          fieldDelimiter: ','
          encoding: 'UTF8'
        }
      }
    }
  }
}
