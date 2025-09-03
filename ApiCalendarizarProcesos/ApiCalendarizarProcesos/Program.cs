using Amazon.Lambda.Serialization.SystemTextJson;
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
#endregion

#region Singleton Helpers
builder.Services.AddSingleton<VariableEntornoHelper>();
builder.Services.AddSingleton<ParameterStoreHelper>();
#endregion

var app = builder.Build();

app.MapTipoMensajesEndpoints();

app.Run();
