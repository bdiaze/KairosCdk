using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.Scheduler;
using ApiCalendarizarProcesos.Helpers;
using ApiCalendarizarProcesos.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ApiCalendarizarProcesos.Endpoints {
    public static class ProcesosEndpoints {
        public static IEndpointRouteBuilder MapProcesosEndpoints(this IEndpointRouteBuilder routes) {
            RouteGroupBuilder group = routes.MapGroup("/Procesos");
            group.MapPostEndpoint();

            return routes;
        }

        private static IEndpointRouteBuilder MapPostEndpoint(this IEndpointRouteBuilder routes) {
            routes.MapPost("/", async (EntIngresarProceso entrada, VariableEntornoHelper variableEntorno, ParameterStoreHelper parameterStore, SchedulerHelper scheduler, DynamoHelper dynamo) => {
                Stopwatch stopwatch = Stopwatch.StartNew();

                try {
                    string nombreAplicacion = variableEntorno.Obtener("APP_NAME");

                    string nombreTablaProcesos = await parameterStore.ObtenerParametro($"/{nombreAplicacion}/DynamoDB/NombreTablaProcesos");
                    string nombreTablaCalendarizaciones = await parameterStore.ObtenerParametro($"/{nombreAplicacion}/DynamoDB/NombreTablaCalendarizaciones");
                    string nombreScheduleGroup = await parameterStore.ObtenerParametro($"/{nombreAplicacion}/Schedule/NombreGrupo");
                    string arnRoleSchedule = await parameterStore.ObtenerParametro($"/{nombreAplicacion}/Schedule/RoleArn");
                    string arnLambdaDispatcher = await parameterStore.ObtenerParametro($"/{nombreAplicacion}/Dispatcher/LambdaArn");

                    // Se limpia la entrada...
                    entrada.Nombre = Regex.Replace(entrada.Nombre.Trim(), @"\s+", " ").ToUpperInvariant();
                    entrada.Cron = Regex.Replace(entrada.Cron.Trim(), @"\s+", " ").ToUpperInvariant();

                    // Se valida si existe el schedule, si no existe entonces se crea y registra en dynamoDB...
                    string idCalendarizacion = $"calendarizacion-{Convert.ToBase64String(Encoding.UTF8.GetBytes(entrada.Cron))}";
                    Schedule? scheduleExistente = await scheduler.Obtener(idCalendarizacion, nombreScheduleGroup);
                    if (scheduleExistente == null) {
                        scheduleExistente = await scheduler.Crear(
                            idCalendarizacion, 
                            $"Calendarizacion de {nombreAplicacion} para cron({entrada.Cron})", 
                            nombreScheduleGroup, 
                            entrada.Cron,
                            arnRoleSchedule,
                            arnLambdaDispatcher,
                            JsonSerializer.Serialize(new DispatcherInput { 
                                IdCalendarizacion = idCalendarizacion
                            }, AppJsonSerializerContext.Default.DispatcherInput)
                        );
                        if (scheduleExistente != null) {
                            await dynamo.Crear(nombreTablaCalendarizaciones, new Document {
                                ["IdCalendarizacion"] = scheduleExistente.Nombre,
                                ["Nombre"] = scheduleExistente.Nombre,
                                ["Descripcion"] = scheduleExistente.Descripcion,
                                ["Grupo"] = scheduleExistente.Grupo,
                                ["Cron"] = scheduleExistente.Cron,
                                ["Arn"] = scheduleExistente.Arn,
                                ["FechaCreacion"] = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
                            });
                        }
                    }

                    // Se valida si ya existe el proceso en dynamoDB, si no existe entonces se registra...
                    string idProceso = $"proceso-{Convert.ToBase64String(Encoding.UTF8.GetBytes(entrada.Nombre))}";
                    Document procesoExistente = await dynamo.Obtener(nombreTablaProcesos, entrada.Nombre);
                    procesoExistente ??= await dynamo.Crear(nombreTablaProcesos, new Document {
                        ["IdProceso"] = idProceso,
                        ["IdCalendarizacion"] = idCalendarizacion,
                        ["Nombre"] = entrada.Nombre,
                        ["ArnProceso"] = entrada.ArnProceso,
                        ["Parametros"] = dynamo.ToDynamoDbEntry(entrada.Parametros),
                        ["Habilitado"] = entrada.Habilitado ? DynamoDBBool.True : DynamoDBBool.False,
                        ["FechaCreacion"] = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
                    });

                    LambdaLogger.Log(
                        $"[POST] - [Procesos] - [Ingresar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
                        $"Se programa exitosamente el proceso.");

                    return Results.Ok(procesoExistente);
                } catch (Exception ex) {
                    LambdaLogger.Log(
                        $"[POST] - [Procesos] - [Ingresar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
                        $"Ocurrió un error al programar el proceso. " +
                        $"{ex}");
                    return Results.Problem("Ocurrió un error al procesar su solicitud.");
                }
            });

            return routes;
        }
    }
}
