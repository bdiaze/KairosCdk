using Amazon.DynamoDBv2.Model;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace LibreriaCompartida.Entities {
	public class Calendarizacion : Base {
		public override string PK => GenerarPK();
		public override string SK => GenerarSK(IdCalendarizacion);
		public required string IdCalendarizacion { get; set; }
		public required string Nombre { get; set; }
		public required string Descripcion { get; set; }
		public required string Grupo { get; set; }
		public required string Arn { get; set; }
		public required int CantProcesos { get; set; }
		public string? Cron { get; set; }
		public int? FrecuenciaDias { get; set; }
		public DateTime? InicioEjecucionUtc { get; set; }
		public required DateTime FechaCreacionUtc { get; set; }

		public static string GenerarPK() {
			return "CALENDARIZACION";
		}

		public static string GenerarSK(string? idCalendarizacion = null) {
			StringBuilder sb = new();
			sb.Append("CAL#");
			if (idCalendarizacion != null) {
				sb.Append(idCalendarizacion.Replace("#", ""));
				sb.Append('#');					
			}
			return sb.ToString();
		}

		public static Dictionary<string, AttributeValue> GenerarKey(string? idCalendarizacion = null) {
			return new Dictionary<string, AttributeValue> {
				{ "PK", new AttributeValue() { S = GenerarPK() } },
				{ "SK", new AttributeValue() { S = GenerarSK(idCalendarizacion) } }
			};
		}

		public override Dictionary<string, AttributeValue> ToItem() {
			Dictionary<string, AttributeValue> item = this.Key.Concat(
				new Dictionary<string, AttributeValue>() {
					{ "IdCalendarizacion", new AttributeValue { S = IdCalendarizacion } },
					{ "Nombre", new AttributeValue { S = Nombre } },
					{ "Descripcion",  new AttributeValue { S = Descripcion } },
					{ "Grupo", new AttributeValue { S = Grupo } },
					{ "Arn", new AttributeValue { S = Arn } },
					{ "CantProcesos", new AttributeValue { N = CantProcesos.ToString(CultureInfo.InvariantCulture) } },
					{ "Cron", new AttributeValue { NULL = true } },
					{ "FrecuenciaDias", new AttributeValue { NULL = true } },
					{ "InicioEjecucionUtc", new AttributeValue { NULL = true } },
					{ "FechaCreacionUtc", new AttributeValue { S = $"{FechaCreacionUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" } },
				}
			).ToDictionary();

			if (Cron != null) item["Cron"] = new AttributeValue { S = Cron };
			if (FrecuenciaDias != null) item["FrecuenciaDias"] = new AttributeValue { N = FrecuenciaDias.Value.ToString(CultureInfo.InvariantCulture) };
			if (InicioEjecucionUtc != null) item["InicioEjecucionUtc"] = new AttributeValue { S = $"{InicioEjecucionUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };

			return item;
		}

		public static Calendarizacion FromItem(Dictionary<string, AttributeValue> item) {
			return new Calendarizacion() { 
				IdCalendarizacion = item["IdCalendarizacion"].S,
				Nombre = item["Nombre"].S,
				Descripcion = item["Descripcion"].S,
				Grupo = item["Grupo"].S,
				Arn = item["Arn"].S,
				CantProcesos = int.Parse(item["CantProcesos"].N, CultureInfo.InvariantCulture),
				Cron = item["Cron"].NULL != null && item["Cron"].NULL!.Value ? null : item["Cron"].S,
				FrecuenciaDias = item["FrecuenciaDias"].NULL != null && item["FrecuenciaDias"].NULL!.Value ? null : int.Parse(item["FrecuenciaDias"].N, CultureInfo.InvariantCulture),
				InicioEjecucionUtc = item["InicioEjecucionUtc"].NULL != null && item["InicioEjecucionUtc"].NULL!.Value ? null : DateTime.ParseExact(item["InicioEjecucionUtc"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
				FechaCreacionUtc = DateTime.ParseExact(item["FechaCreacionUtc"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
			};
		}
	}
}
