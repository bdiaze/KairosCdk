using ApiCalendarizarProcesos.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApiCalendarizarProcesos.Test.Helpers {
	public class VariableEntornoHelperTest {
		private readonly IHostEnvironment env = Substitute.For<IHostEnvironment>();
		private readonly IConfiguration config = Substitute.For<IConfiguration>();
		private readonly VariableEntornoHelper variableEntorno;

		public VariableEntornoHelperTest() {
			variableEntorno = new(env, config);
		}

		[Fact]
		public async Task Obtener_ValidoProduction() {
			env.EnvironmentName = Environments.Production;
			Environment.SetEnvironmentVariable("NOMBRE_VARIABLE_TEST", "VALOR_VARIABLE_TEST");

			try {
				string retorno = variableEntorno.Obtener("NOMBRE_VARIABLE_TEST");
				Assert.Equal("VALOR_VARIABLE_TEST", retorno);
				env.Received(1).IsProduction();
			} finally {
				Environment.SetEnvironmentVariable("NOMBRE_VARIABLE_TEST", null);
			}
		}

		[Fact]
		public async Task Obtener_InvalidoProduction() {
			env.EnvironmentName = Environments.Production;

			Assert.Throws<InvalidOperationException>(() => variableEntorno.Obtener("NOMBRE_VARIABLE_TEST"));
			env.Received(1).IsProduction();
		}

		[Fact]
		public async Task Obtener_ValidoDevelopment() {
			env.EnvironmentName = Environments.Development;
			config["VariableEntorno:NOMBRE_VARIABLE_TEST"].Returns("VALOR_VARIABLE_TEST");

			string retorno = variableEntorno.Obtener("NOMBRE_VARIABLE_TEST");
			Assert.Equal("VALOR_VARIABLE_TEST", retorno);
			env.Received(1).IsProduction();
		}

		[Fact]
		public async Task Obtener_InvalidoDevelopment() {
			env.EnvironmentName = Environments.Development;
			config["VariableEntorno:NOMBRE_VARIABLE_TEST"].Returns((string?)null);

			Assert.Throws<InvalidOperationException>(() => variableEntorno.Obtener("NOMBRE_VARIABLE_TEST"));
			env.Received(1).IsProduction();
		}
	}
}
