using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.Scheduler;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.SSM;
using Constructs;
using System;
using System.Collections.Generic;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;
using StageOptions = Amazon.CDK.AWS.APIGateway.StageOptions;

namespace KairosCdk
{
    public class KairosCdkStack : Stack
    {
        internal KairosCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            string appName = System.Environment.GetEnvironmentVariable("APP_NAME") ?? throw new ArgumentNullException("APP_NAME");
            string account = System.Environment.GetEnvironmentVariable("ACCOUNT_AWS") ?? throw new ArgumentNullException("ACCOUNT_AWS");
            string region = System.Environment.GetEnvironmentVariable("REGION_AWS") ?? throw new ArgumentNullException("REGION_AWS");

            #region DynamoDB
            // Se crean tablas para registrar los procesos y calendarizaciones...
            Table tablaProcesos = new(this, $"{appName}DynamoDBTableProcesos", new TableProps {
                TableName = $"{appName}Procesos",
                PartitionKey = new Attribute { 
                    Name = "IdProceso",
                    Type = AttributeType.STRING
                },
                DeletionProtection = true,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            tablaProcesos.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps {
                IndexName = $"PorIdCalendarizacion",
                PartitionKey = new Attribute {
                    Name = "IdCalendarizacion",
                    Type = AttributeType.STRING
                },
            });

            Table tablaCalendarizacion = new(this, $"{appName}DynamoDBTableCalendarizacion", new TableProps {
                TableName = $"{appName}Calendarizaciones",
                PartitionKey = new Attribute {
                    Name = "IdCalendarizacion",
                    Type = AttributeType.STRING
                },
                DeletionProtection = true,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            StringParameter stringParameterDynamoProcesos = new(this, $"{appName}StringParameterDynamoProcesos", new StringParameterProps {
                ParameterName = $"/{appName}/DynamoDB/NombreTablaProcesos",
                Description = $"Nombre tabla de procesos de DynamoDB de la aplicacion {appName}",
                StringValue = tablaProcesos.TableName,
                Tier = ParameterTier.STANDARD,
            });

            StringParameter stringParameterDynamoCalendarizaciones = new(this, $"{appName}StringParameterDynamoCalendarizaciones", new StringParameterProps {
                ParameterName = $"/{appName}/DynamoDB/NombreTablaCalendarizaciones",
                Description = $"Nombre tabla de calendarizaciones de DynamoDB de la aplicacion {appName}",
                StringValue = tablaCalendarizacion.TableName,
                Tier = ParameterTier.STANDARD,
            });
            #endregion

            #region Scheduler
            CfnScheduleGroup scheduleGroup = new(this, $"{appName}ScheduleGroup", new CfnScheduleGroupProps {
                Name = $"{appName}ScheduleGroup"
            });

            StringParameter stringParameterScheduleGroup = new(this, $"{appName}StringParameterScheduleGroup", new StringParameterProps {
                ParameterName = $"/{appName}/Schedule/NombreGrupo",
                Description = $"Nombre del Schedule Group de la aplicacion {appName}",
                StringValue = scheduleGroup.Name,
                Tier = ParameterTier.STANDARD,
            });
            #endregion

            #region SQS y Lambda
            string dispatcherDirectory = System.Environment.GetEnvironmentVariable("DISPATCHER_DIRECTORY") ?? throw new ArgumentNullException("DISPATCHER_DIRECTORY");
            string dispatcherHandler = System.Environment.GetEnvironmentVariable("DISPATCHER_LAMBDA_HANDLER") ?? throw new ArgumentNullException("DISPATCHER_LAMBDA_HANDLER");
            string dispatcherTimeout = System.Environment.GetEnvironmentVariable("DISPATCHER_LAMBDA_TIMEOUT") ?? throw new ArgumentNullException("DISPATCHER_LAMBDA_TIMEOUT");
            string dispatcherMemorySize = System.Environment.GetEnvironmentVariable("DISPATCHER_LAMBDA_MEMORY_SIZE") ?? throw new ArgumentNullException("DISPATCHER_LAMBDA_MEMORY_SIZE");

            // Creación de cola...
            Queue queue = new(this, $"{appName}Queue", new QueueProps {
                QueueName = $"{appName}Queue",
                RetentionPeriod = Duration.Days(14),
                VisibilityTimeout = Duration.Minutes(5),
                EnforceSSL = true,
            });

            StringParameter stringParameterQueueUrl = new(this, $"{appName}StringParameterQueueUrl", new StringParameterProps {
                ParameterName = $"/{appName}/SQS/QueueUrl",
                Description = $"Queue URL de la aplicacion {appName}",
                StringValue = queue.QueueUrl,
                Tier = ParameterTier.STANDARD,
            });

            // Creación de log group lambda...
            LogGroup dispatcherLogGroup = new(this, $"{appName}DispatcherLogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}DispatcherLambdaFunction/logs",
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Creación de role para la función lambda...
            Role roleDispatcherLambda = new(this, $"{appName}DispatcherLambdaRole", new RoleProps {
                RoleName = $"{appName}DispatcherLambdaRole",
                Description = $"Role para Lambda dispatcher de {appName}",
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies = [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument> {
                    {
                        $"{appName}DispatcherLambdaPolicy",
                        new PolicyDocument(new PolicyDocumentProps {
                            Statements = [
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToParameterStore",
                                    Actions = [
                                        "ssm:GetParameter"
                                    ],
                                    Resources = [
                                        stringParameterDynamoProcesos.ParameterArn,
                                        stringParameterQueueUrl.ParameterArn,
                                    ],
                                }),
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToSQS",
                                    Actions = [
                                        "sqs:SendMessage"
                                    ],
                                    Resources = [
                                        queue.QueueArn
                                    ],
                                }),
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToDynamoDB",
                                    Actions = [
                                        "dynamodb:Query"
                                    ],
                                    Resources = [
                                        tablaProcesos.TableArn,
                                        $"{tablaProcesos.TableArn}/*",
                                    ],
                                })
                            ]
                        })
                    }
                }
            });

