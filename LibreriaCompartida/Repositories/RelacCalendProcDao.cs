using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LibreriaCompartida.Entities;
using LibreriaCompartida.Interfaces.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace LibreriaCompartida.Repositories {
	public class RelacCalendProcDao(IAmazonDynamoDB client, IVariableEntornoHelper variableEntornoHelper) {
		private readonly string DYNAMO_TABLE_NAME = variableEntornoHelper.Obtener("DYNAMO_TABLE_NAME");

		public async Task<RelacCalendProc?> Obtener(string idCalendarizacion, string idProceso) {
			GetItemRequest request = new() {
				TableName = DYNAMO_TABLE_NAME,
				Key = RelacCalendProc.GenerarKey(idCalendarizacion, idProceso)
			};

			GetItemResponse response = await client.GetItemAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al obtener la relación entre calendarización y proceso de DynamoDB");
			}

			if (response.Item == null || response.Item.Count == 0) return null;

			return RelacCalendProc.FromItem(response.Item);
		}

		public async Task<List<RelacCalendProc>> ObtenerPorCalendarizacion(string idCalendarizacion) {
			List<RelacCalendProc> retorno = [];

			Dictionary<string, AttributeValue>? lastKey = null;

			do {
				QueryRequest request = new() {
					TableName = DYNAMO_TABLE_NAME,
					KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
					ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
						[":pk"] = new AttributeValue() { S = RelacCalendProc.GenerarPK() },
						[":sk"] = new AttributeValue() { S = RelacCalendProc.GenerarSK(idCalendarizacion) },
					},
					ExclusiveStartKey = lastKey
				};

				QueryResponse response = await client.QueryAsync(request);

				if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
					throw new Exception("Ocurrió un error al obtener las relaciones entre calendarización y procesos de DynamoDB");
				}

				retorno.AddRange(response.Items.Select(i => RelacCalendProc.FromItem(i)));
				lastKey = response.LastEvaluatedKey;
			} while (lastKey?.Count > 0);

			return retorno;
		}
	}
}
