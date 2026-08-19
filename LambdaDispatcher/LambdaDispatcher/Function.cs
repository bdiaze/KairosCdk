using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using LambdaDispatcher.Models;
using LibreriaCompartida.Entities;
using LibreriaCompartida.Enums;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Interfaces.Helpers;
using LibreriaCompartida.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

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
            services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
            services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
            #endregion

            #region Singleton Helpers
            services.AddSingleton<IVariableEntornoHelper, VariableEntornoHelper>();
			#endregion

			#region Singleton Daos
			services.AddSingleton<RelacCalendProcDao>();
			services.AddSingleton<EjecucionDao>();
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

		IAmazonSQS sqsClient = serviceProvider.GetRequiredService<IAmazonSQS>();
		IVariableEntornoHelper variableEntorno = serviceProvider.GetRequiredService<IVariableEntornoHelper>();
		RelacCalendProcDao relacCalendProcDao = serviceProvider.GetRequiredService<RelacCalendProcDao>();
		EjecucionDao ejecucionDao = serviceProvider.GetRequiredService<EjecucionDao>();

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se obtendran los parametros necesarios para despachar los procesos.");

		string sqsQueueUrl = variableEntorno.Obtener("SQS_QUEUE_URL");

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se consultaran los procesos que necesitan ser despachados.");

        List<RelacCalendProc> relaciones = await relacCalendProcDao.ObtenerPorCalendarizacion(input.IdCalendarizacion);

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se tiene {relaciones.Count} procesos para despachar.");

        foreach (RelacCalendProc relacion in relaciones) {
            Ejecucion? ejecucionCreada = null;

			try {
				ejecucionCreada = await ejecucionDao.Crear(
					Guid.NewGuid().ToString(),
                    relacion.IdProceso,
                    DateTime.UtcNow,
                    EstadoEjecucion.EncoladoOk,
                    null,
                    DateTimeOffset.UtcNow.AddDays(90).ToUnixTimeSeconds()
				);

                SendMessageRequest request = new() {
                    QueueUrl = sqsQueueUrl,
                    MessageBody = ejecucionCreada.IdEjecucion
				};

                SendMessageResponse response = await sqsClient.SendMessageAsync(request);

                LambdaLogger.Log(
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Se despacha exitosamente el proceso ID {relacion.IdProceso} - ID Ejecución: {ejecucionCreada.IdEjecucion}.");

            } catch(Exception ex) {
                if (ejecucionCreada != null) {
                    await ejecucionDao.CambiarEstado(ejecucionCreada.IdEjecucion, EstadoEjecucion.ErrorAlEncolar, $"Error al encolar proceso - {ex}");
                } else {
					ejecucionCreada = await ejecucionDao.Crear(
					    Guid.NewGuid().ToString(),
					    relacion.IdProceso,
					    DateTime.UtcNow,
					    EstadoEjecucion.ErrorAlEncolar,
						$"Error al encolar proceso - {ex}",
					    DateTimeOffset.UtcNow.AddDays(90).ToUnixTimeSeconds()
				    );
				}

                LambdaLogger.Log(LogLevel.Error,
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Ocurrio un error al despachar proceso - ID: {relacion.IdProceso} - ID Ejecución: {ejecucionCreada.IdEjecucion}. " +
                    $"{ex}");
            }
        }

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Termino exitosamente el dispatcher de procesos.");
    }
}
