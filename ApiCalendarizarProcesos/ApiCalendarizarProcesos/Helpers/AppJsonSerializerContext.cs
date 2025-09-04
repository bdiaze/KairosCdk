using Amazon.Lambda.APIGatewayEvents;
using ApiCalendarizarProcesos.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace ApiCalendarizarProcesos.Helpers {

    [JsonSerializable(typeof(APIGatewayProxyRequest))]
    [JsonSerializable(typeof(APIGatewayProxyResponse))]
    [JsonSerializable(typeof(ProblemDetails))]
    [JsonSerializable(typeof(EntIngresarProceso))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext {

    }
}
