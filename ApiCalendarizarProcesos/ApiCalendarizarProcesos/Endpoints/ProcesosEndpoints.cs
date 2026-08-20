using Amazon.Lambda.Core;
using ApiCalendarizarProcesos.Exceptions;
using ApiCalendarizarProcesos.Helpers;
using ApiCalendarizarProcesos.Models;
using ApiCalendarizarProcesos.UseCases;
using LibreriaCompartida.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ApiCalendarizarProcesos.Endpoints {
    public static class ProcesosEndpoints {
        public static IEndpointRouteBuilder MapProcesosEndpoints(this IEndpointRouteBuilder routes) {
            RouteGroupBuilder group = routes.MapGroup("/Procesos");
            group.MapPostEndpoint();
			group.MapPostVariosEndpoint();
			group.MapDeleteEndpoint();
			group.MapDeleteVariosEndpoint();
			group.MapGetEjecucionesEndpoint();
			group.MapGetProcesosEndpoint();
            group.MapGetCalendarizacionesEndpoint();

			return routes;
        }

        private static IEndpointRouteBuilder MapPostEndpoint(this IEndpointRouteBuilder routes) {
            routes.MapPost("/", async (EntIngresarProceso entrada, ProcesoUseCase procesoUseCase) => {
                Stopwatch stopwatch = Stopwatch.StartNew();

                try {
					(_, Proceso proceso, _, _, _) = await procesoUseCase.RegistrarProcesoSiNoExiste(
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
				} catch (ErrorValidacion ex) {
					LambdaLogger.Log(
						$"[POST] - [Procesos] - [Ingresar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status400BadRequest}] - " +
						$"Ocurrió un error de validación. " +
						$"{ex}");
					return Results.BadRequest(ex.MensajeGenerico);
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

		private static IEndpointRouteBuilder MapPostVariosEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapPost("/Varios", async (List<EntIngresarProceso> entrada, ProcesoUseCase procesoUseCase) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					(_, List<Proceso> procesos, _, _) = await procesoUseCase.RegistrarVariosProcesosSiNoExisten(
						entrada.Select(e => (e.Nombre, e.ArnRol, e.ArnProceso, e.Parametros, e.Cron, e.FrecuenciaDias, e.InicioEjecucionUtc)).ToList()
					);

					LambdaLogger.Log(
						$"[POST] - [Procesos] - [IngresarVarios] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						$"Se programan exitosamente los procesos.");
					return Results.Ok(procesos);
				} catch (ErrorValidacion ex) {
					LambdaLogger.Log(
						$"[POST] - [Procesos] - [IngresarVarios] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status400BadRequest}] - " +
						$"Ocurrió un error de validación. " +
						$"{ex}");
					return Results.BadRequest(ex.MensajeGenerico);
				} catch (Exception ex) {
					LambdaLogger.Log(
						$"[POST] - [Procesos] - [IngresarVarios] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
						$"Ocurrio un error al programar los procesos. " +
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
				} catch (ErrorValidacion ex) {
					LambdaLogger.Log(
						$"[DELETE] - [Procesos] - [Eliminar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status400BadRequest}] - " +
						$"Ocurrió un error de validación. " +
						$"{ex}");
					return Results.BadRequest(ex.MensajeGenerico);
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

		private static IEndpointRouteBuilder MapDeleteVariosEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapDelete("/Varios", async (List<string> idsProcesos, ProcesoUseCase procesoUseCase) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					await procesoUseCase.QuitarVariosProcesosSiExisten(idsProcesos);

					LambdaLogger.Log(
						$"[DELETE] - [Procesos] - [EliminarVarios] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						$"Se descalendarizan exitosamente los procesos.");
					return Results.Ok();
				} catch (ErrorValidacion ex) {
					LambdaLogger.Log(
						$"[DELETE] - [Procesos] - [EliminarVarios] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status400BadRequest}] - " +
						$"Ocurrió un error de validación. " +
						$"{ex}");
					return Results.BadRequest(ex.MensajeGenerico);
				} catch (Exception ex) {
					LambdaLogger.Log(
						$"[DELETE] - [Procesos] - [EliminarVarios] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
						$"Ocurrio un error al descalendarizar los procesos. " +
						$"{ex}");
					return Results.Problem("Ocurrió un error al procesar su solicitud.");
				}
			});

			return routes;
		}

		private static IEndpointRouteBuilder MapGetEjecucionesEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapGet("/Ejecuciones", async ([FromQuery] string? formato, [FromQuery] string filtroIdProceso, ProcesoUseCase procesoUseCase) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					List<Ejecucion> retorno = await procesoUseCase.ObtenerEjecucionesPorProceso(filtroIdProceso);

					formato ??= "json";
					if (formato.Equals("csv", StringComparison.InvariantCultureIgnoreCase)) {
						byte[] csv = CsvHelper.ToCsv([.. retorno.Select(r => new Dictionary<string, object?>() {
							["IdEjecucion"] = r.IdEjecucion,
							["IdProceso"] = r.IdProceso,
							["FechaEncolamientoUtc"] = r.FechaEncolamientoUtc,
							["FechaEjecucionUtc"] = r.FechaEjecucionUtc,
							["Estado"] = r.Estado,
							["Observacion"] = r.Observacion,
						})]);

						LambdaLogger.Log(
							$"[GET] - [Procesos] - [GetEjecuciones] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
							$"Se obtienen exitosamente las ejecuciones en formato CSV - Cantidad: {retorno.Count}.");

						return Results.File(
							csv,
							"text/csv",
							$"Ejecuciones_{Guid.NewGuid()}.csv"
						);
					}

					LambdaLogger.Log(
						$"[GET] - [Procesos] - [GetEjecuciones] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						$"Se obtienen exitosamente las ejecuciones - Cantidad: {retorno.Count}.");

					return Results.Ok(retorno);
				} catch (Exception ex) {
					LambdaLogger.Log(
						$"[GET] - [Procesos] - [GetEjecuciones] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
						$"Ocurrio un error al obtener las ejecuciones según proceso - Id Proceso: {filtroIdProceso}. " +
						$"{ex}");
					return Results.Problem("Ocurrió un error al procesar su solicitud.");
				}
			});

			return routes;
		}

		private static IEndpointRouteBuilder MapGetProcesosEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapGet("/Procesos", async ([FromQuery] string? formato, [FromQuery] string? filtroNombre, [FromQuery] string? filtroIdCalendarizacion, ProcesoUseCase procesoUseCase) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					List<Proceso> retorno;
					if (string.IsNullOrWhiteSpace(filtroIdCalendarizacion)) {
						retorno = await procesoUseCase.ObtenerTodosProcesos();
					} else {
						retorno = await procesoUseCase.ObtenerProcesosPorCalendarizacion(filtroIdCalendarizacion);
					}

					retorno = [.. retorno.Where(p => {
						if (string.IsNullOrWhiteSpace(filtroNombre)) return true;
						return p.Nombre.Contains(filtroNombre, StringComparison.InvariantCultureIgnoreCase);
					})];

                    formato ??= "json";
					if (formato.Equals("csv", StringComparison.InvariantCultureIgnoreCase)) {
						byte[] csv = CsvHelper.ToCsv([.. retorno.Select(r => new Dictionary<string, object?>() {
							["IdProceso"] = r.IdProceso,
							["IdCalendarizacion"] = r.IdCalendarizacion,
							["Nombre"] = r.Nombre,
							["ArnRol"] = r.ArnRol,
							["ArnProceso"] = r.ArnProceso,
							["Parametros"] = r.Parametros,
							["FechaUltimaEjecucionUtc"] = r.FechaUltimaEjecucionUtc,
							["FechaCreacionUtc"] = r.FechaCreacionUtc
						})]);

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
						$"Ocurrio un error al obtener los procesos según filtro - Nombre: {filtroNombre} - Id Calendarización: {filtroIdCalendarizacion}. " +
						$"{ex}");
					return Results.Problem("Ocurrió un error al procesar su solicitud.");
				}
			});

			return routes;
		}

		private static IEndpointRouteBuilder MapGetCalendarizacionesEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapGet("/Calendarizaciones", async ([FromQuery] string? formato, [FromQuery] string? filtroNombre, ProcesoUseCase procesoUseCase) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					List<Calendarizacion> retorno = [.. (await procesoUseCase.ObtenerTodasCalendarizaciones())
						.Where(p => {
							if (string.IsNullOrWhiteSpace(filtroNombre)) return true;
							return p.Nombre.Contains(filtroNombre, StringComparison.InvariantCultureIgnoreCase);
						})
					];

					formato ??= "json";
					if (formato.Equals("csv", StringComparison.InvariantCultureIgnoreCase)) {
                        byte[] csv = CsvHelper.ToCsv([.. retorno.Select(r => new Dictionary<string, object?>() {
							["IdCalendarizacion"] = r.IdCalendarizacion,
							["Nombre"] = r.Nombre,
							["Descripcion"] = r.Descripcion,
							["Grupo"] = r.Grupo,
							["Arn"] = r.Arn,
							["CantProcesos"] = r.CantProcesos,
							["Cron"] = r.Cron,
							["FrecuenciaDias"] = r.FrecuenciaDias,
							["InicioEjecucionUtc"] = r.InicioEjecucionUtc,
							["FechaCreacionUtc"] = r.FechaCreacionUtc,
						})]);

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
