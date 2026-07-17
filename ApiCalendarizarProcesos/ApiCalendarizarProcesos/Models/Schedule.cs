namespace ApiCalendarizarProcesos.Models {
    public class Schedule {
        public required string Nombre { get; set; }
        public required string Descripcion { get; set; }
        public required string Grupo { get; set; }
        public string? Cron { get; set; }
		public int? FrecuenciaDias { get; set; }
		public DateTime? InicioEjecucionUtc { get; set; }
		public string? Arn { get; set; }
    }
}
