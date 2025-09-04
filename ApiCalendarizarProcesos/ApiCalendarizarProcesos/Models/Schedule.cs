namespace ApiCalendarizarProcesos.Models {
    public class Schedule {
        public required string Nombre { get; set; }
        public required string Descripcion { get; set; }
        public required string Grupo { get; set; }
        public required string Cron { get; set; }
        public string? Arn { get; set; }
    }
}
