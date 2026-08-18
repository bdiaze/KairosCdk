namespace ApiCalendarizarProcesos.Models {
    public class Schedule {
        public required string Nombre { get; set; }
        public required string Descripcion { get; set; }
        public required string Grupo { get; set; }
        public string? Cron { get; set; }
		public int? FrecuenciaDias { get; set; }
		public DateTime? InicioEjecucionUtc { get; set; }
		public required string Arn { get; set; }
		public required string TargetArn { get; set; }
		public required string TargetRoleArn { get; set; }
        public required string TargetInput { get; set; }
        public required string TargetDlqArn { get; set; }
    }
}
