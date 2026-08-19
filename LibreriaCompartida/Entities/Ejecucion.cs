using Amazon.DynamoDBv2.Model;
using LibreriaCompartida.Enums;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace LibreriaCompartida.Entities {
	public class Ejecucion : Base {
		[JsonIgnore]
		public override string PK => GenerarPK();

		[JsonIgnore]
		public override string SK => GenerarSK(IdEjecucion);

		[JsonPropertyName("IdEjecucion")]
		public required string IdEjecucion { get; set; }

		[JsonPropertyName("IdProceso")]
		public required string IdProceso { get; set; }

		[JsonPropertyName("FechaEncolamientoUtc")]
		public required DateTime FechaEncolamientoUtc { get; set; }

		[JsonPropertyName("FechaEjecucionUtc")]
		public required DateTime? FechaEjecucionUtc { get; set; }

		[JsonPropertyName("Estado")]
		public required EstadoEjecucion Estado { get; set; }

		[JsonPropertyName("Observacion")]
		public required string? Observacion { get; set; }

		[JsonIgnore]
		public required long TTL { get; set; }

		public static string GenerarPK() {
			return "EJECUCION";
		}

		public static string GenerarSK(string? idEjecucion = null) {
			StringBuilder sb = new();
			sb.Append("EJEC#");
			if (idEjecucion != null) {
				sb.Append(idEjecucion.Replace("#", ""));
				sb.Append('#');
			}
			return sb.ToString();
		}

		public static Dictionary<string, AttributeValue> GenerarKey(string? idEjecucion = null) {
			return new Dictionary<string, AttributeValue> {
				{ "PK", new AttributeValue() { S = GenerarPK() } },
				{ "SK", new AttributeValue() { S = GenerarSK(idEjecucion) } }
			};
		}

		public override Dictionary<string, AttributeValue> ToItem() {
			Dictionary<string, AttributeValue> item = this.Key.Concat(
				new Dictionary<string, AttributeValue>() {
					{ "IdEjecucion", new AttributeValue { S = IdEjecucion } },
					{ "IdProceso", new AttributeValue { S = IdProceso } },
					{ "FechaEncolamientoUtc", new AttributeValue { S = $"{FechaEncolamientoUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" } },
					{ "FechaEjecucionUtc", new AttributeValue { NULL = true } },
					{ "Estado", new AttributeValue { S = Estado.ToString() } },
					{ "Observacion", new AttributeValue { NULL = true } },
					{ "TTL",  new AttributeValue { N = TTL.ToString(CultureInfo.InvariantCulture) } },
				}
			).ToDictionary();

			if (FechaEjecucionUtc != null) item["FechaEjecucionUtc"] = new AttributeValue { S = $"{FechaEjecucionUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };
			if (Observacion != null) item["Observacion"] = new AttributeValue { S = Observacion };

			return item;
		}

		public static Ejecucion FromItem(Dictionary<string, AttributeValue> item) {
			return new Ejecucion() {
				IdEjecucion = item["IdEjecucion"].S,
				IdProceso = item["IdProceso"].S,
				FechaEncolamientoUtc = DateTime.ParseExact(item["FechaEncolamientoUtc"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
				FechaEjecucionUtc = item["FechaEjecucionUtc"].NULL != null && item["FechaEjecucionUtc"].NULL!.Value ? null : DateTime.ParseExact(item["FechaEjecucionUtc"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
				Estado = Enum.Parse<EstadoEjecucion>(item["Estado"].S),
				Observacion = item["Observacion"].NULL != null && item["Observacion"].NULL!.Value ? null : item["Observacion"].S,
				TTL = int.Parse(item["TTL"].N, CultureInfo.InvariantCulture),
			};
		}
	}
}
