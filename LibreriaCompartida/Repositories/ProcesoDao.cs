using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LibreriaCompartida.Entities;
using LibreriaCompartida.Interfaces.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace LibreriaCompartida.Repositories {
	public class ProcesoDao(IAmazonDynamoDB client, IVariableEntornoHelper variableEntornoHelper) {
		private readonly string DYNAMO_TABLE_NAME = variableEntornoHelper.Obtener("DYNAMO_TABLE_NAME");

		public async Task<Proceso> Crear(string idProceso, string idCalendarizacion, string nombre, string arnRol, string arnProceso, string parametros, DateTime? fechaUltimaEjecucionUtc, DateTime fechaCreacionUtc) {
			Proceso item = new() {
				IdProceso = idProceso,
				IdCalendarizacion = idCalendarizacion,				
				Nombre = nombre,
				ArnRol = arnRol,
				ArnProceso = arnProceso,
				Parametros = parametros,
				FechaUltimaEjecucionUtc = fechaUltimaEjecucionUtc,
				FechaCreacionUtc = fechaCreacionUtc
			};

			RelacCalendProc relacion = new() {
				IdCalendarizacion = item.IdCalendarizacion,
				IdProceso = item.IdProceso
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
						Put = new Put {
							TableName = DYNAMO_TABLE_NAME,
							Item = relacion.ToItem(),
							ConditionExpression = "attribute_not_exists(PK)"
						}
					},
					new TransactWriteItem {
						Update = new Update {
							TableName = DYNAMO_TABLE_NAME,
							Key = Calendarizacion.GenerarKey(idCalendarizacion),
							UpdateExpression = "ADD CantProcesos :uno",
							ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
								[":uno"] = new AttributeValue { N = "1" }
							},
							ConditionExpression = "attribute_exists(PK)",
						}
					}
				]
			};

			TransactWriteItemsResponse response = await client.TransactWriteItemsAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new HttpRequestException("Ocurrió un error al insertar el proceso en DynamoDB");
			}

			return item;
		}

		public async Task Eliminar(string idCalendarizacion, string idProceso) {
			TransactWriteItemsRequest request = new() {
				TransactItems = [
					new TransactWriteItem {
						Delete = new Delete {
							TableName = DYNAMO_TABLE_NAME,
							Key = RelacCalendProc.GenerarKey(idCalendarizacion, idProceso),
							ConditionExpression = "attribute_exists(PK)"
						}
					},
					new TransactWriteItem {
						Delete = new Delete {
							TableName = DYNAMO_TABLE_NAME,
							Key = Proceso.GenerarKey(idProceso),
							ConditionExpression = "attribute_exists(PK)"
						}
					},
					new TransactWriteItem {
						Update = new Update {
							TableName = DYNAMO_TABLE_NAME,
							Key = Calendarizacion.GenerarKey(idCalendarizacion),
							UpdateExpression = "ADD CantProcesos :menosUno",
							ConditionExpression = "attribute_exists(PK) AND CantProcesos > :cero",
							ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
								[":menosUno"] = new AttributeValue { N = "-1" },
								[":cero"] = new AttributeValue { N = "0" }
							},
						}
					}
				]
			};

			TransactWriteItemsResponse response = await client.TransactWriteItemsAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al eliminar el proceso en DynamoDB");
			}
		}

		public async Task<Proceso?> Obtener(string idProceso) {
			GetItemRequest request = new() {
				TableName = DYNAMO_TABLE_NAME,
				Key = Proceso.GenerarKey(idProceso)
			};

			GetItemResponse response = await client.GetItemAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al obtener el proceso de DynamoDB");
			}

			if (response.Item == null || response.Item.Count == 0) return null;

			return Proceso.FromItem(response.Item);
		}

		public async Task<List<Proceso>> ObtenerTodos() {
			List<Proceso> retorno = [];

			Dictionary<string, AttributeValue>? lastKey = null;

			do {
				QueryRequest request = new() {
					TableName = DYNAMO_TABLE_NAME,
					KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
					ExpressionAttributeValues = Proceso.GenerarKey(),
					ExclusiveStartKey = lastKey
				};

				QueryResponse response = await client.QueryAsync(request);

				if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
					throw new Exception("Ocurrió un error al obtener todos los procesos de DynamoDB");
				}

				retorno.AddRange(response.Items.Select(i => Proceso.FromItem(i)));
				lastKey = response.LastEvaluatedKey;
			} while (lastKey?.Count > 0);

			return retorno;
		}
	}
}
