using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using LambdaDispatcher.Helpers;
using LambdaDispatcher.Models;
using System.Diagnostics;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaDispatcher;

public class Function(VariableEntornoHelper variableEntorno, ParameterStoreHelper parameterStore, DynamoHelper dynamoHelper, IAmazonSQS sqsClient)
{
    public async Task FunctionHandler(DispatcherInput input, ILambdaContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - " +
            $"Se inicia dispatcher de procesos.");

        string nombreAplicacion = variableEntorno.Obtener("APP_NAME");
        string nombreTablaProcesos = await parameterStore.ObtenerParametro($"/{nombreAplicacion}/DynamoDB/NombreTablaProcesos");
        string sqsQueueUrl = await parameterStore.ObtenerParametro($"/{nombreAplicacion}/SQS/QueueUrl");

        List<Document> procesos = await dynamoHelper.ObtenerPorIndice(nombreTablaProcesos, "PorIdCalendarizacion", "IdCalendarizacion", input.IdCalendarizacion);
        
        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se tiene {procesos.Count} procesos por gatillar.");

        foreach (Document proceso in procesos) {
            try {
                if (!proceso["Habilitado"].AsBoolean()) {
                    LambdaLogger.Log(
                        $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                        $"No se despacha el proceso ID {proceso["IdProceso"].AsString()} dado que no está habilitado.");
                    continue;
                }

                SendMessageRequest request = new() {
                    QueueUrl = sqsQueueUrl,
                    MessageBody = proceso.ToJson()
                };

                SendMessageResponse response = await sqsClient.SendMessageAsync(request);

                LambdaLogger.Log(
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Se despacha exitosamente el proceso ID {proceso["IdProceso"].AsString()}.");

            } catch(Exception ex) {

                LambdaLogger.Log(
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Ocurrio un error al despachar proceso - ID: {proceso["IdProceso"]}. " +
                    $"{ex}");

            }
        }
    }
}
