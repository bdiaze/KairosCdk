using ApiCalendarizarProcesos.Models;

namespace ApiCalendarizarProcesos.Interfaces.Helpers {
	public interface ISchedulerHelper {
		public Task<Schedule?> Crear(string nombre, string descripcion, string grupo, string? cron, int? frecuenciaDias, DateTime? inicioEjecucionUtc, string roleArn, string dlqArn, string targetArn, string targetInput);
		public Task Eliminar(string nombre, string grupo);
		public Task<Schedule?> Obtener(string nombre, string grupo);
	}
}
