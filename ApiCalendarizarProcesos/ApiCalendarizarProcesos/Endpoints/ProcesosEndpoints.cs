using Amazon.Lambda.Core;
using ApiCalendarizarProcesos.Helpers;
using ApiCalendarizarProcesos.Interfaces.Helpers;
using ApiCalendarizarProcesos.Models;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Interfaces.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ApiCalendarizarProcesos.Endpoints {
    public static class ProcesosEndpoints {
        public static IEndpointRouteBuilder MapProcesosEndpoints(this IEndpointRouteBuilder routes) {
            RouteGroupBuilder group = routes.MapGroup("/Procesos");
            group.MapPostEndpoint();
            group.MapDeleteEndpoint();
            group.MapGetProcesosEndpoint();
            group.MapGetCalendarizacionesEndpoint();

			return routes;
        }

        private static IEndpointRouteBuilder MapPostEndpoint(this IEndpointRouteBuilder routes) {
            routes.MapPost("/", async (EntIngresarProceso entrada, IVariableEntornoHelper variableEntorno, ISchedulerHelper scheduler, IDynamoHelper dynamo) => {
                Stopwatch stopwatch = Stopwatch.StartNew();

                try {
                    string nombreAplicacion = variableEntorno.Obtener("APP_NAME");

                    string nombreTablaProcesos = variableEntorno.Obtener("DYNAMO_TABLE_PROCESOS_NAME");
                    string nombreTablaCalendarizaciones = variableEntorno.Obtener("DYNAMO_TABLE_CALENDARIZACIONES_NAME");
                    string nombreScheduleGroup = variableEntorno.Obtener("NOMBRE_SCHEDULE_GROUP");
                    string arnRoleSchedule = variableEntorno.Obtener("ARN_ROLE_SCHEDULE");
                    string arnDlqSchedule = variableEntorno.Obtener("ARN_DLQ_SCHEDULE");
                    string arnLambdaDispatcher = variableEntorno.Obtener("ARN_LAMBDA_DISPATCHER");

                    // Se limpia la entrada...
                    entrada.Nombre = Regex.Replace(entrada.Nombre.Trim(), @"\s+", " ", RegexOptions.NonBacktracking);
                    if (entrada.Cron != null) entrada.Cron = Regex.Replace(entrada.Cron.Trim(), @"\s+", " ", RegexOptions.NonBacktracking).ToUpperInvariant();

                    // Se valida que venga cron o frecuencia en días (no ambos al mismo tiempo)...
                    if ((entrada.Cron == null && entrada.FrecuenciaDias == null) || (entrada.Cron != null && entrada.FrecuenciaDias != null)) {
						LambdaLogger.Log(
						    $"[POST] - [Procesos] - [Ingresar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status400BadRequest}] - " +
						    $"Se debe indicar una configuración cron o una frecuencia en días.");
						return Results.BadRequest("Se debe indicar una configuración cron o una frecuencia en días.");
					}

                    // Se valida que si viene frecuencia en días, también se incluya el inicio de las ejecuciones...
                    if (entrada.FrecuenciaDias != null && entrada.InicioEjecucionUtc == null) {
						LambdaLogger.Log(
							$"[POST] - [Procesos] - [Ingresar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status400BadRequest}] - " +
							$"Junto con indicar la frecuencia en días, se debe indicar la fecha en que inicia la ejecución del proceso.");
						return Results.BadRequest("Junto con indicar la frecuencia en días, se debe indicar la fecha en que inicia la ejecución del proceso.");
					}

                    // Se valida que la fecha de inicio de ejecución sea futura...
                    if (entrada.InicioEjecucionUtc != null && entrada.InicioEjecucionUtc <= DateTime.UtcNow) {
                        LambdaLogger.Log(
                            $"[POST] - [Procesos] - [Ingresar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status400BadRequest}] - " +
                            $"La fecha de inicio de ejecución debe ser una fecha futura.");
                        return Results.BadRequest("La fecha de inicio de ejecución debe ser una fecha futura.");
					}

                    // Se valida si existe el schedule, si no existe entonces se crea y registra en dynamoDB...
                    string idCalendarizacion = $"{NombresHelper.GenerarNombreCalendarizacion(entrada.Cron, entrada.FrecuenciaDias, entrada.InicioEjecucionUtc)}";
                    Schedule? scheduleExistente = await scheduler.Obtener(idCalendarizacion, nombreScheduleGroup);
                    if (scheduleExistente == null) {
                        string descripcionInicio = "";
                        if (entrada.InicioEjecucionUtc != null) {
							TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
							DateTime inicioEjecucionChile = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(entrada.InicioEjecucionUtc.Value, DateTimeKind.Utc), timeZoneInfo);
                            descripcionInicio = $"start_date({inicioEjecucionChile:yyyy.MM.dd HH.mm}) - ";
						}
                        string descripcionFrecuencia = entrada.Cron != null ? $"cron({entrada.Cron})" : $"rate({entrada.FrecuenciaDias} days)";
                        scheduleExistente = await scheduler.Crear(
                            idCalendarizacion, 
                            $"Calendarizacion de {nombreAplicacion} para {descripcionInicio}{descripcionFrecuencia}", 
                            nombreScheduleGroup, 
                            entrada.Cron,
                            entrada.FrecuenciaDias,
                            entrada.InicioEjecucionUtc,
                            arnRoleSchedule,
                            arnDlqSchedule,
                            arnLambdaDispatcher,
                            JsonSerializer.Serialize(new DispatcherInput { 
                                IdCalendarizacion = idCalendarizacion
                            }, AppJsonSerializerContext.Default.DispatcherInput)
                        );
                        if (scheduleExistente != null) {
                            await dynamo.Insertar(nombreTablaCalendarizaciones, new Dictionary<string, object?> {
                                ["IdCalendarizacion"] = scheduleExistente.Nombre,
                                ["Nombre"] = scheduleExistente.Nombre,
                                ["Descripcion"] = scheduleExistente.Descripcion,
                                ["Grupo"] = scheduleExistente.Grupo,
                                ["Cron"] = scheduleExistente.Cron,
								["FrecuenciaDias"] = scheduleExistente.FrecuenciaDias,
								["InicioEjecucion"] = scheduleExistente.InicioEjecucionUtc?.ToString("O"),
								["Arn"] = scheduleExistente.Arn,
                                ["FechaCreacion"] = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
                            });
                        }
                    }

                    // Se valida si ya existe el proceso en dynamoDB, si no existe entonces se registra...
                    string idProceso = NombresHelper.GenerarNombreProceso(entrada.Nombre);
                    Dictionary<string, object?>? procesoExistente = await dynamo.Obtener(nombreTablaProcesos, new Dictionary<string, object?> {
                        ["IdProceso"] = idProceso
                    });
                    procesoExistente ??= await dynamo.Insertar(nombreTablaProcesos, new Dictionary<string, object?> {
                        ["IdProceso"] = idProceso,
                        ["IdCalendarizacion"] = idCalendarizacion,
                        ["Nombre"] = entrada.Nombre,
                        ["ArnRol"] = entrada.ArnRol,
                        ["ArnProceso"] = entrada.ArnProceso,
                        ["Parametros"] = entrada.Parametros,
                        ["Habilitado"] = entrada.Habilitado,
                        ["FechaCreacion"] = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
                    });

                    LambdaLogger.Log(
                        $"[POST] - [Procesos] - [Ingresar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
                        $"Se programa exitosamente el proceso.");

                    return Results.Ok(procesoExistente);
                } catch (Exception ex) {
                    LambdaLogger.Log(
                        $"[POST] - [Procesos] - [Ingresar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
                        $"Ocurrio un error al programar el proceso. " +
                        $"{ex}");
                    return Results.Problem("Ocurrió un error al procesar su solicitud.");
                }
            });

            return routes;
        }

        private static IEndpointRouteBuilder MapDeleteEndpoint(this IEndpointRouteBuilder routes) {
            routes.MapDelete("/{idProceso}", async (string idProceso, IVariableEntornoHelper variableEntorno, ISchedulerHelper scheduler, IDynamoHelper dynamo) => {
                Stopwatch stopwatch = Stopwatch.StartNew();

                try {
                    string nombreAplicacion = variableEntorno.Obtener("APP_NAME");

					string nombreTablaProcesos = variableEntorno.Obtener("DYNAMO_TABLE_PROCESOS_NAME");
					string nombreTablaCalendarizaciones = variableEntorno.Obtener("DYNAMO_TABLE_CALENDARIZACIONES_NAME");
					string nombreScheduleGroup = variableEntorno.Obtener("NOMBRE_SCHEDULE_GROUP");
					string arnRoleSchedule = variableEntorno.Obtener("ARN_ROLE_SCHEDULE");
					string arnLambdaDispatcher = variableEntorno.Obtener("ARN_LAMBDA_DISPATCHER");

                    // Se obtiene el proceso a eliminar...
                    Dictionary<string, object?>? proceso = await dynamo.Obtener(nombreTablaProcesos, new Dictionary<string, object?> {
                        ["IdProceso"] = idProceso
                    });

                    // Si el proceso existe, se elimina...
                    if (proceso != null && proceso.TryGetValue("IdProceso", out object? value)) {

                        // Antes de eliminar el proceso, si no quedan otros procesos en la calendarización, tambien se elimina ésta...
                        if (proceso.TryGetValue("IdCalendarizacion", out object? idCalendarizacion) && idCalendarizacion != null) {
                            List<Dictionary<string, object?>> procesos = await dynamo.ObtenerPorIndice(
                                nombreTablaProcesos,
                                "PorIdCalendarizacion",
                                "IdCalendarizacion",
                                (string)idCalendarizacion
                            );

                            if (procesos.Count == 1) {
                                // Si existe el schedule de eventbridge, se elimina...
                                Schedule? schedule = await scheduler.Obtener((string)idCalendarizacion, nombreScheduleGroup);
                                if (schedule != null) {
                                    await scheduler.Eliminar(schedule.Nombre, schedule.Grupo);
                                }

                                // Si existe el registro en dynamo, se elimina...
                                Dictionary<string, object?>? calendarizacion = await dynamo.Obtener(nombreTablaCalendarizaciones, new Dictionary<string, object?> {
                                    ["IdCalendarizacion"] = idCalendarizacion
                                });
                                if (calendarizacion != null) {
                                    await dynamo.Eliminar(nombreTablaCalendarizaciones, new Dictionary<string, object?> {
                                        ["IdCalendarizacion"] = idCalendarizacion
                                    });
                                }
                            } 
                        }

                        await dynamo.Eliminar(nombreTablaProcesos, new Dictionary<string, object?> {
                            ["IdProceso"] = value
                        });
                    }

                    LambdaLogger.Log(
                        $"[DELETE] - [Procesos] - [Eliminar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
                        $"Se descalendariza exitosamente el proceso.");

                    return Results.Ok();
                } catch (Exception ex) {
                    LambdaLogger.Log(
                        $"[DELETE] - [Procesos] - [Eliminar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
                        $"Ocurrio un error al descalendarizar el proceso. " +
                        $"{ex}");
                    return Results.Problem("Ocurrió un error al procesar su solicitud.");
                }
            });

            return routes;
        }

		private static IEndpointRouteBuilder MapGetProcesosEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapGet("/Procesos", async ([FromQuery] string? formato, [FromQuery] string? filtroNombre, IVariableEntornoHelper variableEntorno, IDynamoHelper dynamo) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					string nombreAplicacion = variableEntorno.Obtener("APP_NAME");
					string nombreTablaProcesos = variableEntorno.Obtener("DYNAMO_TABLE_PROCESOS_NAME");

                    List<Dictionary<string, object?>> retorno = [.. (await dynamo.ObtenerTodos(nombreTablaProcesos))
                        .Where(p => {
                            if (string.IsNullOrWhiteSpace(filtroNombre)) return true;

                            return p.TryGetValue("Nombre", out object? nombre) &&
                                   nombre is string nombreTexto &&
                                   nombreTexto.Contains(filtroNombre, StringComparison.InvariantCultureIgnoreCase);
                        })
                    ];

                    formato ??= "json";
					if (formato.Equals("csv", StringComparison.InvariantCultureIgnoreCase)) {
						byte[] csv = CsvHelper.ToCsv(retorno);

						LambdaLogger.Log(
							$"[GET] - [Procesos] - [GetProcesos] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
							$"Se obtienen exitosamente los procesos en formato CSV - Cantidad: {retorno.Count}.");

						return Results.File(
							csv,
							"text/csv",
							$"Procesos_{Guid.NewGuid()}.csv"
						);
					}

					LambdaLogger.Log(
						$"[GET] - [Procesos] - [GetProcesos] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						$"Se obtienen exitosamente los procesos - Cantidad: {retorno.Count}.");

					return Results.Ok(retorno);
				} catch (Exception ex) {
					LambdaLogger.Log(
						$"[GET] - [Procesos] - [GetProcesos] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
						$"Ocurrio un error al obtener los procesos según filtro - Nombre: {filtroNombre}. " +
						$"{ex}");
					return Results.Problem("Ocurrió un error al procesar su solicitud.");
				}
			});

			return routes;
		}

		private static IEndpointRouteBuilder MapGetCalendarizacionesEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapGet("/Calendarizaciones", async ([FromQuery] string? formato, [FromQuery] string? filtroNombre, IVariableEntornoHelper variableEntorno, IDynamoHelper dynamo) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					string nombreAplicacion = variableEntorno.Obtener("APP_NAME");
					string nombreTablaCalendarizaciones = variableEntorno.Obtener("DYNAMO_TABLE_CALENDARIZACIONES_NAME");

					List<Dictionary<string, object?>> retorno = [.. (await dynamo.ObtenerTodos(nombreTablaCalendarizaciones))
						.Where(p => {
							if (string.IsNullOrWhiteSpace(filtroNombre)) return true;

							return p.TryGetValue("Nombre", out object? nombre) &&
								   nombre is string nombreTexto &&
								   nombreTexto.Contains(filtroNombre, StringComparison.InvariantCultureIgnoreCase);
						})
					];

					formato ??= "json";
					if (formato.Equals("csv", StringComparison.InvariantCultureIgnoreCase)) {
                        byte[] csv = CsvHelper.ToCsv(retorno);

						LambdaLogger.Log(
						    $"[GET] - [Procesos] - [GetCalendarizaciones] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						    $"Se obtienen exitosamente las calendarizaciones en formato CSV - Cantidad: {retorno.Count}.");

						return Results.File(
                            csv,
                            "text/csv",
                            $"Calendarizaciones_{Guid.NewGuid()}.csv"
                        );
                    }

					LambdaLogger.Log(
						$"[GET] - [Procesos] - [GetCalendarizaciones] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						$"Se obtienen exitosamente las calendarizaciones - Cantidad: {retorno.Count}.");

					return Results.Ok(retorno);
				} catch (Exception ex) {
					LambdaLogger.Log(
						$"[GET] - [Procesos] - [GetCalendarizaciones] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
						$"Ocurrio un error al obtener las calendarizaciones según filtro - Nombre: {filtroNombre}. " +
						$"{ex}");
					return Results.Problem("Ocurrió un error al procesar su solicitud.");
				}
			});

			return routes;
		}
	}
}
