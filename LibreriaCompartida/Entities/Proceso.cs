using Amazon.DynamoDBv2.Model;
using System.Globalization;
using System.Text;

namespace LibreriaCompartida.Entities {
	public class Proceso : Base {
		public override string PK => GenerarPK();
		public override string SK => GenerarSK(IdProceso);
		public required string IdProceso { get; set; }
		public required string IdCalendarizacion { get; set; }
		public required string Nombre { get; set; }
		public required string ArnRol { get; set; }
		public required string ArnProceso { get; set; }
		public required string Parametros { get; set; }
		public DateTime? FechaUltimaEjecucionUtc { get; set; }
		public required DateTime FechaCreacionUtc { get; set; }

		public static string GenerarPK() {
			return "PROCESO";
		}

		public static string GenerarSK(string? idProceso = null) {
			StringBuilder sb = new();
			sb.Append("PROC#");
			if (idProceso != null) {
				sb.Append(idProceso.Replace("#", ""));
				sb.Append('#');
			}
			return sb.ToString();
		}

		public static Dictionary<string, AttributeValue> GenerarKey(string? idProceso = null) {
			return new Dictionary<string, AttributeValue> {
				{ "PK", new AttributeValue() { S = GenerarPK() } },
				{ "SK", new AttributeValue() { S = GenerarSK(idProceso) } }
			};
		}

		public override Dictionary<string, AttributeValue> ToItem() {
			Dictionary<string, AttributeValue> item = this.Key.Concat(
				new Dictionary<string, AttributeValue>() {
					{ "IdProceso", new AttributeValue { S = IdProceso } },
					{ "IdCalendarizacion", new AttributeValue { S = IdCalendarizacion } },
					{ "Nombre",  new AttributeValue { S = Nombre } },
					{ "ArnRol", new AttributeValue { S = ArnRol } },
					{ "ArnProceso", new AttributeValue { S = ArnProceso } },
					{ "Parametros", new AttributeValue { S = Parametros } },
					{ "FechaUltimaEjecucionUtc", new AttributeValue { NULL = true } },
					{ "FechaCreacionUtc", new AttributeValue { S = $"{FechaCreacionUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" } },
				}
			).ToDictionary();

			if (FechaUltimaEjecucionUtc != null) item["FechaUltimaEjecucionUtc"] = new AttributeValue { S = $"{FechaUltimaEjecucionUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };

			return item;
		}

		public static Proceso FromItem(Dictionary<string, AttributeValue> item) {
			return new Proceso() {
				IdProceso = item["IdProceso"].S,
				IdCalendarizacion = item["IdCalendarizacion"].S,
				Nombre = item["Nombre"].S,
				ArnRol = item["ArnRol"].S,
				ArnProceso = item["ArnProceso"].S,
				Parametros = item["Parametros"].S,
				FechaUltimaEjecucionUtc = item["FechaUltimaEjecucionUtc"].NULL != null && item["FechaUltimaEjecucionUtc"].NULL!.Value ? null : DateTime.ParseExact(item["FechaUltimaEjecucionUtc"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
				FechaCreacionUtc = DateTime.ParseExact(item["FechaCreacionUtc"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
			};
		}
	}
}
