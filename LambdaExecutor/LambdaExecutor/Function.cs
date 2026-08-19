using Amazon.DynamoDBv2;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.Lambda.SQSEvents;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using LibreriaCompartida.Entities;
using LibreriaCompartida.Enums;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Interfaces.Helpers;
using LibreriaCompartida.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using static Amazon.Lambda.SQSEvents.SQSBatchResponse;
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
            services.AddSingleton<IAmazonSecurityTokenService, AmazonSecurityTokenServiceClient>();
			services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
			#endregion

			#region Singleton Helpers
			services.AddSingleton<IVariableEntornoHelper, VariableEntornoHelper>();
			#endregion

			#region Singleton Daos
			services.AddSingleton<EjecucionDao>();
			services.AddSingleton<ProcesoDao>();
			#endregion
		});

        var app = builder.Build();

        serviceProvider = app.Services;
    }

    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        List<BatchItemFailure> listaMensajesError = [];

        Stopwatch stopwatch = Stopwatch.StartNew();

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - " +
            $"Se inicia executor de procesos.");

		IVariableEntornoHelper variableEntorno = serviceProvider.GetRequiredService<IVariableEntornoHelper>();
        IAmazonSecurityTokenService securityTokenClient = serviceProvider.GetRequiredService<IAmazonSecurityTokenService>();
		EjecucionDao ejecucionDao = serviceProvider.GetRequiredService<EjecucionDao>();
		ProcesoDao procesoDao = serviceProvider.GetRequiredService<ProcesoDao>();

		LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se obtendran los parametros necesarios para procesar los mensajes.");

        string nombreAplicacion = variableEntorno.Obtener("APP_NAME");

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se tiene {evnt.Records.Count} mensajes que procesar.");

        foreach (SQSMessage mensaje in evnt.Records) {
			string idEjecucion = mensaje.Body;

			try {
                LambdaLogger.Log(
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Se procedera a procesar el mensaje de la cola - Message ID: {mensaje.MessageId} - ID Ejecución: {idEjecucion}.");

                Ejecucion ejecucion = await ejecucionDao.Obtener(idEjecucion) ?? throw new InvalidOperationException("No se encuentra la metadata de la ejecución");
                Proceso proceso = await procesoDao.Obtener(ejecucion.IdProceso) ?? throw new InvalidOperationException("No se encuentra la metadata del proceso");

                // Se asume el rol para ejecutar el proceso...
                AssumeRoleRequest requestAssumeRole = new() {
                    RoleSessionName = $"{nombreAplicacion}-Execute-Session",
                    RoleArn = proceso.ArnRol
				};
                AssumeRoleResponse responseAssumeRole = await securityTokenClient.AssumeRoleAsync(requestAssumeRole);

                // Se manda a procesar el mensaje...
                if (proceso.ArnProceso.StartsWith("arn:aws:lambda:")) {
                    AmazonLambdaClient lambdaClient = new(
                        responseAssumeRole.Credentials.AccessKeyId,
                        responseAssumeRole.Credentials.SecretAccessKey,
                        responseAssumeRole.Credentials.SessionToken
                    );

                    InvokeRequest request = new() {
                        FunctionName = proceso.ArnProceso,
                        InvocationType = InvocationType.Event,
                        Payload = proceso.Parametros
					};
                    await lambdaClient.InvokeAsync(request);

                    LambdaLogger.Log(
                        $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                        $"Se procesa exitosamente la llamada a la funcion lambda ARN {proceso.ArnProceso} - Message ID: {mensaje.MessageId} - ID Ejecución: {ejecucion.IdEjecucion}.");
                } else if (proceso.ArnProceso.StartsWith("arn:aws:states:")) {    
                    AmazonStepFunctionsClient stepFunctionClient = new(
                        responseAssumeRole.Credentials.AccessKeyId,
                        responseAssumeRole.Credentials.SecretAccessKey,
                        responseAssumeRole.Credentials.SessionToken
                    );

                    StartExecutionRequest request = new() {
                        StateMachineArn = proceso.ArnProceso,
                        Input = proceso.Parametros
					};
                    await stepFunctionClient.StartExecutionAsync(request);

                    LambdaLogger.Log(
                        $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                        $"Se procesa exitosamente la llamada a la step function ARN {proceso.ArnProceso} - Message ID: {mensaje.MessageId} - ID Ejecución: {ejecucion.IdEjecucion}.");
                } else if (proceso.ArnProceso.StartsWith("arn:aws:sns:")) {
                    AmazonSimpleNotificationServiceClient snsClient = new(
                        responseAssumeRole.Credentials.AccessKeyId,
                        responseAssumeRole.Credentials.SecretAccessKey,
                        responseAssumeRole.Credentials.SessionToken
                    );

                    PublishRequest request = new() {
                        TopicArn = proceso.ArnProceso,
                        Message = proceso.Parametros
					};
                    await snsClient.PublishAsync(request);

                    LambdaLogger.Log(
                        $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                        $"Se procesa exitosamente la llamada a SNS ARN {proceso.ArnProceso} - Message ID: {mensaje.MessageId} - ID Ejecución: {ejecucion.IdEjecucion}.");
                } else if (proceso.ArnProceso.StartsWith("arn:aws:sqs:")) {
                    AmazonSQSClient sqsAssumeClient = new(
                        responseAssumeRole.Credentials.AccessKeyId,
                        responseAssumeRole.Credentials.SecretAccessKey,
                        responseAssumeRole.Credentials.SessionToken
                    );

                    string[] arnParts = proceso.ArnProceso.Split(':');

                    SendMessageRequest request = new() {
                        QueueUrl = $"https://sqs.{arnParts[3]}.amazonaws.com/{arnParts[4]}/{arnParts[5]}",
                        MessageBody = proceso.Parametros
					};
                    await sqsAssumeClient.SendMessageAsync(request);

                    LambdaLogger.Log(
                        $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                        $"Se procesa exitosamente la llamada a SQS ARN {proceso.ArnProceso} - Message ID: {mensaje.MessageId} - ID Ejecución: {ejecucion.IdEjecucion}.");
                } else {
                    throw new NotSupportedException($"{nombreAplicacion} no soporta el ARN ingresado: {proceso.ArnProceso}");
                }

                await ejecucionDao.RegistrarFechaEjecucion(idEjecucion, DateTime.UtcNow, EstadoEjecucion.EjecutadoOk, null);
			} catch(Exception ex) {
                LambdaLogger.Log(LogLevel.Error,
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Ocurrio un error al procesar mensaje - Message ID: {mensaje.MessageId}. " +
                    $"{ex}");

                listaMensajesError.Add(new BatchItemFailure {
                    ItemIdentifier = mensaje.MessageId,
                });

                await ejecucionDao.RegistrarFechaEjecucion(idEjecucion, DateTime.UtcNow, EstadoEjecucion.ErrorAlEjecutar, $"Error al ejecutar proceso - {ex}");
            }
        }

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Termino exitosamente el executor de procesos - Casos con error: {listaMensajesError.Count}.");

        return new SQSBatchResponse {
            BatchItemFailures = listaMensajesError
        };
    }
}
