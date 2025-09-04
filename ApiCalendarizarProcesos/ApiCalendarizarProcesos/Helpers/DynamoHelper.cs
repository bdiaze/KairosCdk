using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using System.Globalization;
using System.Text.Json;

namespace ApiCalendarizarProcesos.Helpers {
    public class DynamoHelper(IAmazonDynamoDB client) {
        private readonly Dictionary<string, ITable> tablas = [];

        private ITable ObtenerTabla(string nombreTabla) {
            if (!tablas.TryGetValue(nombreTabla, out ITable? tabla)) {
                tabla = Table.LoadTable(client, nombreTabla);
                tablas.Add(nombreTabla, tabla);
            }
            return tabla;
        }

        public async Task<Document> Crear(string nombreTabla, Document elemento) {
            ITable tabla = ObtenerTabla(nombreTabla);
            return await tabla.PutItemAsync(elemento);
        }

        public async Task<Document> Obtener(string nombreTabla, string key) {
            ITable tabla = ObtenerTabla(nombreTabla);
            return await tabla.GetItemAsync(key);
        }

        public async Task<List<Document>> ObtenerPorIndice(string nombreTabla, string nombreIndice, string nombreCampo, string valorCampo) {
            ITable tabla = ObtenerTabla(nombreTabla);

            ISearch search = tabla.Query(new QueryOperationConfig {
                IndexName = nombreIndice,
                Filter = new QueryFilter(nombreCampo, QueryOperator.Equal, valorCampo),
                ConsistentRead = false
            });

            return await search.GetRemainingAsync();
        }

        public async Task<Document> Eliminar(string nombreTabla, string key) {
            ITable tabla = ObtenerTabla(nombreTabla);
            return await tabla.DeleteItemAsync(key);
        }

        public DynamoDBEntry ToDynamoDbEntry(JsonElement element) {
            switch (element.ValueKind) {
                case JsonValueKind.String:
                    return new Primitive(element.GetString());

                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long l))
                        return new Primitive(l.ToString(CultureInfo.InvariantCulture), true);
                    if (element.TryGetDouble(out double d))
                        return new Primitive(d.ToString(CultureInfo.InvariantCulture), true);
                    return new Primitive(element.GetRawText());

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return new DynamoDBBool(element.GetBoolean());

                case JsonValueKind.Array:
                    DynamoDBList list = new();
                    foreach (JsonElement item in element.EnumerateArray())
                        list.Add(ToDynamoDbEntry(item));
                    return list;

                case JsonValueKind.Object:
                    var doc = new Document();
                    foreach (JsonProperty prop in element.EnumerateObject())
                        doc[prop.Name] = ToDynamoDbEntry(prop.Value);
                    return doc;

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return new DynamoDBNull();

                default:
                    throw new NotSupportedException($"Tipo de JsonElement no soportado: {element.ValueKind}");
            }
        }
    }
}
