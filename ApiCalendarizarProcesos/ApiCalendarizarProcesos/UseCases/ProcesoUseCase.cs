using ApiCalendarizarProcesos.Business;
using ApiCalendarizarProcesos.Exceptions;
using ApiCalendarizarProcesos.Models;
using LibreriaCompartida.Entities;
using LibreriaCompartida.Interfaces.Helpers;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ApiCalendarizarProcesos.UseCases {
	public class ProcesoUseCase(CalendarizacionBusiness calendarizacionBusiness, ProcesoBusiness procesoBusiness) {
		public async Task<List<Calendarizacion>> ObtenerTodasCalendarizaciones() {
			return await calendarizacionBusiness.ObtenerTodos();
		}

		public async Task<List<Proceso>> ObtenerTodosProcesos() {
			return await procesoBusiness.ObtenerTodos();
		}

		public async Task<List<Proceso>> ObtenerProcesosPorCalendarizacion(string idCalendarizacion) {
			return await procesoBusiness.ObtenerPorCalendarizacion(idCalendarizacion);
		}

		public async Task<List<Ejecucion>> ObtenerEjecucionesPorProceso(string idProceso) {
			return await procesoBusiness.ObtenerEjecuciones(idProceso);
		}

		public async Task<(Calendarizacion, Proceso, Proceso? procesoCreado, Schedule? scheduleCreado, Calendarizacion? calendarizacionCreada)> RegistrarProcesoSiNoExiste(string nombre, string arnRol, string arnProceso, string parametros, string? cron, int? frecuenciaDias, DateTime? inicioEjecucionUtc) {
			Calendarizacion? calendarizacionCreada = null;
			Schedule? scheduleCreado = null;
			Proceso? procesoCreado = null;
			try {
				nombre = Regex.Replace(nombre.Trim(), @"\s+", " ", RegexOptions.NonBacktracking);
				if (cron != null) cron = Regex.Replace(cron.Trim(), @"\s+", " ", RegexOptions.NonBacktracking).ToUpperInvariant();

				// Se valida que venga cron o frecuencia en días (no ambos al mismo tiempo)...
				if ((cron == null && frecuenciaDias == null) || (cron != null && frecuenciaDias != null)) {
					throw new ErrorValidacion(TipoErrorValidacion.ValorNoValido, "Se debe indicar una configuración cron o una frecuencia en días.");
				}

				// Se valida que si viene frecuencia en días, también se incluya el inicio de las ejecuciones...
				if (frecuenciaDias != null && inicioEjecucionUtc == null) {
					throw new ErrorValidacion(TipoErrorValidacion.ValorNoValido, "Junto con indicar la frecuencia en días, se debe indicar la fecha en que inicia la ejecución del proceso.");
				}

				// Se valida que la fecha de inicio de ejecución sea futura...
				if (inicioEjecucionUtc != null && inicioEjecucionUtc <= DateTime.UtcNow) {
					throw new ErrorValidacion(TipoErrorValidacion.ValorNoValido, "La fecha de inicio de ejecución debe ser una fecha futura.");
				}

				(Calendarizacion calendarizacion, scheduleCreado, calendarizacionCreada) = await calendarizacionBusiness.ObtenerOCrear(cron, frecuenciaDias, inicioEjecucionUtc);
				(Proceso proceso, procesoCreado) = await procesoBusiness.ObtenerOCrear(nombre, arnRol, arnProceso, parametros, calendarizacion.IdCalendarizacion);

				return (calendarizacion, proceso, procesoCreado, scheduleCreado, calendarizacionCreada);
			} catch {
				await procesoBusiness.ReversarCreado(procesoCreado);
				await calendarizacionBusiness.ReversarCreados(scheduleCreado, calendarizacionCreada);
				throw;
			}
		}

		public async Task<(List<Calendarizacion>, List<Proceso>, List<Proceso?> procesosCreados, List<(Schedule?, Calendarizacion?)> calendarizacionesYSchedulesCreados)> RegistrarVariosProcesosSiNoExisten(List<(string nombre, string arnRol, string arnProceso, string parametros, string? cron, int? frecuenciaDias, DateTime? inicioEjecucionUtc)> listado) {
			List<(Schedule?, Calendarizacion?)> calendarizacionesYSchedulesCreados = [];
			List<Proceso?> procesosCreados = [];

			try {
				List<Calendarizacion> calendarizaciones = [];
				List<Proceso> procesos = [];
				foreach ((string nombre, string arnRol, string arnProceso, string parametros, string? cron, int? frecuenciaDias, DateTime? inicioEjecucionUtc) in listado) {
					(Calendarizacion calendarizacion, Proceso proceso, Proceso? procesoCreado, Schedule? scheduleCreado, Calendarizacion? calendarizacionCreada) = await RegistrarProcesoSiNoExiste(nombre, arnRol, arnProceso, parametros, cron, frecuenciaDias, inicioEjecucionUtc);
					calendarizacionesYSchedulesCreados.Add((scheduleCreado, calendarizacionCreada));
					procesosCreados.Add(procesoCreado);
					procesos.Add(proceso);
					calendarizaciones.Add(calendarizacion);
				}

				return (calendarizaciones, procesos, procesosCreados, calendarizacionesYSchedulesCreados);
			} catch {
				foreach (Proceso? procesoCreado in procesosCreados) {
					await procesoBusiness.ReversarCreado(procesoCreado);
				}
				foreach ((Schedule? scheduleCreado, Calendarizacion? calendarizacionCreada) in calendarizacionesYSchedulesCreados) {
					await calendarizacionBusiness.ReversarCreados(scheduleCreado, calendarizacionCreada);
				}
				throw;
			}
		}

		public async Task<(Proceso? procesoEliminado, Schedule? scheduleEliminado, Calendarizacion? calendarizacionEliminada)> QuitarProcesoSiExiste(string idProceso) {
			Calendarizacion? calendarizacionEliminada = null;
			Schedule? scheduleEliminado = null;
			Proceso? procesoEliminado = null;
			try {
				procesoEliminado = await procesoBusiness.Eliminar(idProceso);
				if (procesoEliminado != null) {
					(calendarizacionEliminada, scheduleEliminado) = await calendarizacionBusiness.EliminarSiNoTieneProcesos(procesoEliminado.IdCalendarizacion);
				}

				return (procesoEliminado, scheduleEliminado, calendarizacionEliminada);
			} catch {
				await calendarizacionBusiness.ReversarEliminados(calendarizacionEliminada, scheduleEliminado);
				await procesoBusiness.ReversarEliminado(procesoEliminado);
				throw;
			}
		}

		public async Task<(List<Proceso?> procesosEliminados, List<(Schedule?, Calendarizacion?)> calendarizacionesYSchedulesEliminados)> QuitarVariosProcesosSiExisten(List<string> idsProcesos) {
			List<(Schedule?, Calendarizacion?)> calendarizacionesYSchedulesEliminados = [];
			List<Proceso?> procesosEliminados = [];

			try {
				foreach (string idProceso in idsProcesos) {
					(Proceso? procesoEliminado, Schedule? scheduleEliminado, Calendarizacion? calendarizacionEliminada) = await QuitarProcesoSiExiste(idProceso);
					calendarizacionesYSchedulesEliminados.Add((scheduleEliminado, calendarizacionEliminada));
					procesosEliminados.Add(procesoEliminado);
				}

				return (procesosEliminados, calendarizacionesYSchedulesEliminados);
			} catch {
				foreach ((Schedule? scheduleEliminado, Calendarizacion? calendarizacionEliminada) in calendarizacionesYSchedulesEliminados) {
					await calendarizacionBusiness.ReversarEliminados(calendarizacionEliminada, scheduleEliminado);
				}
				foreach (Proceso? procesoEliminado in procesosEliminados) {
					await procesoBusiness.ReversarEliminado(procesoEliminado);
				}
				throw;
			}
		}
	}
}