            // Creación de la función lambda...
            Function dispatcherFunction = new(this, $"{appName}DispatcherLambdaFunction", new FunctionProps {
                FunctionName = $"{appName}DispatcherLambdaFunction",
                Description = $"Función dispatcher encargada de ingresar los procesos a la cola de ejecución de la aplicacion {appName}",
                Runtime = Runtime.DOTNET_8,
                Handler = dispatcherHandler,
                Code = Code.FromAsset($"{dispatcherDirectory}/publish/publish.zip"),
                Timeout = Duration.Seconds(double.Parse(dispatcherTimeout)),
                MemorySize = double.Parse(dispatcherMemorySize),
                Architecture = Architecture.X86_64,
                LogGroup = dispatcherLogGroup,
                Environment = new Dictionary<string, string> {
                    { "APP_NAME", appName },
                },
                Role = roleDispatcherLambda,
            });

            StringParameter stringParameterDispatcherFunction = new(this, $"{appName}StringParameterDispatcherFunction", new StringParameterProps {
                ParameterName = $"/{appName}/Dispatcher/LambdaArn",
                Description = $"ARN del Lambda dispatcher de la aplicacion {appName}",
                StringValue = dispatcherFunction.FunctionArn,
                Tier = ParameterTier.STANDARD,
            });
            #endregion

