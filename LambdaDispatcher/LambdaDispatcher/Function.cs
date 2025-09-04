using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;
using Amazon.SQS.Model;
using LambdaDispatcher.Helpers;
using LambdaDispatcher.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaDispatcher;

public class Function
{
    private readonly IServiceProvider serviceProvider;

    public Function() {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices((context, services) => {
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
        });

        var app = builder.Build();

        serviceProvider = app.Services;
    }

    public async Task FunctionHandler(DispatcherInput input, ILambdaContext context) {
        Stopwatch stopwatch = Stopwatch.StartNew();

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - " +
            $"Se inicia dispatcher de procesos.");
        
        VariableEntornoHelper variableEntorno = serviceProvider.GetRequiredService<VariableEntornoHelper>();
        ParameterStoreHelper parameterStore = serviceProvider.GetRequiredService<ParameterStoreHelper>();
        DynamoHelper dynamoHelper = serviceProvider.GetRequiredService<DynamoHelper>();
        IAmazonSQS sqsClient = serviceProvider.GetRequiredService<IAmazonSQS>();

        string nombreAplicacion = variableEntorno.Obtener("APP_NAME");
        string nombreTablaProcesos = await parameterStore.ObtenerParametro($"/{nombreAplicacion}/DynamoDB/NombreTablaProcesos");
        string sqsQueueUrl = await parameterStore.ObtenerParametro($"/{nombreAplicacion}/SQS/QueueUrl");

        List<Dictionary<string, object?>> procesos = await dynamoHelper.ObtenerPorIndice(nombreTablaProcesos, "PorIdCalendarizacion", "IdCalendarizacion", input.IdCalendarizacion);
        
        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se tiene {procesos.Count} procesos por gatillar.");

        foreach (Dictionary<string, object?> proceso in procesos) {
            try {
                if (!proceso.ContainsKey("Habilitado") || proceso["Habilitado"] == null || !(bool)proceso["Habilitado"]!) {
                    LambdaLogger.Log(
                        $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                        $"No se despacha el proceso ID {proceso["IdProceso"]} dado que no está habilitado.");
                    continue;
                }

                SendMessageRequest request = new() {
                    QueueUrl = sqsQueueUrl,
                    MessageBody = JsonSerializer.Serialize(proceso)
                };

                SendMessageResponse response = await sqsClient.SendMessageAsync(request);

                LambdaLogger.Log(
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Se despacha exitosamente el proceso ID {proceso["IdProceso"]}.");

            } catch(Exception ex) {

                LambdaLogger.Log(
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Ocurrio un error al despachar proceso - ID: {proceso["IdProceso"]}. " +
                    $"{ex}");

            }
        }
    }
}
