using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ApiCalendarizarProcesos.Helpers;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApiCalendarizarProcesos.Test.Helpers {
	public class DynamoHelperTest {
		private readonly IAmazonDynamoDB client = Substitute.For<IAmazonDynamoDB>();
		private readonly DynamoHelper dynamoHelper;

		public DynamoHelperTest() {
			dynamoHelper = new(client);
		}

		[Fact]
		public async Task InsertarTest() {
			client.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>()).Returns(new PutItemResponse() {
				HttpStatusCode = System.Net.HttpStatusCode.OK
			});
			Dictionary<string, object?> entrada = new() {
				["atributo_1_test"] = null,
				["atributo_2_test"] = "valor_2_test",
				["atributo_3_test"] = 100,
				["atributo_4_test"] = 1000L,
				["atributo_5_test"] = 50.5,
				["atributo_6_test"] = 500.55f,
				["atributo_7_test"] = true,
				["atributo_8_test"] = new List<int>() { 1, 2, 3 },
				["atributo_9_test"] = new Dictionary<string, object?>() { ["key_1"] = "value_1", ["key_2"] = 10 }
			};

			Dictionary<string, object?>? retorno = await dynamoHelper.Insertar("nombre-tabla-test", entrada);
			Assert.NotNull(retorno);
			Assert.Equal(9, retorno.Keys.Count);
			Assert.Null(retorno["atributo_1_test"]);
			Assert.Equal("valor_2_test", retorno["atributo_2_test"]);
			Assert.Equal(100, retorno["atributo_3_test"]);
			Assert.Equal(1000L, retorno["atributo_4_test"]);
			Assert.Equal(50.5, retorno["atributo_5_test"]);
			Assert.Equal(500.55f, retorno["atributo_6_test"]);
			Assert.Equal(true, retorno["atributo_7_test"]);
			Assert.Equal(3, (retorno["atributo_8_test"] as List<int>)!.Count);
			Assert.Equal(2, (retorno["atributo_9_test"] as Dictionary<string, object?>)!.Keys.Count);
			await client.Received(1).PutItemAsync(
				Arg.Is<PutItemRequest>(r =>
					r != null &&
					r.TableName == "nombre-tabla-test" &&
					r.Item["atributo_1_test"].NULL == true &&
					r.Item["atributo_2_test"].S == "valor_2_test" &&
					r.Item["atributo_3_test"].N == "100" &&
					r.Item["atributo_4_test"].N == "1000" &&
					r.Item["atributo_5_test"].N == "50.5" &&
					r.Item["atributo_6_test"].N == "500.55" &&
					r.Item["atributo_7_test"].BOOL == true &&
					r.Item["atributo_8_test"].L.Count == 3 &&
					r.Item["atributo_9_test"].M.Keys.Count == 2
				),
				Arg.Any<CancellationToken>()
			);
		}

		[Fact]
		public async Task InsertarTest_Invalido() {
			client.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>()).Returns(new PutItemResponse() {
				HttpStatusCode = System.Net.HttpStatusCode.BadRequest
			});
			Dictionary<string, object?> entrada = new() {
				["atributo_1_test"] = "valor_1_test",
			};

			await Assert.ThrowsAsync<HttpRequestException>(() => dynamoHelper.Insertar("nombre-tabla-test", entrada));
			await client.Received(1).PutItemAsync(
				Arg.Is<PutItemRequest>(r =>
					r != null &&
					r.TableName == "nombre-tabla-test" &&
					r.Item["atributo_1_test"].S == "valor_1_test"
				),
				Arg.Any<CancellationToken>()
			);
		}

		[Fact]
		public async Task ObtenerTest() {
			client.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>()).Returns(new GetItemResponse() {
				HttpStatusCode = System.Net.HttpStatusCode.OK,
				Item = new Dictionary<string, AttributeValue>() {
					["atributo_1_test"] = new AttributeValue { NULL = true },
					["atributo_2_test"] = new AttributeValue { S = "valor_2_test" },
					["atributo_3_test"] = new AttributeValue { N = "100" },
					["atributo_4_test"] = new AttributeValue { N = "1000" },
					["atributo_5_test"] = new AttributeValue { N = "50.5" },
					["atributo_6_test"] = new AttributeValue { N = "500.55" },
					["atributo_7_test"] = new AttributeValue { BOOL = true },
					["atributo_8_test"] = new AttributeValue {
						L = [
							new AttributeValue { N = "1" },
							new AttributeValue { N = "2" },
							new AttributeValue { N = "3" },
						]
					},
					["atributo_9_test"] = new AttributeValue { 
						M = new Dictionary<string, AttributeValue> {
							["key_1"] = new AttributeValue { S = "value_1" },
							["key_2"] = new AttributeValue { N = "10" },
						} 
					},
				}
			});

			Dictionary<string, object?>? retorno = await dynamoHelper.Obtener("nombre-tabla-test", new Dictionary<string, object?> { ["key"] = "value-key" });
			Assert.NotNull(retorno);
			Assert.Null(retorno["atributo_1_test"]);
			Assert.Equal("valor_2_test", retorno["atributo_2_test"]);
			Assert.Equal(100, retorno["atributo_3_test"]!);
			Assert.Equal(1000, retorno["atributo_4_test"]);
			Assert.Equal(50.5m, retorno["atributo_5_test"]);
			Assert.Equal(500.55m, retorno["atributo_6_test"]);
			Assert.Equal(true, retorno["atributo_7_test"]);
			Assert.Equal(3, (retorno["atributo_8_test"] as List<object?>)!.Count);
			Assert.Equal(2, (retorno["atributo_9_test"] as Dictionary<string, object?>)!.Keys.Count);
			await client.Received(1).GetItemAsync(
				Arg.Is<GetItemRequest>(r => 
					r != null &&
					r.TableName == "nombre-tabla-test" &&
					r.Key["key"].S == "value-key"
				), 
				Arg.Any<CancellationToken>()
			);
		} 
	}
}