            #region Role para Scheduler
            // Creación de role usado por Scheduler para gatillar dispatcher lambda...
            Role roleScheduler = new(this, $"{appName}SchedulerRole", new RoleProps {
                RoleName = $"{appName}SchedulerRole",
                Description = $"Role para Scheduler de {appName}",
                AssumedBy = new ServicePrincipal("scheduler.amazonaws.com"),
                InlinePolicies = new Dictionary<string, PolicyDocument> {
                    {
                        $"{appName}SchedulerPolicy",
                        new PolicyDocument(new PolicyDocumentProps {
                            Statements = [
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToDispatcherLambda",
                                    Actions = [
                                        "lambda:InvokeFunction"
                                    ],
                                    Resources = [
                                        dispatcherFunction.FunctionArn
                                    ],
                                })
                            ]
                        })
                    }
                }
            });

            StringParameter stringParameterRoleScheduler = new(this, $"{appName}StringParameterRoleScheduler", new StringParameterProps {
                ParameterName = $"/{appName}/Schedule/RoleArn",
                Description = $"ARN del Rol para Schedule de la aplicacion {appName}",
                StringValue = roleScheduler.RoleArn,
                Tier = ParameterTier.STANDARD,
            });
            #endregion

            #region API Gateway y Lambda
            string apiDirectory = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_DIRECTORY") ?? throw new ArgumentNullException("AOT_MINIMAL_API_DIRECTORY");
            string apiHandler = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_HANDLER") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_HANDLER");
            string apiTimeout = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_TIMEOUT") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_TIMEOUT");
            string apiMemorySize = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_MEMORY_SIZE") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_MEMORY_SIZE");
            string apiDomainName = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_MAPPING_DOMAIN_NAME") ?? throw new ArgumentNullException("AOT_MINIMAL_API_MAPPING_DOMAIN_NAME");
            string apiMappingKey = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_MAPPING_KEY") ?? throw new ArgumentNullException("AOT_MINIMAL_API_MAPPING_KEY");

            // Creación de log group lambda...
            LogGroup logGroup = new(this, $"{appName}APILogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}APILambdaFunction/logs",
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Creación de role para la función lambda...
            IRole roleLambda = new Role(this, $"{appName}APILambdaRole", new RoleProps {
                RoleName = $"{appName}APILambdaRole",
                Description = $"Role para API Lambda de {appName}",
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies = [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument> {
                    {
                        $"{appName}APILambdaPolicy",
                        new PolicyDocument(new PolicyDocumentProps {
                            Statements = [
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToParameterStore",
                                    Actions = [
                                        "ssm:GetParameter"
                                    ],
                                    Resources = [
                                        stringParameterDynamoProcesos.ParameterArn,
                                        stringParameterDynamoCalendarizaciones.ParameterArn,
                                        stringParameterRoleScheduler.ParameterArn,
                                        stringParameterDispatcherFunction.ParameterArn,
                                        stringParameterScheduleGroup.ParameterArn,
                                    ],
                                }),
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToScheduler",
                                    Actions = [
                                        "scheduler:CreateSchedule",
                                        "scheduler:DeleteSchedule",
                                        "scheduler:GetSchedule",
                                    ],
                                    Resources = [
                                        $"arn:aws:scheduler:{this.Region}:{this.Account}:schedule/{scheduleGroup.Name}/*"
                                    ],
                                }),
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToDynamoDB",
                                    Actions = [
                                        "dynamodb:PutItem",
                                        "dynamodb:DeleteItem",
                                        "dynamodb:GetItem",
                                        "dynamodb:Query"
                                    ],
                                    Resources = [
                                        tablaProcesos.TableArn,
                                        $"{tablaProcesos.TableArn}/*",
                                        tablaCalendarizacion.TableArn,
                                        $"{tablaCalendarizacion.TableArn}/*",
                                    ],
                                }),
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToIAM",
                                    Actions = [
                                        "iam:PassRole",
                                    ],
                                    Resources = [
                                        roleScheduler.RoleArn
                                    ],
                                })
                            ]
                        })
                    }
                }
            });

            // Creación de la función lambda...
            Function function = new(this, $"{appName}APILambdaFunction", new FunctionProps {
                FunctionName = $"{appName}APILambdaFunction",
                Description = $"API encargada de programar la ejecución de procesos de la aplicacion {appName}",
                Runtime = Runtime.DOTNET_8,
                Handler = apiHandler,
                Code = Code.FromAsset($"{apiDirectory}/publish/publish.zip"),
                Timeout = Duration.Seconds(double.Parse(apiTimeout)),
                MemorySize = double.Parse(apiMemorySize),
                Architecture = Architecture.X86_64,
                LogGroup = logGroup,
                Environment = new Dictionary<string, string> {
                    { "APP_NAME", appName },
                },
                Role = roleLambda,
            });

            // Creación de access logs...
            LogGroup logGroupAccessLogs = new(this, $"{appName}APILambdaFunctionLogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}APILambdaFunction/access_logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Creación de la LambdaRestApi...
            LambdaRestApi lambdaRestApi = new(this, $"{appName}APILambdaRestApi", new LambdaRestApiProps {
                RestApiName = $"{appName}APILambdaRestApi",
                Handler = function,
                DeployOptions = new StageOptions {
                    AccessLogDestination = new LogGroupLogDestination(logGroupAccessLogs),
                    AccessLogFormat = AccessLogFormat.Custom("'{\"requestTime\":\"$context.requestTime\",\"requestId\":\"$context.requestId\",\"httpMethod\":\"$context.httpMethod\",\"path\":\"$context.path\",\"resourcePath\":\"$context.resourcePath\",\"status\":$context.status,\"responseLatency\":$context.responseLatency,\"xrayTraceId\":\"$context.xrayTraceId\",\"integrationRequestId\":\"$context.integration.requestId\",\"functionResponseStatus\":\"$context.integration.status\",\"integrationLatency\":\"$context.integration.latency\",\"integrationServiceStatus\":\"$context.integration.integrationStatus\",\"authorizeStatus\":\"$context.authorize.status\",\"authorizerStatus\":\"$context.authorizer.status\",\"authorizerLatency\":\"$context.authorizer.latency\",\"authorizerRequestId\":\"$context.authorizer.requestId\",\"ip\":\"$context.identity.sourceIp\",\"userAgent\":\"$context.identity.userAgent\",\"principalId\":\"$context.authorizer.principalId\"}'"),
                    StageName = "prod",
                    Description = $"Stage para produccion de la aplicacion {appName}",
                },
                DefaultMethodOptions = new MethodOptions {
                    ApiKeyRequired = true,                   
                },
            });

            // Creación de la CfnApiMapping para el API Gateway...
            CfnApiMapping apiMapping = new(this, $"{appName}APIApiMapping", new CfnApiMappingProps {
                DomainName = apiDomainName,
                ApiMappingKey = apiMappingKey,
                ApiId = lambdaRestApi.RestApiId,
                Stage = lambdaRestApi.DeploymentStage.StageName,
            });

            // Se crea Usage Plan para configurar API Key...
            UsagePlan usagePlan = new(this, $"{appName}APIUsagePlan", new UsagePlanProps {
                Name = $"{appName}APIUsagePlan",
                Description = $"Usage Plan de {appName} API",
                ApiStages = [
                    new UsagePlanPerApiStage() {
                        Api = lambdaRestApi,
                        Stage = lambdaRestApi.DeploymentStage
                    }
                ],
            });

            // Se crea API Key...
            ApiKey apiGatewayKey = new(this, $"{appName}APIAPIKey", new ApiKeyProps {
                ApiKeyName = $"{appName}APIAPIKey",
                Description = $"API Key de {appName} API",
            });
            usagePlan.AddApiKey(apiGatewayKey);

            // Se configura permisos para la ejecucíon de la Lambda desde el API Gateway...
            ArnPrincipal arnPrincipal = new("apigateway.amazonaws.com");
            Permission permission = new() {
                Scope = this,
                Action = "lambda:InvokeFunction",
                Principal = arnPrincipal,
                SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{lambdaRestApi.RestApiId}/*/*/*",
            };
            function.AddPermission($"{appName}APIPermission", permission);

            // Se configuran parámetros para ser rescatados por consumidores...
            _ = new StringParameter(this, $"{appName}StringParameterApiUrl", new StringParameterProps {
                ParameterName = $"/{appName}/Api/Url",
                Description = $"API URL de la aplicacion {appName}",
                StringValue = $"https://{apiMapping.DomainName}/{apiMapping.ApiMappingKey}/",
                Tier = ParameterTier.STANDARD,
            });

            _ = new StringParameter(this, $"{appName}StringParameterApiKeyId", new StringParameterProps {
                ParameterName = $"/{appName}/Api/KeyId",
                Description = $"API Key ID de la aplicacion {appName}",
                StringValue = $"{apiGatewayKey.KeyId}",
                Tier = ParameterTier.STANDARD,
            });
            #endregion
        }
    }
}
