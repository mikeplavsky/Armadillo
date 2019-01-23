﻿using System;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Armadillo.Siebel;
using Microsoft.Extensions.Logging;

namespace Armadillo.Agent
{
    public class Application
    {
        ILoggerFactory loggerFactory_;
        ILogger logger_; 
        
        public Application(ILoggerFactory loggerFactory)
        {
            loggerFactory_ = loggerFactory;
            logger_ = loggerFactory_.CreateLogger("Application");
        }

        public void Run()
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddUserSecrets("28e6f711-a4c4-4cef-9e37-50ebfee35f91");
                var configuration = builder.Build();

                var dataProviderName = configuration["SubcaseDataProvider"];
                logger_.LogInformation($"Data provider: {dataProviderName}");

                ISubcaseDataProdiver dataProvider = null;
                if(String.Equals("Random", dataProviderName, StringComparison.OrdinalIgnoreCase))
                {
                    logger_.LogInformation("Using random data provider");
                    dataProvider = new RandomDataProvider();
                }
                else if(String.Equals("Report", dataProviderName, StringComparison.OrdinalIgnoreCase))
                {
                    logger_.LogInformation("Using SSRS report data provider");
                    dataProvider = new ReportServerDataProvider(loggerFactory_.CreateLogger("ReportServerDataProvider"));
                }
                else
                {
                    throw new ApplicationException($"Unsupported data provider: {dataProviderName}");
                }

                var endpointUri = configuration["CosmosDB:EndpointUri"];
                var primaryKey = configuration["CosmosDB:PrimaryKey"];

                logger_.LogInformation("Database endpoint {0}", endpointUri);
                var documentClient = new DocumentClient(new Uri(endpointUri), primaryKey);

                var uploader = new Uploader(dataProvider, documentClient, loggerFactory_.CreateLogger("Uploader"));
                uploader.UpdateAsync().Wait();

                logger_.LogInformation("Update completed");

                // await TestReadSubcasesAsync();
            }
            catch (Exception ex)
            {
                logger_.LogError(ex.ToString());
            }
        }

        // For test/dev purposes only - read and print uploaded data
        private async Task TestReadSubcasesAsync(DocumentClient documentClient)
        {
            var cosmosDataProvider = new CosmosDataProvider(documentClient, loggerFactory_.CreateLogger("CosmosDataProvider"));
            var products = cosmosDataProvider.GetProducts();
            foreach(var product in products)
            {
                logger_.LogInformation($"Read product {product}");

                var subcases = await cosmosDataProvider.GetSubcasesAsync(product);
                foreach (var subcase in subcases)
                {
                    logger_.LogInformation($"Subcase {subcase.Id} {subcase.Title}");
                }
            }            
        }
    }
}
