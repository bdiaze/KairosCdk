using ApiCalendarizarProcesos.Helpers;
using ApiCalendarizarProcesos.Interfaces.Helpers;
using ApiCalendarizarProcesos.Models;
using LibreriaCompartida.Entities;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Interfaces.Helpers;
using LibreriaCompartida.Repositories;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ApiCalendarizarProcesos.Business {
	public class CalendarizacionBusiness(IVariableEntornoHelper variableEntornoHelper, CalendarizacionDao calendarizacionDao, ISchedulerHelper schedulerHelper) {
		private readonly string APP_NAME = variableEntornoHelper.Obtener("APP_NAME");
		private readonly string NOMBRE_SCHEDULE_GROUP = variableEntornoHelper.Obtener("NOMBRE_SCHEDULE_GROUP");
		private readonly string ARN_ROLE_SCHEDULE = variableEntornoHelper.Obtener("ARN_ROLE_SCHEDULE");
		private readonly string ARN_DLQ_SCHEDULE = variableEntornoHelper.Obtener("ARN_DLQ_SCHEDULE");
		private readonly string ARN_LAMBDA_DISPATCHER = variableEntornoHelper.Obtener("ARN_LAMBDA_DISPATCHER");

		private string GenerarDescripcion(string? cron, int? frecuenciaDias, DateTime? inicioEjecucionUtc) {
			string descripcionInicio = "";
			if (inicioEjecucionUtc != null) {
				TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
				DateTime inicioEjecucionChile = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(inicioEjecucionUtc.Value, DateTimeKind.Utc), timeZoneInfo);
				descripcionInicio = $"start_date({inicioEjecucionChile:yyyy.MM.dd HH.mm}) - ";
			}

			string descripcionFrecuencia = cron != null ? $"cron({cron})" : $"rate({frecuenciaDias} days)";

			return $"Calendarizacion de {APP_NAME} para {descripcionInicio}{descripcionFrecuencia}";
		}

		public async Task<(Calendarizacion, Schedule? schedulerCreado, Calendarizacion? calendarizacionCreada)> ObtenerOCrear(string? cron, int? frecuenciaDias, DateTime? inicioEjecucionUtc) {
			Schedule? schedulerCreado = null;
			Calendarizacion? calendarizacionCreada = null;
			try {
				string idCalendarizacion = NombresHelper.GenerarNombreCalendarizacion(cron, frecuenciaDias, inicioEjecucionUtc);

				Schedule? scheduleExistente = await schedulerHelper.Obtener(idCalendarizacion, NOMBRE_SCHEDULE_GROUP);
				if (scheduleExistente == null) {
					scheduleExistente = await schedulerHelper.Crear(
						idCalendarizacion,
						GenerarDescripcion(cron, frecuenciaDias, inicioEjecucionUtc),
						NOMBRE_SCHEDULE_GROUP,
						cron,
						frecuenciaDias,
						inicioEjecucionUtc,
						ARN_ROLE_SCHEDULE,
						ARN_DLQ_SCHEDULE,
						ARN_LAMBDA_DISPATCHER,
						JsonSerializer.Serialize(new DispatcherInput {
							IdCalendarizacion = idCalendarizacion
						}, AppJsonSerializerContext.Default.DispatcherInput)
					);
					schedulerCreado = scheduleExistente;
				}

				Calendarizacion? existente = await calendarizacionDao.Obtener(idCalendarizacion);
				if (existente == null) {
					existente = await calendarizacionDao.Crear(
						scheduleExistente.Nombre,
						scheduleExistente.Nombre,
						scheduleExistente.Descripcion,
						scheduleExistente.Grupo,
						scheduleExistente.Arn,
						scheduleExistente.Cron,
						scheduleExistente.FrecuenciaDias,
						scheduleExistente.InicioEjecucionUtc,
						DateTime.UtcNow
					);
					calendarizacionCreada = existente;
				}

				return (existente!, schedulerCreado, calendarizacionCreada);
			} catch {
				await ReversarCreados(schedulerCreado, calendarizacionCreada);
				throw;
			}
		}

		public async Task ReversarCreados(Schedule? schedulerCreado, Calendarizacion? calendarizacionCreada) {
			if (schedulerCreado != null) {
				await schedulerHelper.Eliminar(schedulerCreado.Nombre, schedulerCreado.Grupo);
			}

			if (calendarizacionCreada != null) {
				await calendarizacionDao.Eliminar(calendarizacionCreada.IdCalendarizacion);
			}
		}

		public async Task<(Calendarizacion? calendarizacionEliminada, Schedule? scheduleEliminado)> EliminarSiNoTieneProcesos(string idCalendarizacion) {
			Calendarizacion? calendarizacionEliminada = null;
			Schedule? scheduleEliminado = null;
			try {
				Calendarizacion? existente = await calendarizacionDao.Obtener(idCalendarizacion);
				if (existente != null && existente.CantProcesos == 0) {
					Schedule? scheduleExistente = await schedulerHelper.ObtenerPorArn(existente.Arn);
					if (scheduleExistente != null) {
						await schedulerHelper.Eliminar(scheduleExistente.Nombre, scheduleExistente.Grupo);
						scheduleEliminado = scheduleExistente;
					}

					await calendarizacionDao.Eliminar(existente.IdCalendarizacion);
					calendarizacionEliminada = existente;
				}

				return (calendarizacionEliminada, scheduleEliminado);
			} catch {
				await ReversarEliminados(calendarizacionEliminada, scheduleEliminado);
				throw;
			}
		}

		public async Task ReversarEliminados(Calendarizacion? calendarizacionEliminada, Schedule? scheduleEliminado) {
			if (scheduleEliminado != null) {
				await schedulerHelper.Crear(
					scheduleEliminado.Nombre,
					scheduleEliminado.Descripcion,
					scheduleEliminado.Grupo,
					scheduleEliminado.Cron,
					scheduleEliminado.FrecuenciaDias,
					scheduleEliminado.InicioEjecucionUtc,
					scheduleEliminado.TargetRoleArn,
					scheduleEliminado.TargetDlqArn,
					scheduleEliminado.TargetArn,
					scheduleEliminado.TargetInput
				);
			}

			if (calendarizacionEliminada != null) {
				await calendarizacionDao.Crear(
					calendarizacionEliminada.IdCalendarizacion,
					calendarizacionEliminada.Nombre,
					calendarizacionEliminada.Descripcion,
					calendarizacionEliminada.Grupo,
					calendarizacionEliminada.Arn,
					calendarizacionEliminada.Cron,
					calendarizacionEliminada.FrecuenciaDias,
					calendarizacionEliminada.InicioEjecucionUtc,
					calendarizacionEliminada.FechaCreacionUtc
				);
			}
		}
	}
}
