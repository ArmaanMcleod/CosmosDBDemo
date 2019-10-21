using System;
using System.IO;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Cosmos;
using System.Linq.Expressions;

using CosmosDBDemo.Models;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.FileExtensions;
using Microsoft.Extensions.Configuration.Json;

namespace CosmosDBDemo
{
    public class Program
    {
        private CosmosClient cosmosClient;
        private Database database;
        private Container container;

        private const string DatabaseId = "FamilyDatabase";
        private const string ContainerId = "FamilyContainer";

        private static SettingsConfig settings;

        public static async Task Main(string[] args)
        {
            // Add configuration file
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            var configuration = builder.GetSection("CosmosDb");

            // Create settings object to create Cosmos db client later on
            settings = new SettingsConfig
            {
                EndpointUri = configuration.GetSection("EnpointUri").Value,
                PrimaryKey = configuration.GetSection("PrimaryKey").Value
            };

            try
            {
                Console.WriteLine("Beginning operations...\n");
                var program = new Program();
                await program.GetStartedDemoAsync();

            }
            catch (CosmosException de)
            {
                var baseException = de.GetBaseException();
                Console.WriteLine($"{de.StatusCode} error occurred: {de}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e}");
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Entry point to call methods that operate on Azure Cosmos DB resources in this sample
        /// </summary>
        /// <returns></returns>
        private async Task GetStartedDemoAsync()
        {
            // Create a new instance of the Cosmos Client
            cosmosClient = new CosmosClient(settings.EndpointUri, settings.PrimaryKey);

            await CreateDatabaseAsync();
            await CreateContainerAsync();
            await AddItemsToContainerAsync();
            await QueryItemsAsync();
            await ReplaceFamilyItemAsync();
            await DeleteFamilyItemAsync();
            await DeleteDatabaseAndCleanupAsync();
        }

        /// <summary>
        /// Create the database if it does not exist
        /// </summary>
        private async Task CreateDatabaseAsync()
        {
            // Create a new database
            database = await cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
            Console.WriteLine($"Created Database: {database.Id}\n");
        }

        /// <summary>
        /// Create the container if it does not exist. 
        /// Specifiy "/LastName" as the partition key since we're storing family information, to ensure good distribution of requests and storage.
        /// </summary>
        /// <returns></returns>
        private async Task CreateContainerAsync()
        {
            // Create a new container
            container = await database.CreateContainerIfNotExistsAsync(ContainerId, "/LastName");
            Console.WriteLine($"Created Container: {container.Id}\n");
        }

        /// <summary>
        /// Add Family items to the container
        /// </summary>
        private async Task AddItemsToContainerAsync()
        {
            // Create a family object for the Andersen family
            var andersenFamily = new Family
            {
                Id = "Andersen.1",
                LastName = "Andersen",
                Parents = new Parent[]
                {
                    new Parent { FirstName = "Thomas" },
                    new Parent { FirstName = "Mary Kay" }
                },
                Children = new Child[]
                {
                    new Child
                    {
                        FirstName = "Henriette Thaulow",
                        Gender = "female",
                        Grade = 5,
                        Pets = new Pet[]
                        {
                            new Pet { GivenName = "Fluffy" }
                        }
                    }       
                },
                Address = new Address { State = "WA", County = "King", City = "Seattle" },
                IsRegistered = false
            };

            try
            {
                // Read the item to see if it exists.  
                var andersenFamilyResponse = await container.ReadItemAsync<Family>(andersenFamily.Id, new PartitionKey(andersenFamily.LastName));
                Console.WriteLine($"Item in database with id: {andersenFamilyResponse.Resource.Id} already exists\n");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                var andersenFamilyResponse = await container.CreateItemAsync(andersenFamily, new PartitionKey(andersenFamily.LastName));

                    // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. 
                    // We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                    Console.WriteLine($"Created item in database with id: ${andersenFamilyResponse.Resource.Id} Operation consumed {andersenFamilyResponse.RequestCharge} RUs.\n");
            }

            // Create a family object for the Wakefield family
            var wakefieldFamily = new Family
            {
                Id = "Wakefield.7",
                LastName = "Wakefield",
                Parents = new Parent[]
                {
                    new Parent { FamilyName = "Wakefield", FirstName = "Robin" },
                    new Parent { FamilyName = "Miller", FirstName = "Ben" }
                },
                Children = new Child[]
                {
                    new Child
                    {
                        FamilyName = "Merriam",
                        FirstName = "Jesse",
                        Gender = "female",
                        Grade = 8,
                        Pets = new Pet[]
                        {
                            new Pet { GivenName = "Goofy" },
                            new Pet { GivenName = "Shadow" }
                        }
                    },
                    new Child
                    {
                        FamilyName = "Miller",
                        FirstName = "Lisa",
                        Gender = "female",
                        Grade = 1
                    }
                },
                Address = new Address { State = "NY", County = "Manhattan", City = "NY" },
                IsRegistered = true
            };

            try
            {
                // Read the item to see if it exists
                var wakefieldFamilyResponse = await container.ReadItemAsync<Family>(wakefieldFamily.Id, new PartitionKey(wakefieldFamily.LastName));
                Console.WriteLine($"Item in database with id: {wakefieldFamilyResponse.Resource.Id} already exists\n");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Create an item in the container representing the Wakefield family. Note we provide the value of the partition key for this item, which is "Wakefield"
                var wakefieldFamilyResponse = await container.CreateItemAsync(wakefieldFamily, new PartitionKey(wakefieldFamily.LastName));

                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. 
                // We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                Console.WriteLine($"Created item in database with id: {wakefieldFamilyResponse.Resource.Id} Operation consumed {wakefieldFamilyResponse.RequestCharge} RUs.\n");
            }
        }

        /// <summary>
        /// Run a query (using Azure Cosmos DB SQL syntax) against the container
        /// </summary>
        private async Task QueryItemsAsync()
        {
            var sqlQueryText = "SELECT * FROM c WHERE c.LastName = 'Andersen'";

            Console.WriteLine($"Running Query: {sqlQueryText}\n");

            var queryDefinition = new QueryDefinition(sqlQueryText);
            var queryResultSetIterator = container.GetItemQueryIterator<Family>(queryDefinition);

            var families = new List<Family>();

            while (queryResultSetIterator.HasMoreResults)
            {
                var currentResultSet = await queryResultSetIterator.ReadNextAsync();
                families.AddRange(currentResultSet);
            }
        }

        /// <summary>
        /// Replace an item in the container
        /// </summary>
        private async Task ReplaceFamilyItemAsync()
        {
            ItemResponse<Family> wakefieldFamilyResponse = await container.ReadItemAsync<Family>("Wakefield.7", new PartitionKey("Wakefield"));
            var itemBody = wakefieldFamilyResponse.Resource;

            // update registration status from false to true
            itemBody.IsRegistered = true;
            // update grade of child
            itemBody.Children[0].Grade = 6;

            // replace the item with the updated content
            wakefieldFamilyResponse = await container.ReplaceItemAsync(itemBody, itemBody.Id, new PartitionKey(itemBody.LastName));
            Console.WriteLine($"Updated Family [{itemBody.LastName},{itemBody.Id}].\n \tBody is now: {wakefieldFamilyResponse.Resource}\n");
        }

        /// <summary>
        /// Delete an item in the container
        /// </summary>
        private async Task DeleteFamilyItemAsync()
        {
            var partitionKeyValue = "Wakefield";
            var familyId = "Wakefield.7";

            // Delete an item. Note we must provide the partition key value and id of the item to delete
            await container.DeleteItemAsync<Family>(familyId, new PartitionKey(partitionKeyValue));
            Console.WriteLine($"Deleted Family [{partitionKeyValue},{familyId}]\n");
        }

        /// <summary>
        /// Delete the database and dispose of the Cosmos Client instance
        /// </summary>
        private async Task DeleteDatabaseAndCleanupAsync()
        {
            await database.DeleteAsync();
            // Also valid: await this.cosmosClient.Databases["FamilyDatabase"].DeleteAsync();

            Console.WriteLine($"Deleted Database: {DatabaseId}\n");

            //Dispose of CosmosClient
            cosmosClient.Dispose();
        }
    }
}
