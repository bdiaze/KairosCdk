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

		public async Task<Ejecucion> Crear(string idEjecucion, string idProceso, DateTime fechaEncolamientoUtc, EstadoEjecucion estado, string? observacion, long ttl) {
			Ejecucion item = new() {
				IdEjecucion = idEjecucion,
				IdProceso = idProceso,
				FechaEncolamientoUtc = fechaEncolamientoUtc,
				FechaEjecucionUtc = null,
				Observacion = observacion,
				Estado = estado,
				TTL = ttl
			};

			RelacProcEjec relacion = new() { 
				IdProceso = idProceso,
				IdEjecucion = idEjecucion,
				FechaEncolamientoUtc = fechaEncolamientoUtc,
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
						Put = new Put {
							TableName = DYNAMO_TABLE_NAME,
							Item = relacion.ToItem(),
							ConditionExpression = "attribute_not_exists(PK)"
						}
					},
					new TransactWriteItem {
						Update = new Update {
							TableName = DYNAMO_TABLE_NAME,
							Key = Proceso.GenerarKey(idProceso),
							UpdateExpression = "SET FechaUltimaEjecucionUtc = :ultimaEjecucion",
							ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
								[":ultimaEjecucion"] = new AttributeValue { S = fechaEncolamientoUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) }
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

		public async Task RegistrarFechaEjecucion(string idEjecucion, DateTime fechaEjecucionUtc, EstadoEjecucion estado, string? observacion) {
			UpdateItemRequest request = new() {
				TableName = DYNAMO_TABLE_NAME,
				Key = Ejecucion.GenerarKey(idEjecucion),
				UpdateExpression = "SET FechaEjecucionUtc = :fechaEjecucionUtc, Estado = :estado, Observacion = :observacion",
				ConditionExpression = "attribute_exists(PK) AND (attribute_not_exists(FechaEjecucionUtc) OR FechaEjecucionUtc = :null)",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
					[":fechaEjecucionUtc"] = new AttributeValue { S = fechaEjecucionUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) },
					[":estado"] = new AttributeValue { S = estado.ToString() },
					[":observacion"] = observacion == null 
						? new() { NULL = true }
						: new() { S = observacion },
					[":null"] = new AttributeValue { NULL = true },
				},
			};

			UpdateItemResponse response = await client.UpdateItemAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new HttpRequestException("Ocurrió un error al registrar fecha de ejecución en DynamoDB");
			}
		}

		public async Task<Ejecucion?> Obtener(string idEjecucion) {
			GetItemRequest request = new() {
				TableName = DYNAMO_TABLE_NAME,
				Key = Ejecucion.GenerarKey(idEjecucion)
			};

			GetItemResponse response = await client.GetItemAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al obtener la ejecución de DynamoDB");
			}

			if (response.Item == null || response.Item.Count == 0) return null;

			return Ejecucion.FromItem(response.Item);
		}
	}
}
