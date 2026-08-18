using ApiCalendarizarProcesos.Business;
using ApiCalendarizarProcesos.Models;
using LibreriaCompartida.Entities;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Interfaces.Helpers;
using LibreriaCompartida.Repositories;
using System.Text.RegularExpressions;

namespace ApiCalendarizarProcesos.UseCases {
	public class ProcesoUseCase(CalendarizacionBusiness calendarizacionBusiness, ProcesoBusiness procesoBusiness) {
		public async Task RegistrarProcesoSiNoExiste(string nombre, string arnRol, string arnProceso, string parametros, string? cron, int? frecuenciaDias, DateTime? inicioEjecucionUtc) {
			Calendarizacion? calendarizacionCreada = null;
			Schedule? scheduleCreado = null;
			Proceso? procesoCreado = null;
			try {
				nombre = Regex.Replace(nombre.Trim(), @"\s+", " ", RegexOptions.NonBacktracking);
				if (cron != null) cron = Regex.Replace(cron.Trim(), @"\s+", " ", RegexOptions.NonBacktracking).ToUpperInvariant();

				string idCalendarizacion = NombresHelper.GenerarNombreCalendarizacion(cron, frecuenciaDias, inicioEjecucionUtc);
				string idProceso = NombresHelper.GenerarNombreProceso(nombre);

				(Calendarizacion calendarizacion, scheduleCreado, calendarizacionCreada) = await calendarizacionBusiness.ObtenerOCrear(cron, frecuenciaDias, inicioEjecucionUtc);
				(Proceso proceso, procesoCreado) = await procesoBusiness.ObtenerOCrear(nombre, arnRol, arnProceso, parametros, calendarizacion.IdCalendarizacion);
			} catch {
				await procesoBusiness.ReversarCreado(procesoCreado);
				await calendarizacionBusiness.ReversarCreados(scheduleCreado, calendarizacionCreada);
				throw;
			}
		}
	}
}
