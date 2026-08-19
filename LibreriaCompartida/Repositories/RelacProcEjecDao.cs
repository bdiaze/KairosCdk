using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LibreriaCompartida.Entities;
using LibreriaCompartida.Interfaces.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace LibreriaCompartida.Repositories {
	public class RelacProcEjecDao(IAmazonDynamoDB client, IVariableEntornoHelper variableEntornoHelper) {
		private readonly string DYNAMO_TABLE_NAME = variableEntornoHelper.Obtener("DYNAMO_TABLE_NAME");

		public async Task<List<RelacProcEjec>> ObtenerPorProceso(string idProceso) {
			List<RelacProcEjec> retorno = [];

			Dictionary<string, AttributeValue>? lastKey = null;

			do {
				QueryRequest request = new() {
					TableName = DYNAMO_TABLE_NAME,
					KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
					ExpressionAttributeValues = RelacProcEjec.GenerarKey(idProceso),
					ExclusiveStartKey = lastKey
				};

				QueryResponse response = await client.QueryAsync(request);

				if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
					throw new Exception("Ocurrió un error al obtener todas las relaciones de un proceso con sus ejecuciones de DynamoDB");
				}

				retorno.AddRange(response.Items.Select(i => RelacProcEjec.FromItem(i)));
				lastKey = response.LastEvaluatedKey;
			} while (lastKey?.Count > 0);

			return retorno;
		}
	}
}
