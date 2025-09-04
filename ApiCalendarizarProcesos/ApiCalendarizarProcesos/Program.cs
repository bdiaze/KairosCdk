using Amazon.DynamoDBv2;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Scheduler;
using Amazon.SimpleSystemsManagement;
using ApiCalendarizarProcesos.Endpoints;
using ApiCalendarizarProcesos.Helpers;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi, new SourceGeneratorLambdaJsonSerializer<AppJsonSerializerContext>());

#region Singleton AWS Services
builder.Services.AddSingleton<IAmazonSimpleSystemsManagement, AmazonSimpleSystemsManagementClient>();
builder.Services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
builder.Services.AddSingleton<IAmazonScheduler, AmazonSchedulerClient>();
#endregion

#region Singleton Helpers
builder.Services.AddSingleton<VariableEntornoHelper>();
builder.Services.AddSingleton<ParameterStoreHelper>();
builder.Services.AddSingleton<SchedulerHelper>();
builder.Services.AddSingleton<DynamoHelper>();
#endregion

var app = builder.Build();

app.MapProcesosEndpoints();

app.Run();
