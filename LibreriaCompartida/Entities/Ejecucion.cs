using Amazon.DynamoDBv2.Model;
using LibreriaCompartida.Enums;
using System.Globalization;
using System.Text;

namespace LibreriaCompartida.Entities {
	public class Ejecucion : Base {
		public override string PK => GenerarPK();
		public override string SK => GenerarSK(IdProceso, FechaEjecucionUtc, IdEjecucion);
		public required string IdEjecucion { get; set; }
		public required string IdProceso { get; set; }
		public required DateTime FechaEjecucionUtc { get; set; }
		public required EstadoEjecucion Estado { get; set; }
		public required long TTL { get; set; }

		public static string GenerarPK() {
			return "EJECUCION";
		}

		public static string GenerarSK(string? idProceso = null, DateTime? fechaEjecucionUtc = null, string? idEjecucion = null) {
			StringBuilder sb = new();
			sb.Append("PROC#");
			if (idProceso != null) {
				sb.Append(idProceso.Replace("#", ""));
				sb.Append("#FECHA#");
				if (fechaEjecucionUtc != null) {
					sb.Append(fechaEjecucionUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture).Replace("#", ""));
					sb.Append("#EJEC#");
					if (idEjecucion != null) {
						sb.Append(idEjecucion.Replace("#", ""));
						sb.Append('#');
					}
				}
			}
			return sb.ToString();
		}

		public static Dictionary<string, AttributeValue> GenerarKey(string? idProceso = null, DateTime? fechaEjecucionUtc = null, string? idEjecucion = null) {
			return new Dictionary<string, AttributeValue> {
				{ "PK", new AttributeValue() { S = GenerarPK() } },
				{ "SK", new AttributeValue() { S = GenerarSK(idProceso, fechaEjecucionUtc, idEjecucion) } }
			};
		}

		public override Dictionary<string, AttributeValue> ToItem() {
			Dictionary<string, AttributeValue> item = this.Key.Concat(
				new Dictionary<string, AttributeValue>() {
					{ "IdEjecucion", new AttributeValue { S = IdEjecucion } },
					{ "IdProceso", new AttributeValue { S = IdProceso } },
					{ "FechaEjecucionUtc", new AttributeValue { S = $"{FechaEjecucionUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" } },
					{ "Estado", new AttributeValue { S = Estado.ToString() } },
					{ "TTL",  new AttributeValue { N = TTL.ToString(CultureInfo.InvariantCulture) } },
				}
			).ToDictionary();

			return item;
		}

		public static Ejecucion FromItem(Dictionary<string, AttributeValue> item) {
			return new Ejecucion() {
				IdEjecucion = item["IdEjecucion"].S,
				IdProceso = item["IdProceso"].S,
				FechaEjecucionUtc = DateTime.ParseExact(item["FechaEjecucionUtc"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
				Estado = Enum.Parse<EstadoEjecucion>(item["Estado"].S),
				TTL = int.Parse(item["TTL"].N, CultureInfo.InvariantCulture),
			};
		}
	}
}
