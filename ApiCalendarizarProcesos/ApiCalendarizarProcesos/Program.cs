using Amazon.DynamoDBv2;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Scheduler;
using ApiCalendarizarProcesos.Business;
using ApiCalendarizarProcesos.Endpoints;
using ApiCalendarizarProcesos.Helpers;
using ApiCalendarizarProcesos.Interfaces.Helpers;
using ApiCalendarizarProcesos.UseCases;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Interfaces.Helpers;
using LibreriaCompartida.Repositories;
using Scalar.AspNetCore;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi, new SourceGeneratorLambdaJsonSerializer<AppJsonSerializerContext>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(c => {
	c.AddDocumentTransformer((document, context, cancellationToken) => {
		document.Info = new() {
			Title = "API Kairos - Minimal API AoT",
			Version = "v1"
		};

		return Task.CompletedTask;
	});
});

#region Singleton AWS Services
builder.Services.AddSingleton<IAmazonDynamoDB>(sp => {
	AmazonDynamoDBConfig config = new() {
		ConnectTimeout = TimeSpan.FromSeconds(5),
		Timeout = TimeSpan.FromSeconds(25)
	};
	return new AmazonDynamoDBClient(config);
});
builder.Services.AddSingleton<IAmazonScheduler>(sp => {
	AmazonSchedulerConfig config = new() {
		ConnectTimeout = TimeSpan.FromSeconds(5),
		Timeout = TimeSpan.FromSeconds(25)
	};

	return new AmazonSchedulerClient(config);
});
#endregion

#region Singleton Helpers
builder.Services.AddSingleton<IVariableEntornoHelper, VariableEntornoHelper>();
builder.Services.AddSingleton<ISchedulerHelper, SchedulerHelper>();
#endregion

#region Singleton Daos
builder.Services.AddSingleton<ProcesoDao>();
builder.Services.AddSingleton<CalendarizacionDao>();
builder.Services.AddSingleton<EjecucionDao>();
builder.Services.AddSingleton<RelacCalendProcDao>();
builder.Services.AddSingleton<RelacProcEjecDao>();
#endregion

#region Singleton Business
builder.Services.AddSingleton<CalendarizacionBusiness>();
builder.Services.AddSingleton<ProcesoBusiness>();
#endregion

#region Singleton Use Cases
builder.Services.AddSingleton<ProcesoUseCase>();
#endregion

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment()) {
	app.MapOpenApi();
	app.MapScalarApiReference();
}

app.MapProcesosEndpoints();

await app.RunAsync();
