using Amazon.Lambda.Core;
using ApiCalendarizarProcesos.Helpers;
using ApiCalendarizarProcesos.Interfaces.Helpers;
using ApiCalendarizarProcesos.Models;
using ApiCalendarizarProcesos.UseCases;
using LibreriaCompartida.Entities;
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
			group.MapMigrarModeloEndpoint();

			return routes;
        }

        private static IEndpointRouteBuilder MapPostEndpoint(this IEndpointRouteBuilder routes) {
            routes.MapPost("/", async (EntIngresarProceso entrada, ProcesoUseCase procesoUseCase) => {
                Stopwatch stopwatch = Stopwatch.StartNew();

                try {
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

					(_, Proceso proceso) = await procesoUseCase.RegistrarProcesoSiNoExiste(
                        entrada.Nombre,
                        entrada.ArnRol,
                        entrada.ArnProceso,
                        entrada.Parametros,
                        entrada.Cron,
                        entrada.FrecuenciaDias,
                        entrada.InicioEjecucionUtc
                    );

                    LambdaLogger.Log(
                        $"[POST] - [Procesos] - [Ingresar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
                        $"Se programa exitosamente el proceso.");

                    return Results.Ok(proceso);
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
            routes.MapDelete("/{idProceso}", async (string idProceso, ProcesoUseCase procesoUseCase) => {
                Stopwatch stopwatch = Stopwatch.StartNew();

                try {
					await procesoUseCase.QuitarProcesoSiExiste(idProceso);

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

		private static IEndpointRouteBuilder MapMigrarModeloEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapPost("/MigrarModelo", async (ProcesoUseCase procesoUseCase) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
                    (int calMigrados, int calTotales, int procMigrados, int procTotales) = await procesoUseCase.MigrarANuevoModelo();

					LambdaLogger.Log(
						$"[POST] - [Procesos] - [MigrarModelo] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						$"Se migra exitosamente el modelo - Cal. Migrados: {calMigrados} - Cal. Totales: {calTotales} - Proc. Migrados: {procMigrados} - Proc. Totales: {procTotales}.");

					return Results.Ok();
				} catch (Exception ex) {
					LambdaLogger.Log(
						$"[POST] - [Procesos] - [MigrarModelo] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
						$"Ocurrio un error al migrar el modelo. " +
						$"{ex}");
					return Results.Problem("Ocurrió un error al procesar su solicitud.");
				}
			});

			return routes;
		}
	}
}
