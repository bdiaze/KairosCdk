using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using ApiCalendarizarProcesos.Models;
using System.Net;
using System.Threading.Tasks;

namespace ApiCalendarizarProcesos.Helpers {
    public class SchedulerHelper(IAmazonScheduler client) {
        public async Task<Schedule?> Crear(string nombre, string descripcion, string grupo, string cron, string roleArn, string dlqArn, string targetArn, string targetInput) {
            CreateScheduleRequest request = new() {
                Name = nombre,
                Description = descripcion,
                GroupName = grupo,
                ScheduleExpression = $"cron({cron})",
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

                return new Schedule {
                    Nombre = response.Name,
                    Descripcion = response.Description,
                    Grupo = response.GroupName,
                    Cron = response.ScheduleExpression.Replace("cron(", "").Replace(")", ""),
                    Arn = response.Arn,
                };
            } catch (ResourceNotFoundException) {
                return null;
            }
        }
    }
}
