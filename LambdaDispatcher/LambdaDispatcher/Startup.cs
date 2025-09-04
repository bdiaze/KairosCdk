using Amazon.DynamoDBv2;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;
using LambdaDispatcher.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LambdaDispatcher {
    public class Startup {
        public void ConfigureServices(IServiceCollection services) {
            #region Singleton AWS Services
            services.AddSingleton<IAmazonSimpleSystemsManagement, AmazonSimpleSystemsManagementClient>();
            services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
            services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
            #endregion

            #region Singleton Helpers
            services.AddSingleton<VariableEntornoHelper>();
            services.AddSingleton<ParameterStoreHelper>();
            services.AddSingleton<DynamoHelper>();
            #endregion
        }
    }
}
