using Amazon.DynamoDBv2;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Scheduler;
using ApiCalendarizarProcesos.Endpoints;
using ApiCalendarizarProcesos.Helpers;
using ApiCalendarizarProcesos.Interfaces.Helpers;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Interfaces.Helpers;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi, new SourceGeneratorLambdaJsonSerializer<AppJsonSerializerContext>());

#region Singleton AWS Services
builder.Services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
builder.Services.AddSingleton<IAmazonScheduler, AmazonSchedulerClient>();
#endregion

#region Singleton Helpers
builder.Services.AddSingleton<IVariableEntornoHelper, VariableEntornoHelper>();
builder.Services.AddSingleton<ISchedulerHelper, SchedulerHelper>();
builder.Services.AddSingleton<IDynamoHelper, DynamoHelper>();
#endregion

var app = builder.Build();

app.MapProcesosEndpoints();

await app.RunAsync();
