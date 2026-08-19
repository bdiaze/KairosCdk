using ApiCalendarizarProcesos.Business;
using ApiCalendarizarProcesos.Models;
using LibreriaCompartida.Entities;
using System.Text.RegularExpressions;

namespace ApiCalendarizarProcesos.UseCases {
	public class ProcesoUseCase(CalendarizacionBusiness calendarizacionBusiness, ProcesoBusiness procesoBusiness) {
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
	}
}
