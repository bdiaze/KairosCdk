using LibreriaCompartida.Entities;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Repositories;

namespace ApiCalendarizarProcesos.Business {
	public class ProcesoBusiness(ProcesoDao procesoDao, RelacCalendProcDao relacCalendProcDao, RelacProcEjecDao relacProcEjecDao, EjecucionDao ejecucionDao) {
		public async Task<List<Proceso>> ObtenerTodos() {
			return await procesoDao.ObtenerTodos();
		}

		public async Task<List<Proceso>> ObtenerPorCalendarizacion(string idCalendarizacion) {
			List<RelacCalendProc> relaciones = await relacCalendProcDao.ObtenerPorCalendarizacion(idCalendarizacion);

			List<Proceso> retorno = [];
			foreach (RelacCalendProc relacion in relaciones) {
				Proceso? proceso = await procesoDao.Obtener(relacion.IdProceso);
				if (proceso != null) {
					retorno.Add(proceso);
				}
			}

			return retorno;
		}

		public async Task<List<Ejecucion>> ObtenerEjecuciones(string idProceso) {
			List<RelacProcEjec> relaciones = await relacProcEjecDao.ObtenerPorProceso(idProceso);

			List<Ejecucion> retorno = [];
			foreach (RelacProcEjec relacion in relaciones) {
				Ejecucion? ejecucion = await ejecucionDao.Obtener(relacion.IdEjecucion);
				if (ejecucion != null) {
					retorno.Add(ejecucion);
				}
			}

			return retorno;
		}

		public async Task<(Proceso, Proceso? procesoCreado)> ObtenerOCrear(string nombre, string arnRol, string arnProceso, string parametros, string idCalendarizacion) {
			Proceso? procesoCreado = null;
			try {
				string idProceso = NombresHelper.GenerarNombreProceso(nombre);

				Proceso? existente = await procesoDao.Obtener(idProceso);
				if (existente == null) {
					existente = await procesoDao.Crear(
						idProceso,
						idCalendarizacion,
						nombre,
						arnRol,
						arnProceso,
						parametros,
						null,
						DateTime.UtcNow
					);
					procesoCreado = existente;
				}

				return (existente!, procesoCreado);
			} catch {
				await ReversarCreado(procesoCreado);
				throw;
			}
		}

		public async Task ReversarCreado(Proceso? procesoCreado) {
			if (procesoCreado != null) {
				await procesoDao.Eliminar(procesoCreado.IdCalendarizacion, procesoCreado.IdProceso);
			}
		}

		public async Task<Proceso?> Eliminar(string idProceso) {
			Proceso? procesoEliminado = null;
			try {
				Proceso? existente = await procesoDao.Obtener(idProceso);
				if (existente != null) {
					await procesoDao.Eliminar(existente.IdCalendarizacion, existente.IdProceso);
					procesoEliminado = existente;
				}

				return procesoEliminado;
			} catch {
				await ReversarEliminado(procesoEliminado);
				throw;
			}
		}

		public async Task ReversarEliminado(Proceso? procesoEliminado) {
			if (procesoEliminado != null) {
				await procesoDao.Crear(
					procesoEliminado.IdProceso,
					procesoEliminado.IdCalendarizacion,
					procesoEliminado.Nombre,
					procesoEliminado.ArnRol,
					procesoEliminado.ArnProceso,
					procesoEliminado.Parametros,
					procesoEliminado.FechaUltimaEjecucionUtc,
					procesoEliminado.FechaCreacionUtc
				);
			}
		}
	}
}
