using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.Lambda.SQSEvents;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using LambdaExecutor.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;
using static Amazon.Lambda.SQSEvents.SQSEvent;
using LogLevel = Amazon.Lambda.Core.LogLevel;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaExecutor;

public class Function
{
    private readonly IServiceProvider serviceProvider;

    public Function() {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices((context, services) => {
            #region Singleton AWS Services
            services.AddSingleton<IAmazonSimpleSystemsManagement, AmazonSimpleSystemsManagementClient>();
            services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
            services.AddSingleton<IAmazonSecurityTokenService, AmazonSecurityTokenServiceClient>();
            #endregion

            #region Singleton Helpers
            services.AddSingleton<VariableEntornoHelper>();
            services.AddSingleton<ParameterStoreHelper>();
            #endregion
        });

        var app = builder.Build();

        serviceProvider = app.Services;
    }

    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - " +
            $"Se inicia executor de procesos.");

        VariableEntornoHelper variableEntorno = serviceProvider.GetRequiredService<VariableEntornoHelper>();
        ParameterStoreHelper parameterStore = serviceProvider.GetRequiredService<ParameterStoreHelper>();
        IAmazonSQS sqsClient = serviceProvider.GetRequiredService<IAmazonSQS>();
        IAmazonSecurityTokenService securityTokenClient = serviceProvider.GetRequiredService<IAmazonSecurityTokenService>();

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se obtendran los parametros necesarios para procesar los mensajes.");

        string nombreAplicacion = variableEntorno.Obtener("APP_NAME");
        string sqsQueueUrl = await parameterStore.ObtenerParametro($"/{nombreAplicacion}/SQS/QueueUrl");

        foreach (SQSMessage mensaje in evnt.Records) {
            try { 
                Dictionary<string, object?> proceso = JsonConvert.DeserializeObject<Dictionary<string, object?>>(mensaje.Body)!;

                LambdaLogger.Log(
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Se procedera a procesar el mensaje de la cola - Message ID: {mensaje.MessageId}");

                if (proceso.TryGetValue("ArnRol", out object? arnRole) && proceso.TryGetValue("ArnProceso", out object? arnProceso) && proceso.TryGetValue("Parametros", out object? parametros)) {
                    // Si no viene el ARN del role o del proceso a gatillar, se omite la ejecución...
                    if (arnRole == null || arnProceso == null) {
                        LambdaLogger.Log(LogLevel.Warning,
                            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                            $"Se omite procesar el mensaje dado que no incluye ARN del proceso o ARN del role - Message ID: {mensaje.MessageId}.");

                        continue;
                    }

                    // Se asume el rol para ejecutar el proceso...
                    AssumeRoleRequest requestAssumeRole = new() {
                        RoleSessionName = $"{nombreAplicacion}-Execute-Session",
                        RoleArn = (string)arnRole
                    };
                    AssumeRoleResponse responseAssumeRole = await securityTokenClient.AssumeRoleAsync(requestAssumeRole);

                    // Se manda a procesar el mensaje...
                    if (((string)arnProceso).StartsWith("arn:aws:lambda:")) {
                        AmazonLambdaClient lambdaClient = new(
                            responseAssumeRole.Credentials.AccessKeyId,
                            responseAssumeRole.Credentials.SecretAccessKey,
                            responseAssumeRole.Credentials.SessionToken
                        );

                        InvokeRequest request = new() {
                            FunctionName = (string)arnProceso,
                            InvocationType = InvocationType.Event,
                        };
                        if (parametros != null) request.Payload = (string)parametros;

                        await lambdaClient.InvokeAsync(request);

                        LambdaLogger.Log(
                            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                            $"Se procesa exitosamente la llamada a la funcion lambda ARN {(string)arnProceso} - Message ID: {mensaje.MessageId}");
                    } else if (((string)arnProceso).StartsWith("arn:aws:states:")) {    
                        AmazonStepFunctionsClient stepFunctionClient = new(
                            responseAssumeRole.Credentials.AccessKeyId,
                            responseAssumeRole.Credentials.SecretAccessKey,
                            responseAssumeRole.Credentials.SessionToken
                        );

                        StartExecutionRequest request = new() {
                            StateMachineArn = (string)arnProceso,
                        };
                        if (parametros != null) request.Input = (string)parametros;

                        await stepFunctionClient.StartExecutionAsync(request);

                        LambdaLogger.Log(
                            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                            $"Se procesa exitosamente la llamada a la step function ARN {(string)arnProceso} - Message ID: {mensaje.MessageId}");
                    } else if (((string)arnProceso).StartsWith("arn:aws:sns:")) {
                        AmazonSimpleNotificationServiceClient snsClient = new(
                            responseAssumeRole.Credentials.AccessKeyId,
                            responseAssumeRole.Credentials.SecretAccessKey,
                            responseAssumeRole.Credentials.SessionToken
                        );

                        PublishRequest request = new() {
                            TopicArn = (string)arnProceso
                        };
                        if (parametros != null) request.Message = (string)parametros;

                        await snsClient.PublishAsync(request);

                        LambdaLogger.Log(
                            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                            $"Se procesa exitosamente la llamada a SNS ARN {(string)arnProceso} - Message ID: {mensaje.MessageId}");
                    } else if (((string)arnProceso).StartsWith("arn:aws:sqs:")) {
                        AmazonSQSClient sqsAssumeClient = new(
                            responseAssumeRole.Credentials.AccessKeyId,
                            responseAssumeRole.Credentials.SecretAccessKey,
                            responseAssumeRole.Credentials.SessionToken
                        );

                        string[] arnParts = ((string)arnProceso).Split(':');

                        SendMessageRequest request = new() {
                            QueueUrl = $"https://sqs.{arnParts[3]}.amazonaws.com/{arnParts[4]}/{arnParts[5]}"
                        };
                        if (parametros != null) request.MessageBody = (string)parametros;

                        await sqsAssumeClient.SendMessageAsync(request);

                        LambdaLogger.Log(
                            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                            $"Se procesa exitosamente la llamada a SQS ARN {(string)arnProceso} - Message ID: {mensaje.MessageId}");
                    } else {
                        throw new NotSupportedException($"{nombreAplicacion} no soporta el ARN ingresado: {(string)arnProceso}");
                    }

                    // Se elimina mensaje de la cola...
                    DeleteMessageResponse deleteResponse = await sqsClient.DeleteMessageAsync(sqsQueueUrl, mensaje.ReceiptHandle);
                    if (deleteResponse.HttpStatusCode != HttpStatusCode.OK) {
                        throw new Exception($"Error al quitar mensaje de la cola [DeleteMessageResponse - Message ID: {mensaje.MessageId} - HttpStatusCode: {deleteResponse.HttpStatusCode}]");
                    }
                }
            } catch(Exception ex) {
                LambdaLogger.Log(LogLevel.Error,
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Ocurrio un error al procesar mensaje - Message ID: {mensaje.MessageId}. " +
                    $"{ex}");
            }
        }

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Termino exitosamente el executor de procesos.");
    }
}
