namespace ApiCalendarizarProcesos.Exceptions {
	public enum TipoErrorValidacion {
		AccesoCaducado,
		NoVigente,
		TamannoNoValido,
		TipoNoValido,
		EstadoNoValido,
		NoPertenece,
		RestringidoPorPlan,
		ValorNoValido,
		YaExiste,
		NoExiste
	}

	public class ErrorValidacion(TipoErrorValidacion tipoErrorValidacion, string mensaje, string? mensajeGenerico = null) : Exception(mensaje) {
		public TipoErrorValidacion TipoErrorValidacion => tipoErrorValidacion;
		public string MensajeGenerico => mensajeGenerico ?? base.Message;

		public override string ToString() => mensajeGenerico != null ? $"{tipoErrorValidacion} - {mensajeGenerico} - {base.ToString()}" : $"{tipoErrorValidacion} - {base.ToString()}";
	}
}
