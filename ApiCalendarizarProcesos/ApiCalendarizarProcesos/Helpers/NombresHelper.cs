using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ApiCalendarizarProcesos.Helpers {
	public static class NombresHelper {
		private static readonly Dictionary<string, string> mapDias = new() {
			{"1","dom"}, 
			{"2","lun"}, 
			{"3","mar"}, 
			{"4","mie"},
			{"5","jue"}, 
			{"6","vie"}, 
			{"7","sab"},
			{"SUN","dom"}, 
			{"MON","lun"}, 
			{"TUE","mar"}, 
			{"WED","mie"},
			{"THU","jue"}, 
			{"FRI","vie"}, 
			{"SAT","sab"}
		};

		private static readonly Dictionary<string, string> mapMeses = new() {
			{"1","ene"}, 
			{"2","feb"}, 
			{"3","mar"}, 
			{"4","abr"},
			{"5","may"}, 
			{"6","jun"}, 
			{"7","jul"}, 
			{"8","ago"},
			{"9","sep"}, 
			{"10","oct"}, 
			{"11","nov"}, 
			{"12","dic"}
		};

		public static string GenerarNombreCalendarizacion(string cron) {
			string[] parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length != 6) throw new ArgumentException("Cron inválido");

			string min		= parts[0];
			string hour		= parts[1];
			string day		= parts[2];
			string month	= parts[3];
			string weekday	= parts[4];
			string year		= parts[5];

			List<string> segments = [];
			if (day != "*" && day != "?")
				segments.Add("dia" + day.Replace(",", "."));
			
			if (month != "*" && month != "?") {
				string[] mesesTexto = [.. month
					.Split(',')
					.Select(m => mapMeses.TryGetValue(m.Trim(), out string? nombre) ? nombre : m.Trim())
				];
				segments.Add("meses." + string.Join(".", mesesTexto));
			}

			if (year != "*" && year != "?")
				segments.Add("anno" + year);

			if (weekday != "*" && weekday != "?") {
				string[] diasTexto = [.. weekday.ToUpperInvariant()
					.Split(',')
					.Select(d => mapDias.TryGetValue(d.Trim(), out string? nombre) ? nombre : d.Trim())
				];
				segments.Add("dias." + string.Join(".", diasTexto));
			}

			string horaTexto = hour.StartsWith("*/") ? $"cada{hour[2..]}h" : $"{hour.Replace("*", "X")}h";
			string minTexto = min.StartsWith("*/") ? $"cada{min[2..]}min" : $"{min.Replace("*", "X")}min";

			segments.Add(horaTexto);
			segments.Add(minTexto);

			string nombre = "calendarizacion-" + string.Join("-", segments);
			nombre = Regex.Replace(nombre, @"[^a-zA-Z0-9\-._]", ".");
			return nombre.Length > 64 ? nombre[..64] : nombre;
		}

		public static string GenerarNombreProceso(string nombre) {
			// Se quitan tildes...
			nombre = nombre.Normalize(NormalizationForm.FormD);
			nombre = new string([.. nombre.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);

			// Se reemplazan caracteres de posibles Cron...
			nombre = nombre.Replace("*", "x").Replace("?", "x").Replace(",", ".");
			// Se reemplazan espacios...
			nombre = nombre.Replace(" ", "-");
			// Se eliminan caracteres no permitidos...
			nombre = Regex.Replace(nombre, @"[^a-zA-Z0-9\-._]", "");
			// Se reemplazan secuencias de caracteres repetidos...
			nombre = Regex.Replace(nombre, @"[-_.]{2,}", "-");

			return $"proceso-{nombre.ToLowerInvariant().Trim('-', '.', '_')}-{Guid.NewGuid():N}";
		}

		/*
		string idProceso = $"proceso-{Convert.ToBase64String(Encoding.UTF8.GetBytes($"{entrada.Nombre}/{entrada.ArnProceso}/{entrada.Cron}")).Replace("+", "-").Replace("/", "_").Replace("=", ".")}";
                    
		*/
	}
}
