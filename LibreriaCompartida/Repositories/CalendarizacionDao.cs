using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LibreriaCompartida.Entities;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Interfaces.Helpers;

namespace LibreriaCompartida.Repositories {
	public class CalendarizacionDao(IAmazonDynamoDB client, IVariableEntornoHelper variableEntornoHelper) {
		private readonly string DYNAMO_TABLE_NAME = variableEntornoHelper.Obtener("DYNAMO_TABLE_NAME");

		public async Task<Calendarizacion> Crear(string idCalendarizacion, string nombre, string descripcion, string grupo, string arn, string? cron, int? frecuenciaDias, DateTime? inicioEjecucionUtc, DateTime fechaCreacionUtc) {
			Calendarizacion item = new() {
				IdCalendarizacion = idCalendarizacion,
				Nombre = nombre,
				Descripcion = descripcion,
				Grupo = grupo,
				Arn = arn,
				CantProcesos = 0,
				Cron = cron,
				FrecuenciaDias = frecuenciaDias,
				InicioEjecucionUtc = inicioEjecucionUtc,
				FechaCreacionUtc = fechaCreacionUtc
			};

			PutItemRequest request = new() {
				TableName = DYNAMO_TABLE_NAME,
				Item = item.ToItem(),
				ConditionExpression = "attribute_not_exists(PK)"
			};

			PutItemResponse response = await client.PutItemAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new HttpRequestException("Ocurrió un error al insertar la calendarización en DynamoDB");
			}

			return item;
		}
				
		public async Task Eliminar(string idCalendarizacion) {
			DeleteItemRequest request = new() {
				TableName = DYNAMO_TABLE_NAME,
				Key = Calendarizacion.GenerarKey(idCalendarizacion),
				ConditionExpression = "CantProcesos = :cero",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
					[":cero"] = new AttributeValue { N = "0" }
				}
			};

			DeleteItemResponse response = await client.DeleteItemAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al eliminar la calendarización en DynamoDB");
			}
		}

		public async Task<Calendarizacion?> Obtener(string idCalendarizacion) {
			GetItemRequest request = new() {
				TableName = DYNAMO_TABLE_NAME,
				Key = Calendarizacion.GenerarKey(idCalendarizacion)
			};

			GetItemResponse response = await client.GetItemAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al obtener la calendarización de DynamoDB");
			}

			if (response.Item == null || response.Item.Count == 0) return null;

			return Calendarizacion.FromItem(response.Item);
		}

		public async Task<List<Calendarizacion>> ObtenerTodas() {
			List<Calendarizacion> retorno = [];

			Dictionary<string, AttributeValue>? lastKey = null;

			do {
				QueryRequest request = new() {
					TableName = DYNAMO_TABLE_NAME,
					KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
					ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
						[":pk"] = new AttributeValue() { S = Calendarizacion.GenerarPK() },
						[":sk"] = new AttributeValue() { S = Calendarizacion.GenerarSK() },
					},
					ExclusiveStartKey = lastKey
				};

				QueryResponse response = await client.QueryAsync(request);

				if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
					throw new Exception("Ocurrió un error al obtener todas las calendarizaciones de DynamoDB");
				}

				retorno.AddRange(response.Items.Select(i => Calendarizacion.FromItem(i)));
				lastKey = response.LastEvaluatedKey;
			} while (lastKey?.Count > 0);

			return retorno;
		}
	}
}
