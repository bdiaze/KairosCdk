using System.Text.Json;

namespace ApiCalendarizarProcesos.Models {
    public class EntIngresarProceso {
        public required string Nombre { get; set; }
        public string? Cron { get; set; }
        public int? FrecuenciaDias { get; set; }
		public DateTime? InicioEjecucionUtc { get; set; }
        public required string ArnRol { get; set; }
        public required string ArnProceso { get; set; }
        public required string Parametros { get; set; }
        public required bool Habilitado { get; set; }
    }
}
