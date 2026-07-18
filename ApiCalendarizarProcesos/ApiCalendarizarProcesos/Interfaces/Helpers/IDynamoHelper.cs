using Amazon.DynamoDBv2.Model;

namespace ApiCalendarizarProcesos.Interfaces.Helpers {
	public interface IDynamoHelper {
		public Task<Dictionary<string, object?>?> Insertar(string nombreTabla, Dictionary<string, object?> item);
		public Task<Dictionary<string, object?>?> Obtener(string nombreTabla, Dictionary<string, object?> key);
		public Task<List<Dictionary<string, object?>>> ObtenerPorIndice(string nombreTabla, string nombreIndice, string nombreCampo, string valorCampo);
		public Task Eliminar(string nombreTabla, Dictionary<string, object?> key);
	}
}
