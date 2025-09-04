using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LambdaDispatcher.Helpers {
    public class DynamoHelper(IAmazonDynamoDB client) {
        private readonly Dictionary<string, Table> tablas = [];

        private Table ObtenerTabla(string nombreTabla) {
            if (!tablas.TryGetValue(nombreTabla, out Table? tabla)) {
                tabla = new TableBuilder(client, nombreTabla).Build();
                tablas.Add(nombreTabla, tabla);
            }
            return tabla;
        }

        public async Task<List<Document>> ObtenerPorIndice(string nombreTabla, string nombreIndice, string nombreCampo, string valorCampo) {
            Table tabla = ObtenerTabla(nombreTabla);

            ISearch search = tabla.Query(new QueryOperationConfig {
                IndexName = nombreIndice,
                Filter = new QueryFilter(nombreCampo, QueryOperator.Equal, valorCampo),
                ConsistentRead = false
            });

            return await search.GetRemainingAsync();
        }
    }
}
