namespace ApiCalendarizarProcesos.Interfaces.Helpers {
	public interface IParameterStoreHelper {
		public Task<string> ObtenerParametro(string parameterArn);
	}
}
