using Amazon.DynamoDBv2.Model;
using LibreriaCompartida.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LibreriaCompartida.Entities {
	public class RelacProcEjec : Base {
		public override string PK => GenerarPK();
		public override string SK => GenerarSK(IdProceso, FechaEncolamientoUtc, IdEjecucion);
		public required string IdProceso { get; set; }
		public required string IdEjecucion { get; set; }
		public required DateTime FechaEncolamientoUtc { get; set; }
		public required long TTL { get; set; }

		public static string GenerarPK() {
			return "RELAC_PROC_EJEC";
		}

		public static string GenerarSK(string? idProceso = null, DateTime? fechaEncolamientoUtc = null, string? idEjecucion = null) {
			StringBuilder sb = new();
			sb.Append("PROC#");
			if (idProceso != null) {
				sb.Append(idProceso.Replace("#", ""));
				sb.Append("#FECHA#");
				
				if (fechaEncolamientoUtc != null) {
					sb.Append(fechaEncolamientoUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture).Replace("#", ""));
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
					{ "IdProceso", new AttributeValue { S = IdProceso } },
					{ "IdEjecucion", new AttributeValue { S = IdEjecucion } },
					{ "FechaEncolamientoUtc", new AttributeValue { S = $"{FechaEncolamientoUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" } },
					{ "TTL",  new AttributeValue { N = TTL.ToString(CultureInfo.InvariantCulture) } },
				}
			).ToDictionary();

			return item;
		}

		public static RelacProcEjec FromItem(Dictionary<string, AttributeValue> item) {
			return new RelacProcEjec() {
				IdProceso = item["IdProceso"].S,
				IdEjecucion = item["IdEjecucion"].S,
				FechaEncolamientoUtc = DateTime.ParseExact(item["FechaEncolamientoUtc"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
				TTL = int.Parse(item["TTL"].N, CultureInfo.InvariantCulture),
			};
		}
	}
}
