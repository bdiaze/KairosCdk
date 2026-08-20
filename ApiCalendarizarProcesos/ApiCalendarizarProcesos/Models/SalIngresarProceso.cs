using System.Text.Json.Serialization;

namespace ApiCalendarizarProcesos.Models {
	public class SalIngresarProceso {
		[JsonPropertyName("IdProceso")]
		public required string IdProceso { get; set; }

		[JsonPropertyName("IdCalendarizacion")]
		public required string IdCalendarizacion { get; set; }

		[JsonPropertyName("Nombre")]
		public required string Nombre { get; set; }

		[JsonPropertyName("ArnRol")]
		public required string ArnRol { get; set; }

		[JsonPropertyName("ArnProceso")]
		public required string ArnProceso { get; set; }

		[JsonPropertyName("Parametros")]
		public required string Parametros { get; set; }

		[JsonPropertyName("Cron")]
		public required string? Cron { get; set; }

		[JsonPropertyName("FrecuenciaDias")]
		public required int? FrecuenciaDias { get; set; }

		[JsonPropertyName("InicioEjecucionUtc")]
		public required DateTime? InicioEjecucionUtc { get; set; }
	}
}
