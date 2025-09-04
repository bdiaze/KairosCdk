using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System.Globalization;
using System.Text.Json;

namespace ApiCalendarizarProcesos.Helpers {
    public class DynamoHelper(IAmazonDynamoDB client) {

        public async Task<Dictionary<string, AttributeValue>> Insertar(string nombreTabla, Dictionary<string, AttributeValue> item) {
            PutItemRequest request = new() {
                TableName = nombreTabla,
                Item = item
            };

            PutItemResponse response = await client.PutItemAsync(request);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
                throw new Exception("Ocurrió un error al insertar el ítem de Dynamo");
            }

            return item;
        }

        public async Task<Dictionary<string, AttributeValue>> Obtener(string nombreTabla, Dictionary<string, AttributeValue> key) {
            GetItemRequest request = new() { 
                TableName = nombreTabla,
                Key = key
            };

            GetItemResponse response = await client.GetItemAsync(request);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
                throw new Exception("Ocurrió un error al obtener el ítem de Dynamo");
            }

            return response.Item;
        }

        public async Task<List<Dictionary<string, AttributeValue>>> ObtenerPorIndice(string nombreTabla, string nombreIndice, string nombreCampo, string valorCampo) {
            QueryRequest request = new() {
                TableName = nombreTabla,
                IndexName = nombreIndice,
                KeyConditionExpression = $"{nombreCampo} = :key",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    [":key"] = new AttributeValue { S = valorCampo }
                }
            };

            QueryResponse response = await client.QueryAsync(request);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
                throw new Exception("Ocurrió un error al obtener por índice los ítems de Dynamo");
            }

            return response.Items;
        }

        public async Task Eliminar(string nombreTabla, Dictionary<string, AttributeValue> key) {
            DeleteItemRequest request = new() {
                TableName = nombreTabla,
                Key = key
            };

            DeleteItemResponse response = await client.DeleteItemAsync(request);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
                throw new Exception("Ocurrió un error al eliminar el ítem de Dynamo");
            }
        }
    }
}
