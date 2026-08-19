using ApiCalendarizarProcesos.Business;
using ApiCalendarizarProcesos.Models;
using LibreriaCompartida.Entities;
using LibreriaCompartida.Interfaces.Helpers;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ApiCalendarizarProcesos.UseCases {
	public class ProcesoUseCase(IVariableEntornoHelper variableEntorno, CalendarizacionBusiness calendarizacionBusiness, ProcesoBusiness procesoBusiness, IDynamoHelper dynamoHelper) {
		public async Task<(Calendarizacion, Proceso)> RegistrarProcesoSiNoExiste(string nombre, string arnRol, string arnProceso, string parametros, string? cron, int? frecuenciaDias, DateTime? inicioEjecucionUtc) {
			Calendarizacion? calendarizacionCreada = null;
			Schedule? scheduleCreado = null;
			Proceso? procesoCreado = null;
			try {
				nombre = Regex.Replace(nombre.Trim(), @"\s+", " ", RegexOptions.NonBacktracking);
				if (cron != null) cron = Regex.Replace(cron.Trim(), @"\s+", " ", RegexOptions.NonBacktracking).ToUpperInvariant();

				(Calendarizacion calendarizacion, scheduleCreado, calendarizacionCreada) = await calendarizacionBusiness.ObtenerOCrear(cron, frecuenciaDias, inicioEjecucionUtc);
				(Proceso proceso, procesoCreado) = await procesoBusiness.ObtenerOCrear(nombre, arnRol, arnProceso, parametros, calendarizacion.IdCalendarizacion);

				return (calendarizacion, proceso);
			} catch {
				await procesoBusiness.ReversarCreado(procesoCreado);
				await calendarizacionBusiness.ReversarCreados(scheduleCreado, calendarizacionCreada);
				throw;
			}
		}

		public async Task QuitarProcesoSiExiste(string idProceso) {
			Calendarizacion? calendarizacionEliminada = null;
			Schedule? scheduleEliminado = null;
			Proceso? procesoEliminado = null;
			try {
				procesoEliminado = await procesoBusiness.Eliminar(idProceso);
				if (procesoEliminado != null) {
					(calendarizacionEliminada, scheduleEliminado) = await calendarizacionBusiness.EliminarSiNoTieneProcesos(procesoEliminado.IdCalendarizacion);
				}
			} catch {
				await calendarizacionBusiness.ReversarEliminados(calendarizacionEliminada, scheduleEliminado);
				await procesoBusiness.ReversarEliminado(procesoEliminado);
				throw;
			}
		}

		public async Task<(int calMigrados, int calTotales, int procMigrados, int proTotales)> MigrarANuevoModelo() {
			string nombreTablaCalendarizaciones = variableEntorno.Obtener("DYNAMO_TABLE_CALENDARIZACIONES_NAME");
			string nombreTablaProcesos = variableEntorno.Obtener("DYNAMO_TABLE_PROCESOS_NAME");

			List<Dictionary<string, object?>> calendarizaciones = await dynamoHelper.ObtenerTodos(nombreTablaCalendarizaciones);
			List<Dictionary<string, object?>> procesos = await dynamoHelper.ObtenerTodos(nombreTablaProcesos);

			int calMigrados = 0;
			int calTotales = 0;
			foreach (Dictionary<string, object?> calendarizacion in calendarizaciones) {
				calTotales += 1;

				string? cron = calendarizacion.TryGetValue("Cron", out object? c) && c != null ? (string)c : null;
				int? frecuenciaDias = calendarizacion.TryGetValue("FrecuenciaDias", out object? f) && f != null ? (int)f : null;
				DateTime? inicioEjecucionUtc = calendarizacion.TryGetValue("InicioEjecucion", out object? i) && i != null ? DateTime.ParseExact((string)i, "O", CultureInfo.InvariantCulture) : null;

				if (cron != null || frecuenciaDias != null) {
					await calendarizacionBusiness.ObtenerOCrear(cron, frecuenciaDias, inicioEjecucionUtc);
					calMigrados += 1;
				}
			}

			int procMigrados = 0;
			int proTotales = 0;
			foreach (Dictionary<string, object?> proceso in procesos) {
				proTotales += 1;

				string? nombre = proceso.TryGetValue("Nombre", out object? n) && n != null ? (string)n : null;
				string? arnRol = proceso.TryGetValue("ArnRol", out object? ar) && ar != null ? (string)ar : null;
				string? arnProceso = proceso.TryGetValue("ArnProceso", out object? ap) && ap != null ? (string)ap : null;
				string? parametros = proceso.TryGetValue("Parametros", out object? p) && p != null ? (string)p : null;
				string? idCalendarizacion = proceso.TryGetValue("IdCalendarizacion", out object? c) && c != null ? (string)c : null;

				if (nombre != null && arnRol != null && arnProceso != null && parametros != null && idCalendarizacion != null) {
					await procesoBusiness.ObtenerOCrear(
						nombre,
						arnRol,
						arnProceso,
						parametros,
						idCalendarizacion
					);
					procMigrados += 1;
				}
			}

			return (calMigrados, calTotales, procMigrados, proTotales);
		}
	}
}
