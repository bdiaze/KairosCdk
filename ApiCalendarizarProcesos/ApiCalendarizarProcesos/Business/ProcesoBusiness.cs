using LibreriaCompartida.Entities;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Repositories;

namespace ApiCalendarizarProcesos.Business {
	public class ProcesoBusiness(ProcesoDao procesoDao) {
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
