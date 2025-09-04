using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LambdaDispatcher.Helpers {
    public class DynamoHelper(IAmazonDynamoDB client) {
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

        public static string ToJson(Dictionary<string, AttributeValue> item) {
            Dictionary<string, object?> dict = [];

            foreach (KeyValuePair<string, AttributeValue> kvp in item) {
                dict[kvp.Key] = ConvertAttributeValue(kvp.Value);
            }

            return JsonSerializer.Serialize(dict);
        }

        private static object? ConvertAttributeValue(AttributeValue av) {
            if (av.S != null) return av.S;

            if (av.N != null) {
                if (long.TryParse(av.N, out var intValue)) {
                    return intValue;
                }
                if (double.TryParse(av.N, out var doubleValue)) {
                    return doubleValue;
                }
                return av.N;
            }

            if (av.BOOL != null) return av.BOOL.Value;

            if (av.L != null) {
                List<object?> list = [];
                foreach (AttributeValue item in av.L) {
                    list.Add(ConvertAttributeValue(item));
                }
                return list;
            }

            if (av.M != null) {
                Dictionary<string, object?> map = [];
                foreach (KeyValuePair<string, AttributeValue> kv in av.M) {
                    map[kv.Key] = ConvertAttributeValue(kv.Value);
                }
                return map;
            }

            return null;
        }
    }
}
