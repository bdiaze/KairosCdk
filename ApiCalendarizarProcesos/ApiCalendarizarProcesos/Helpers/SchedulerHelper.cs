using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using ApiCalendarizarProcesos.Models;
using System.Net;
using System.Threading.Tasks;

namespace ApiCalendarizarProcesos.Helpers {
    public class SchedulerHelper(IAmazonScheduler client) {
        public async Task<Schedule?> Crear(string nombre, string descripcion, string grupo, string? cron, int? frecuenciaDias, DateTime? inicioEjecucionUtc, string roleArn, string dlqArn, string targetArn, string targetInput) {
			if ((cron == null && frecuenciaDias == null) || (cron != null && frecuenciaDias != null))
				throw new InvalidOperationException("Se debe definir una configuración cron o frecuencia en días.");

            string scheduleExpression;
            if (cron != null) {
                scheduleExpression = $"cron({cron})";
            } else {
                scheduleExpression = $"rate({frecuenciaDias} days)";
            }

			CreateScheduleRequest request = new() {
                Name = nombre,
                Description = descripcion,
                GroupName = grupo,
                ScheduleExpression = scheduleExpression,
                StartDate = inicioEjecucionUtc,
                FlexibleTimeWindow = new FlexibleTimeWindow { 
                    Mode = FlexibleTimeWindowMode.OFF
                },
                Target = new Target {
                    Arn = targetArn,
                    RoleArn = roleArn,
                    Input = targetInput,
                    DeadLetterConfig = new DeadLetterConfig {
                        Arn = dlqArn,
                    },
                    RetryPolicy = new RetryPolicy {
                        MaximumRetryAttempts = 5,
                        MaximumEventAgeInSeconds = 60 * 60,
                    },
                },
                ScheduleExpressionTimezone = "America/Santiago",

            };

            CreateScheduleResponse response = await client.CreateScheduleAsync(request);
            if (response.HttpStatusCode != HttpStatusCode.OK) {
                throw new Exception("No se pudo crear el schedule");
            }

            return await Obtener(nombre, grupo);
        }
        
        public async Task Eliminar(string nombre, string grupo) {
            DeleteScheduleRequest request = new() { 
                Name = nombre,
                GroupName = grupo
            };

            DeleteScheduleResponse response = await client.DeleteScheduleAsync(request);
            if (response.HttpStatusCode != HttpStatusCode.OK) {
                throw new Exception("No se pudo eliminar el schedule");
            }
        }

        public async Task<Schedule?> Obtener(string nombre, string grupo) {
            GetScheduleRequest request = new() {
                Name = nombre,
                GroupName = grupo
            };

            try {
                GetScheduleResponse response = await client.GetScheduleAsync(request);
                if (response.HttpStatusCode != HttpStatusCode.OK) {
                    throw new Exception("No se pudo obtener el schedule");
                }

                string? cron = null;
                if (response.ScheduleExpression.Contains("cron(")) {
                    cron = response.ScheduleExpression.Replace("cron(", "").Replace(")", "");
				}
                int? frecuenciaDias = null;
                if (response.ScheduleExpression.Contains("rate(") && response.ScheduleExpression.Contains("days)")) {
                    frecuenciaDias = Convert.ToInt32(response.ScheduleExpression.Replace("rate(", "").Replace("days)", ""));
                }

                return new Schedule {
                    Nombre = response.Name,
                    Descripcion = response.Description,
                    Grupo = response.GroupName,
                    Cron = cron,
                    FrecuenciaDias = frecuenciaDias,
					InicioEjecucionUtc = response.StartDate,
					Arn = response.Arn,
                };
            } catch (ResourceNotFoundException) {
                return null;
            }
        }
    }
}
