# CosmosDBDemo

Example run through of querying an Azure Cosmos DB database through a .NET Core application. 

Full tutorial covered at [Build a .NET console app to manage data in Azure Cosmos DB SQL API account](https://docs.microsoft.com/en-us/azure/cosmos-db/sql-api-get-started)

Setting up Configuration file:

* Create a `appsettings.json` file in your solution directory.
* Add the following:
```
"CosmosDb": {
  "EnpointUri": "<COSMOS DB URI>",
  "PrimaryKey": "<PRIMARY KEY>"
}
  ```