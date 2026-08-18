using Amazon.DynamoDBv2.Model;
using System.Text;

namespace LibreriaCompartida.Entities {
	public class RelacCalendProc : Base {
		public override string PK => GenerarPK();
		public override string SK => GenerarSK(IdCalendarizacion, IdProceso);
		public required string IdCalendarizacion { get; set; }
		public required string IdProceso { get; set; }

		public static string GenerarPK() {
			return "RELAC_CALEND_PROC";
		}

		public static string GenerarSK(string? idCalendarizacion = null, string? idProceso = null) {
			StringBuilder sb = new();
			sb.Append("CAL#");
			if (idCalendarizacion != null) { 
				sb.Append(idCalendarizacion.Replace("#", ""));
				sb.Append("#PROC#");

				if (idProceso != null) {
					sb.Append(idProceso.Replace("#", ""));
					sb.Append('#');
				}
			}
			return sb.ToString();
		}

		public static Dictionary<string, AttributeValue> GenerarKey(string? idCalendarizacion = null, string? idProceso = null) {
			return new Dictionary<string, AttributeValue> {
				{ "PK", new AttributeValue() { S = GenerarPK() } },
				{ "SK", new AttributeValue() { S = GenerarSK(idCalendarizacion, idProceso) } }
			};
		}

		public override Dictionary<string, AttributeValue> ToItem() {
			Dictionary<string, AttributeValue> item = this.Key.Concat(
				new Dictionary<string, AttributeValue>() {
					{ "IdCalendarizacion", new AttributeValue { S = IdCalendarizacion } },
					{ "IdProceso", new AttributeValue { S = IdProceso } },
				}
			).ToDictionary();

			return item;
		}

		public static RelacCalendProc FromItem(Dictionary<string, AttributeValue> item) {
			return new RelacCalendProc() {
				IdCalendarizacion = item["IdCalendarizacion"].S,
				IdProceso = item["IdProceso"].S,
			};
		}
	}
}
