using Amazon.Lambda.APIGatewayEvents;
using ApiCalendarizarProcesos.Models;
using LibreriaCompartida.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ApiCalendarizarProcesos.Helpers {
	[ExcludeFromCodeCoverage]
	[JsonSerializable(typeof(APIGatewayProxyRequest))]
    [JsonSerializable(typeof(APIGatewayProxyResponse))]
    [JsonSerializable(typeof(ProblemDetails))]
    [JsonSerializable(typeof(EntIngresarProceso))]
    [JsonSerializable(typeof(DispatcherInput))]
	[JsonSerializable(typeof(Proceso))]
	[JsonSerializable(typeof(List<Proceso>))]
	[JsonSerializable(typeof(List<Calendarizacion>))]
	internal partial class AppJsonSerializerContext : JsonSerializerContext {

    }
}
