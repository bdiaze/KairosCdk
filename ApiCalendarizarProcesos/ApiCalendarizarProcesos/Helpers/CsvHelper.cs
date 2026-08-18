using System.Globalization;
using System.Text;

namespace ApiCalendarizarProcesos.Helpers {
	public static class CsvHelper {
		public static byte[] ToCsv(List<Dictionary<string, object?>> registros) {
			StringBuilder csv = new();

			List<string> columnas = [];
			HashSet<string> columnasAgregadas = [];

			foreach (Dictionary<string, object?> registro in registros) {
				foreach (string columna in registro.Keys) {
					if (columnasAgregadas.Add(columna)) {
						columnas.Add(columna);
					}
				}
			}

			csv.AppendLine(string.Join(',', columnas.Select(EscapeCsv)));


			foreach (Dictionary<string, object?> registro in registros) {
				List<string> valores = [];

				foreach (string columna in columnas) {
					registro.TryGetValue(columna, out object? valor);
					valores.Add(EscapeCsv(valor));
				}

				csv.AppendLine(string.Join(',', valores));
			}

			return [
				.. Encoding.UTF8.GetPreamble(), 
				.. Encoding.UTF8.GetBytes(csv.ToString())
			];
		}

		private static string EscapeCsv(object? valor) {
			if (valor == null) return "";

			string texto = ConvertirATexto(valor);
			texto = texto.Replace("\"", "\"\"");
			if (texto.Contains(',') ||
				texto.Contains('"') ||
				texto.Contains('\n') ||
				texto.Contains('\r')) {
				texto = $"\"{texto}\"";
			}

			return texto;
		}

		private static string ConvertirATexto(object? valor) => valor switch {
			null => "",
			string s => s,
			bool b => b ? "true" : "false",
			DateTime dt => dt.ToString("O"),
			DateTimeOffset dto => dto.ToString("O"),
			IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
			_ => valor.ToString()!
		};
	}
}
