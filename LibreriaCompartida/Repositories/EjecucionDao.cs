using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LibreriaCompartida.Entities;
using LibreriaCompartida.Enums;
using LibreriaCompartida.Interfaces.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LibreriaCompartida.Repositories {
	public class EjecucionDao(IAmazonDynamoDB client, IVariableEntornoHelper variableEntornoHelper) {
		private readonly string DYNAMO_TABLE_NAME = variableEntornoHelper.Obtener("DYNAMO_TABLE_NAME");

		public async Task<Ejecucion> Crear(string idEjecucion, string idProceso, DateTime fechaEjecucionUtc, EstadoEjecucion estado, long ttl) {
			Ejecucion item = new() {
				IdEjecucion = idEjecucion,
				IdProceso = idProceso,
				FechaEjecucionUtc = fechaEjecucionUtc,
				Estado = estado,
				TTL = ttl
			};

			TransactWriteItemsRequest request = new() {
				TransactItems = [
					new TransactWriteItem {
						Put = new Put {
							TableName = DYNAMO_TABLE_NAME,
							Item = item.ToItem(),
							ConditionExpression = "attribute_not_exists(PK)"
						}
					},
					new TransactWriteItem {
						Update = new Update {
							TableName = DYNAMO_TABLE_NAME,
							Key = Proceso.GenerarKey(idProceso),
							UpdateExpression = "SET FechaUltimaEjecucionUtc = :ultimaEjecucion",
							ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
								[":ultimaEjecucion"] = new AttributeValue { S = fechaEjecucionUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) }
							},
							ConditionExpression = "attribute_exists(PK)",
						}
					}
				]
			};

			TransactWriteItemsResponse response = await client.TransactWriteItemsAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new HttpRequestException("Ocurrió un error al insertar la ejecución en DynamoDB");
			}

			return item;
		}

		public async Task<List<Ejecucion>> ObtenerPorProceso(string idProceso) {
			List<Ejecucion> retorno = [];

			Dictionary<string, AttributeValue>? lastKey = null;

			do {
				QueryRequest request = new() {
					TableName = DYNAMO_TABLE_NAME,
					KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
					ExpressionAttributeValues = Ejecucion.GenerarKey(idProceso),
					ExclusiveStartKey = lastKey
				};

				QueryResponse response = await client.QueryAsync(request);

				if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
					throw new Exception("Ocurrió un error al obtener todas las ejecución de un proceso de DynamoDB");
				}

				retorno.AddRange(response.Items.Select(i => Ejecucion.FromItem(i)));
				lastKey = response.LastEvaluatedKey;
			} while (lastKey?.Count > 0);

			return retorno;
		}
	}
}
