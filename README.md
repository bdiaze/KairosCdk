# Kairos - Calendarización de Procesos

- [Kairos - Calendarización de Procesos](#kairos---calendarización-de-procesos)
  - [Introducción](#introducción)
    - [Diagrama Arquitectura](#diagrama-arquitectura)
  - [Recursos Requeridos](#recursos-requeridos)
    - [API Gateway Custom Domain Name](#api-gateway-custom-domain-name)
  - [Recursos Creados](#recursos-creados)
    - [Modelo de Datos](#modelo-de-datos)
      - [DynamoDB Tables y Global Secondary Index](#dynamodb-tables-y-global-secondary-index)
      - [Systems Manager String Parameter](#systems-manager-string-parameter)
    - [Sistema de Colas](#sistema-de-colas)
      - [SQS Queue y Dead Letter Queue](#sqs-queue-y-dead-letter-queue)
      - [SNS Topic y CloudWatch Alarm](#sns-topic-y-cloudwatch-alarm)
      - [Systems Manager String Parameter](#systems-manager-string-parameter-1)
    - [Lambda Dispatcher](#lambda-dispatcher)
      - [Log Group e IAM Role](#log-group-e-iam-role)
      - [Lambda Function](#lambda-function)
      - [Systems Manager String Parameter](#systems-manager-string-parameter-2)
    - [Lambda Executor](#lambda-executor)
      - [Log Group e IAM Role](#log-group-e-iam-role-1)
      - [Systems Manager String Parameter](#systems-manager-string-parameter-3)
      - [Lambda Function (con Event Source)](#lambda-function-con-event-source)
    - [Recursos para Schedulers](#recursos-para-schedulers)
      - [Schedule Group](#schedule-group)
      - [Dead Letter Queue](#dead-letter-queue)
      - [SNS Topic y CloudWatch Alarm](#sns-topic-y-cloudwatch-alarm-1)
      - [IAM Role](#iam-role)
      - [Systems Manager String Parameter](#systems-manager-string-parameter-4)
    - [API Calendarizar Procesos](#api-calendarizar-procesos)
      - [Log Group e IAM Role](#log-group-e-iam-role-2)
      - [Lambda Function](#lambda-function-1)
      - [Access Log Group](#access-log-group)
      - [Lambda Rest API](#lambda-rest-api)
      - [API Mapping](#api-mapping)
      - [Usage Plan y API Key](#usage-plan-y-api-key)
      - [API Gateway Permission](#api-gateway-permission)
      - [Systems Manager String Parameter](#systems-manager-string-parameter-5)
  - [Lógica de Lambdas](#lógica-de-lambdas)
    - [API para Calendarizar Procesos](#api-para-calendarizar-procesos)
      - [Endpoints](#endpoints)
      - [Código Endpoint POST](#código-endpoint-post)
      - [Código Endpoint DELETE](#código-endpoint-delete)
    - [Lambda Dispatcher](#lambda-dispatcher-1)
      - [Código](#código)
    - [Lambda Executor](#lambda-executor-1)
      - [Código](#código-1)
  - [Despliegue](#despliegue)
    - [Variables y Secretos de Entorno](#variables-y-secretos-de-entorno)

## Introducción

* Kairos es una herramienta para la calendarización de procesos.
* El siguiente repositorio es para desplegar Kairos, lo que incluye la creación de [Lambdas](https://aws.amazon.com/es/lambda/), [API Gateway](https://aws.amazon.com/es/api-gateway/), [DynamoDB](https://aws.amazon.com/es/dynamodb/), [EventBridge Scheduler](https://aws.amazon.com/es/eventbridge/scheduler/), [SQS Queues](https://aws.amazon.com/es/sqs/), [CloudWatch Alarms](https://aws.amazon.com/es/cloudwatch/), [SNS Topics](https://aws.amazon.com/es/sns/).
* La infraestructura se despliega mediante IaC, usando [AWS CDK en .NET 8.0](https://docs.aws.amazon.com/cdk/api/v2/dotnet/api/).
* El despliegue CI/CD se lleva a cabo mediante  [GitHub Actions](https://github.com/features/actions).

### Diagrama Arquitectura

![Diagrama de Arquitectura de Kairos](./images/ArquitecturaKairos.drawio.png)

## Recursos Requeridos

### API Gateway Custom Domain Name

Es necesario contar con un Custom Domain Name ya asociado a API Gateway, esto dado a que se usará para crear el API Mapping.

<ins>Código donde se usará Custom Domain Name</ins>

```csharp
using Amazon.CDK.AWS.Apigatewayv2;

// Creación de la CfnApiMapping para el API Gateway...
CfnApiMapping apiMapping = new(this, ..., new CfnApiMappingProps {
    DomainName = domainName,
    ApiMappingKey = ...,
    ApiId = ...,
    Stage = ...,
});
```

Para ver un ejemplo de como crear un Custom Domain Name: [BDiazEApiGatewayCDK](https://github.com/bdiaze/BDiazEApiGatewayCDK)

## Recursos Creados

### Modelo de Datos

En primer lugar, se comenzará creando las tablas necesarias para almacenar la información de los procesos y calendarizaciones.

#### DynamoDB Tables y Global Secondary Index

Se crearán dos tablas, una para administrar los procesos que se tienen que ejecutar y la segunda para administrar las distintas calendarizaciones.

<ins>Código para crear Tables y Global Secondary Index:</ins>

```csharp
using Amazon.CDK.AWS.DynamoDB;

// Se crean tablas para registrar los procesos y calendarizaciones...
Table tablaProcesos = new(this, ..., new TableProps {
    TableName = $"{...}Procesos",
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

Table tablaCalendarizacion = new(this, ..., new TableProps {
    TableName = $"{...}Calendarizaciones",
    PartitionKey = new Attribute {
        Name = "IdCalendarizacion",
        Type = AttributeType.STRING
    },
    DeletionProtection = true,
    BillingMode = BillingMode.PAY_PER_REQUEST,
    RemovalPolicy = RemovalPolicy.DESTROY
});
```

> [!NOTE]
> Junto con crear las dos tablas, también se crea un índice para consultar la tabla de procesos según el ID de calendarización. Este índice será usado por la [Lambda Dispatcher](#lambda-dispatcher) para determinar los procesos que se deben despachar.

#### Systems Manager String Parameter

Además de crear las tablas, se crearán dos parámetros para almacenar los nombres de ambas tablas.

<ins>Código para crear String Parameter:</ins>

```csharp
using Amazon.CDK.AWS.SSM;

StringParameter stringParameterDynamoProcesos = new(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/DynamoDB/NombreTablaProcesos",
    Description = ...,
    StringValue = tablaProcesos.TableName,
    Tier = ParameterTier.STANDARD,
});

StringParameter stringParameterDynamoCalendarizaciones = new(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/DynamoDB/NombreTablaCalendarizaciones",
    Description = ...,
    StringValue = tablaCalendarizacion.TableName,
    Tier = ParameterTier.STANDARD,
});
```

### Sistema de Colas

En segundo lugar, dado que no se conoce la cantidad ni duración de los procesos a ejecutar, se contará con un sistema de colas para no saturar la [Lambda Executor](#lambda-executor).

#### SQS Queue y Dead Letter Queue

Comenzamos creando la Queue que almacenará todos los procesos despachados que deben ser ejecutados, con su respectiva DLQ.

<ins>Código para crear SQS Queue y DLQ:</ins>

```csharp
using Amazon.CDK.AWS.SQS;

// Creación de cola...
Queue dlq = new(this, ..., new QueueProps {
    QueueName = $"{...}DeadLetterQueue",
    RetentionPeriod = Duration.Days(14),
    EnforceSSL = true,
});

Queue queue = new(this, ..., new QueueProps {
    QueueName = $"{...}Queue",
    RetentionPeriod = Duration.Days(14),
    VisibilityTimeout = Duration.Seconds(Math.Round(double.Parse(...) * 1.5)),
    EnforceSSL = true,
    DeadLetterQueue = new DeadLetterQueue {
        Queue = dlq,
        MaxReceiveCount = 3,
    },
});
```

#### SNS Topic y CloudWatch Alarm

Además, se crea un Topic y Alarm para notificar al equipo productivo que hubo un problema gatillando un proceso y éste cayó en la DLQ.

<ins>Código para crear SNS Topic y CloudWatch Alarm:</ins>

```csharp
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;

// Se crea SNS topic para notificaciones asociadas a la instancia...
Topic topic = new(this, ..., new TopicProps {
    TopicName = ...,
});

foreach (string email in notificationEmails.Split(",")) {
    topic.AddSubscription(new EmailSubscription(email));
}

// Se crea alarma para enviar notificación cuando llegue un elemento al DLQ...
Alarm alarm = new(this, ..., new AlarmProps {
    AlarmName = ...,
    AlarmDescription = ...,
    Metric = dlq.MetricApproximateNumberOfMessagesVisible(new MetricOptions {
        Period = Duration.Minutes(5),
        Statistic = Stats.MAXIMUM,
    }),
    Threshold = 1,
    EvaluationPeriods = 1,
    DatapointsToAlarm = 1,
    ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
    TreatMissingData = TreatMissingData.NOT_BREACHING,
});
alarm.AddAlarmAction(new SnsAction(topic));
```

#### Systems Manager String Parameter

Por último, se crea un parámetro que contenga la URL de la Queue para ser usado por la [Lambda Dispatcher](#lambda-dispatcher).

<ins>Código para crear String Parameter:</ins>

```csharp
using Amazon.CDK.AWS.SSM;

StringParameter stringParameterQueueUrl = new(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/SQS/QueueUrl",
    Description = ...,
    StringValue = queue.QueueUrl,
    Tier = ParameterTier.STANDARD,
});
```

### Lambda Dispatcher

En tercer lugar, se creará una Lambda cuyo proposito será obtener todos los procesos habilitados asociados a una calendarización e ingresarlos en la [cola para su ejecución](#sqs-queue-y-dead-letter-queue).

#### Log Group e IAM Role

Se comienza creando los recursos base de una Lambda, es decir, el Log Group y el Role.

<ins>Código para crear Log Group y Role:</ins>

```csharp
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.IAM;

// Creación de log group lambda...
LogGroup dispatcherLogGroup = new(this, ..., new LogGroupProps {
    LogGroupName = ...,
    RemovalPolicy = RemovalPolicy.DESTROY
});

// Creación de role para la función lambda...
Role roleDispatcherLambda = new(this, ..., new RoleProps {
    RoleName = ...,
    Description = ...,
    AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
    ManagedPolicies = [
        ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
        ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
    ],
    InlinePolicies = new Dictionary<string, PolicyDocument> {
        {
            ...,
            new PolicyDocument(new PolicyDocumentProps {
                Statements = [
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
                        Actions = [
                            "ssm:GetParameter"
                        ],
                        Resources = [
                            stringParameterDynamoProcesos.ParameterArn,
                            stringParameterQueueUrl.ParameterArn,
                        ],
                    }),
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
                        Actions = [
                            "sqs:SendMessage"
                        ],
                        Resources = [
                            queue.QueueArn
                        ],
                    }),
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
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
```
> [!NOTE]
> Destacar como el Role de la Lambda requiere acceso de lectura sobre la [DynamoDB que administra los procesos](#dynamodb-tables-y-global-secondary-index), acceso para ingresar mensajes a [la cola de procesos despachados](#sqs-queue-y-dead-letter-queue), y acceso para obtener los parámetros que almacenan estos dos valores.

#### Lambda Function

Se continua con la creación de la función Lambda que despachará los procesos a ejecutar.

<ins>Código para crear Lambda Function:</ins>

```csharp
using Amazon.CDK.AWS.Lambda;

// Creación de la función lambda...
Function dispatcherFunction = new(this, ..., new FunctionProps {
    FunctionName = ...,
    Description = ...,
    Runtime = Runtime.DOTNET_8,
    Handler = dispatcherHandler,
    Code = Code.FromAsset($"{...}/publish/publish.zip"),
    Timeout = Duration.Seconds(double.Parse(...)),
    MemorySize = double.Parse(...),
    Architecture = Architecture.X86_64,
    LogGroup = dispatcherLogGroup,
    Environment = new Dictionary<string, string> {
        { "APP_NAME", ... },
    },
    Role = roleDispatcherLambda,
});
```

#### Systems Manager String Parameter

Y por último, se crea un parámetro para almacenar el ARN de la Lambda Dispatcher. Éste será usado por la [API de calendarización](#api-calendarizar-procesos) para crear los Schedulers apuntando a la Lambda Dispatcher como Target.

<ins>Código para crear String Parameter:</ins>

```csharp
StringParameter stringParameterDispatcherFunction = new(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/Dispatcher/LambdaArn",
    Description = ...,
    StringValue = dispatcherFunction.FunctionArn,
    Tier = ParameterTier.STANDARD,
});
```

### Lambda Executor

En cuarto lugar, se creará una Lambda cuyo proposito será gatillar la ejecución de los procesos según lo que obtenga desde la [cola de procesos](#sqs-queue-y-dead-letter-queue).

#### Log Group e IAM Role

Se comienza creando los recursos base de una Lambda, es decir, el Log Group y el Role.

<ins>Código para crear Log Group y Role:</ins>

```csharp
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.IAM;

// Creación de log group lambda...
LogGroup executorLogGroup = new(this, ..., new LogGroupProps {
    LogGroupName = ...,
    RemovalPolicy = RemovalPolicy.DESTROY
});

// Creación de role para la función lambda...
Role roleExecutorLambda = new(this, ..., new RoleProps {
    RoleName = ...,
    Description = ...,
    AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
    ManagedPolicies = [
        ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
        ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
    ],
    InlinePolicies = new Dictionary<string, PolicyDocument> {
        {
            ...,
            new PolicyDocument(new PolicyDocumentProps {
                Statements = [
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
                        Actions = [
                            "sts:AssumeRole",
                        ],
                        Resources = [
                            $"arn:aws:iam::{this.Account}:role/{executorPrefixRoles}*"
                        ],
                    }),
                ]
            })
        }
    }
});
```

> [!NOTE]
> Destacar como el Role de la Lambda requiere acceso para asumir los roles que tengan un prefijo en particular. Cada vez que se registre un proceso a ejecutar, es necesario entregar un Role con acceso de ejecución sobre ese proceso cuyo nombre comience con el prefijo en cuestión.

#### Systems Manager String Parameter

Por último se crean parámetros para almacenar el ARN del Role de la Lambda Executor (para que los Roles de ejecución le permitan asumirlos) y el prefijo que deben tener los Roles de ejecución.

<ins>Código para crear String Parameter:</ins>

```csharp
using Amazon.CDK.AWS.SSM;

_ = new StringParameter(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/Executor/RoleArn",
    Description = ...,
    StringValue = roleExecutorLambda.RoleArn,
    Tier = ParameterTier.STANDARD,
});

_ = new StringParameter(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/Executor/PrefixRoles",
    Description = ...,
    StringValue = executorPrefixRoles,
    Tier = ParameterTier.STANDARD,
});
```

#### Lambda Function (con Event Source)

Por último, se crea la función Lambda que gatillará la ejecución de los procesos. Esta Lambda tendrá como Source la [Queue de procesos](#sqs-queue-y-dead-letter-queue).

<ins>Código para crear Lambda con Event Source:</ins>

```csharp
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;

// Creación de la función lambda...
Function executorFunction = new(this, ..., new FunctionProps {
    FunctionName = ...,
    Description = ...,
    Runtime = Runtime.DOTNET_8,
    Handler = executorHandler,
    Code = Code.FromAsset($"{...}/publish/publish.zip"),
    Timeout = Duration.Seconds(double.Parse(...)),
    MemorySize = double.Parse(...),
    Architecture = Architecture.X86_64,
    LogGroup = executorLogGroup,
    Environment = new Dictionary<string, string> {
        { "APP_NAME", ... },
    },
    Role = roleExecutorLambda,
});

executorFunction.AddEventSource(new SqsEventSource(queue, new SqsEventSourceProps {
    Enabled = true,
    BatchSize = Math.Round(double.Parse(...) * 5 * 0.5),
    MaxBatchingWindow = Duration.Seconds(30),
    ReportBatchItemFailures = true,
}));
```

### Recursos para Schedulers

En quinto lugar, se crearán los recursos necesarios para la creación de los schedulers, esto incluye el schedule group, la DLQ con su respectivo Alarm y SNS Topic, IAM Role e String Parameters.

#### Schedule Group

Se comenzará creando el Schedule Group al que pertenecerán todos los Schedulers que se calendarizarán.

<ins>Código para crear Schedule Group:</ins>

```csharp
using Amazon.CDK.AWS.Scheduler;

CfnScheduleGroup scheduleGroup = new(this, ..., new CfnScheduleGroupProps {
    Name = ...
});
```

#### Dead Letter Queue

Se creará también la DLQ que usarán todos los Schedulers en caso de que tengan problemas para gatillar la [Lambda Dispatcher](#lambda-dispatcher).

<ins>Código para crear DLQ:</ins>

```csharp
using Amazon.CDK.AWS.SQS;

// Creación de cola...
Queue schedulerDlq = new(this, ..., new QueueProps {
    QueueName = ...,
    RetentionPeriod = Duration.Days(14),
    EnforceSSL = true,
});
```

#### SNS Topic y CloudWatch Alarm

Por otro lado, también se creará el Topic y Alarm para notificar al equipo productivo en caso de que lleguen mensajes al DLQ de los Schedulers.

<ins>Código para crear SNS Topic y CloudWatch Alarm:</ins>

```csharp
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;

// Se crea SNS topic para notificaciones asociadas a la instancia...
Topic topicScheduleDlq = new(this, ..., new TopicProps {
    TopicName = ...,
});

foreach (string email in notificationEmails.Split(",")) {
    topicScheduleDlq.AddSubscription(new EmailSubscription(email));
}

// Se crea alarma para enviar notificación cuando llegue un elemento al DLQ...
Alarm alarmScheduleDlq = new(this, ..., new AlarmProps {
    AlarmName = ...,
    AlarmDescription = ...,
    Metric = schedulerDlq.MetricApproximateNumberOfMessagesVisible(new MetricOptions {
        Period = Duration.Minutes(5),
        Statistic = Stats.MAXIMUM,
    }),
    Threshold = 1,
    EvaluationPeriods = 1,
    DatapointsToAlarm = 1,
    ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
    TreatMissingData = TreatMissingData.NOT_BREACHING,
});
alarmScheduleDlq.AddAlarmAction(new SnsAction(topicScheduleDlq));
```

#### IAM Role

También, se crea el Role que usarán todos los Schedulers.

<ins>Código para crear Role:</ins>

```csharp
// Creación de role usado por Scheduler para gatillar dispatcher lambda...
Role roleScheduler = new(this, ..., new RoleProps {
    RoleName = ...,
    Description = ...,
    AssumedBy = new ServicePrincipal("scheduler.amazonaws.com"),
    InlinePolicies = new Dictionary<string, PolicyDocument> {
        {
            ...,
            new PolicyDocument(new PolicyDocumentProps {
                Statements = [
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
                        Actions = [
                            "lambda:InvokeFunction"
                        ],
                        Resources = [
                            dispatcherFunction.FunctionArn
                        ],
                    }),
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
                        Actions = [
                            "sqs:SendMessage"
                        ],
                        Resources = [
                            schedulerDlq.QueueArn
                        ],
                    })
                ]
            })
        }
    }
});
```

> [!NOTE]
> Destacar que el Role tiene acceso para invocar la [Lambda Dispatcher](#lambda-dispatcher), al igual como permiso para ingresar mensajes en la [DLQ de Schedulers](#dead-letter-queue).

#### Systems Manager String Parameter

Por último, se registra los parámetros que almacenarán el nombre del Schedule Group, el ARN de la DLQ y Role para los Schedulers. Todos estos parámetros serán usados por la [API de Calendarización](#api-calendarizar-procesos) para crear los nuevos Schedulers.

<ins>Código para crear String Parameter:</ins>

```csharp
using Amazon.CDK.AWS.SSM;

StringParameter stringParameterScheduleGroup = new(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/Schedule/NombreGrupo",
    Description = ...,
    StringValue = scheduleGroup.Name,
    Tier = ParameterTier.STANDARD,
});

StringParameter stringParameterScheduleDlq = new(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/Schedule/DeadLetterQueueArn",
    Description = ...,
    StringValue = schedulerDlq.QueueArn,
    Tier = ParameterTier.STANDARD,
});

StringParameter stringParameterRoleScheduler = new(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/Schedule/RoleArn",
    Description = ...,
    StringValue = roleScheduler.RoleArn,
    Tier = ParameterTier.STANDARD,
});
```

### API Calendarizar Procesos

En último lugar, se creará la API que calendarizará la ejecución de procesos. Esta API creará el Scheduler si no existiese uno y registrará el proceso para su ejecución en éste.

#### Log Group e IAM Role

Se comienza creando los recursos base de una Lambda, es decir, el Log Group y el Role.

<ins>Código para crear Log Group y Role:</ins>

```csharp
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.IAM;

// Creación de log group lambda...
LogGroup logGroup = new(this, ..., new LogGroupProps {
    LogGroupName = ...,
    RemovalPolicy = RemovalPolicy.DESTROY
});

// Creación de role para la función lambda...
Role roleLambda = new(this, ..., new RoleProps {
    RoleName = ...,
    Description = ...,
    AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
    ManagedPolicies = [
        ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
        ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
    ],
    InlinePolicies = new Dictionary<string, PolicyDocument> {
        {
            ...,
            new PolicyDocument(new PolicyDocumentProps {
                Statements = [
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
                        Actions = [
                            "ssm:GetParameter"
                        ],
                        Resources = [
                            stringParameterDynamoProcesos.ParameterArn,
                            stringParameterDynamoCalendarizaciones.ParameterArn,
                            stringParameterRoleScheduler.ParameterArn,
                            stringParameterDispatcherFunction.ParameterArn,
                            stringParameterScheduleGroup.ParameterArn,
                            stringParameterScheduleDlq.ParameterArn,
                        ],
                    }),
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
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
                        Sid = ...,
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
                        Sid = ...,
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
```

> [!NOTE]
> Destacar que el Role requiere acceso para crear, eliminar y consultar los Schedulers que pertenezcan al [Schedule Group](#schedule-group), acceso para insertar, eliminar y consultar registros en ambas [tablas DynamoDB](#dynamodb-tables-y-global-secondary-index), acceso para asignar el [Role a los Schedulers](#iam-role), y acceso a los parámetros relacionados. 

#### Lambda Function

Se continua con la creación de la función Lambda correspondiente a la Minimal API AoT que calendarizará los procesos. Para entender como se publica y compila el código, dirigirse a la sección de [Despliegue](#despliegue).

<ins>Código para crear Lambda Function:</ins>

```csharp
using Amazon.CDK.AWS.Lambda;

// Creación de la función lambda...
Function function = new(this, ..., new FunctionProps {
    FunctionName = ...,
    Description = ...,
    Runtime = Runtime.DOTNET_8,
    Handler = ...,
    Code = Code.FromAsset($"{...}/publish/publish.zip"),
    Timeout = Duration.Seconds(double.Parse(...)),
    MemorySize = double.Parse(...),
    Architecture = Architecture.X86_64,
    LogGroup = logGroup,
    Environment = new Dictionary<string, string> {
        { "APP_NAME", ... },
    },
    Role = roleLambda,
});
```

#### Access Log Group

Por otro lado, se crea el access log group a usar por API Gateway.

<ins>Código para crear Log Group:</ins>

```csharp
using Amazon.CDK.AWS.CloudWatch;

// Creación de access logs...
LogGroup logGroupAccessLogs = new(this, ..., new LogGroupProps {
    LogGroupName = ...,
    Retention = RetentionDays.ONE_MONTH,
    RemovalPolicy = RemovalPolicy.DESTROY
});
```

#### Lambda Rest API

Se configura los endpoints de API Gateway con LambdaRestApi. En esta parte es donde se configura el uso de API Key para autenticación.

<ins>Código para crear Lambda Rest API:</ins>

```csharp
using Amazon.CDK.AWS.APIGateway;

// Creación de la LambdaRestApi...
LambdaRestApi lambdaRestApi = new(this, ..., new LambdaRestApiProps {
    RestApiName = ...,
    Handler = function,
    DeployOptions = new StageOptions {
        AccessLogDestination = new LogGroupLogDestination(logGroupAccessLogs),
        AccessLogFormat = AccessLogFormat.Custom("'{\"requestTime\":\"$context.requestTime\",\"requestId\":\"$context.requestId\",\"httpMethod\":\"$context.httpMethod\",\"path\":\"$context.path\",\"resourcePath\":\"$context.resourcePath\",\"status\":$context.status,\"responseLatency\":$context.responseLatency,\"xrayTraceId\":\"$context.xrayTraceId\",\"integrationRequestId\":\"$context.integration.requestId\",\"functionResponseStatus\":\"$context.integration.status\",\"integrationLatency\":\"$context.integration.latency\",\"integrationServiceStatus\":\"$context.integration.integrationStatus\",\"authorizeStatus\":\"$context.authorize.status\",\"authorizerStatus\":\"$context.authorizer.status\",\"authorizerLatency\":\"$context.authorizer.latency\",\"authorizerRequestId\":\"$context.authorizer.requestId\",\"ip\":\"$context.identity.sourceIp\",\"userAgent\":\"$context.identity.userAgent\",\"principalId\":\"$context.authorizer.principalId\"}'"),
        StageName = ...,
        Description = ...,
    },
    DefaultMethodOptions = new MethodOptions {
        ApiKeyRequired = true,                   
    },
});
```

> [!NOTE]
> Si deseas conocer más detalles del formato usado para los access logs, recomiendo la lectura de ["The Missing Guide to AWS API Gateway Access Logs"](https://www.alexdebrie.com/posts/api-gateway-access-logs/) por Alex DeBrie.

#### API Mapping

Se crea API Mapping para redireccionar solicitudes desde el Custom Domain de API Gateway a la API en cuestión.

<ins>Código para crear API Mapping:</ins>

```csharp
using Amazon.CDK.AWS.Apigatewayv2;

// Creación de la CfnApiMapping para el API Gateway...
CfnApiMapping apiMapping = new(this, ..., new CfnApiMappingProps {
    DomainName = ...,
    ApiMappingKey = ...,
    ApiId = lambdaRestApi.RestApiId,
    Stage = lambdaRestApi.DeploymentStage.StageName,
});
```

#### Usage Plan y API Key

Se crea Usage Plan y API Key para restringir el consumo del servicio.

<ins>Código para crear Usage Plan y API Key:</ins>

```csharp
using Amazon.CDK.AWS.APIGateway;

// Se crea Usage Plan para configurar API Key...
UsagePlan usagePlan = new(this, ..., new UsagePlanProps {
    Name = ...,
    Description = ...,
    ApiStages = [
        new UsagePlanPerApiStage() {
            Api = lambdaRestApi,
            Stage = lambdaRestApi.DeploymentStage
        }
    ],
});

// Se crea API Key...
ApiKey apiGatewayKey = new(this, ..., new ApiKeyProps {
    ApiKeyName = ...,
    Description = ...,
});
usagePlan.AddApiKey(apiGatewayKey);
```

#### API Gateway Permission

Se crea el permiso que permitirá a API Gateway invocar a la Lambda.

<ins>Código para crear API Gateway Permission:</ins>

```csharp
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;

// Se configura permisos para la ejecucíon de la Lambda desde el API Gateway...
ArnPrincipal arnPrincipal = new("apigateway.amazonaws.com");
Permission permission = new() {
    Scope = this,
    Action = "lambda:InvokeFunction",
    Principal = arnPrincipal,
    SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{lambdaRestApi.RestApiId}/*/*/*",
};
function.AddPermission(..., permission);
```

#### Systems Manager String Parameter

Se crean String Parameters para contener la URL y el ID de la API Key de la Minimal API AoT.

<ins>Código para crear String Parameter:</ins>

```csharp
using Amazon.CDK.AWS.SSM;

// Se configuran parámetros para ser rescatados por consumidores...
_ = new StringParameter(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/Api/Url",
    Description = ...,
    StringValue = $"https://{apiMapping.DomainName}/{apiMapping.ApiMappingKey}/",
    Tier = ParameterTier.STANDARD,
});

_ = new StringParameter(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/Api/KeyId",
    Description = ...,
    StringValue = $"{apiGatewayKey.KeyId}",
    Tier = ParameterTier.STANDARD,
});
```

## Lógica de Lambdas

### API para Calendarizar Procesos

El principal proposito de la API es calendarizar o descalendarizar la ejecución de procesos, recepcionando la información del proceso, registrandolo y creando el schedule asociado si no existe. Para esto, la API contiene los siguientes endpoints:

#### Endpoints
<table>
<tr>
<th>URL</th>
<th>Método</th>
<th>Cuerpo</th>
<th>Retorno</th>
</tr>
<tr>
<td>

`/Procesos`

</td>
<td>

`POST`

</td>
<td>

```json
{
    "nombre": "...",
    "cron": "...",
    "arnRol": "...",
    "arnProceso": "...",
    "parametros": "...",
    "habilitado": true|false
}
```

</td>
<td>

```json
{
    "IdProceso": "...",
    "IdCalendarizacion": "...",
    "Nombre": "...",
    "ArnRol": "...",
    "ArnProceso": "...",
    "Parametros": "...",
    "Habilitado": true|false,
    "FechaCreacion": ""
}
```

</td>
</tr>
<tr>
<td>

`/Procesos/{idProceso}`

</td>
<td>

`DELETE`

</td>
<td>

`Sin Cuerpo`

</td>
<td>

`Sin Retorno`

</td>
</tr>
</table>

#### Código Endpoint POST

```csharp
routes.MapPost("/", async (EntIngresarProceso entrada, VariableEntornoHelper variableEntorno, ParameterStoreHelper parameterStore, SchedulerHelper scheduler, DynamoHelper dynamo) => {
    Stopwatch stopwatch = Stopwatch.StartNew();

    try {
        // Se valida si existe el schedule, si no existe entonces se crea y registra en dynamoDB...
        string idCalendarizacion = $"calendarizacion-{Convert.ToBase64String(Encoding.UTF8.GetBytes(entrada.Cron)).Replace("+", "-").Replace("/", "_").Replace("=", ".")}";
        Schedule? scheduleExistente = await scheduler.Obtener(idCalendarizacion, nombreScheduleGroup);
        if (scheduleExistente == null) {
            scheduleExistente = await scheduler.Crear(
                idCalendarizacion, 
                ..., 
                nombreScheduleGroup, 
                entrada.Cron,
                arnRoleSchedule,
                arnDlqSchedule,
                arnLambdaDispatcher,
                JsonSerializer.Serialize(new DispatcherInput { 
                    IdCalendarizacion = idCalendarizacion
                }, AppJsonSerializerContext.Default.DispatcherInput)
            );
            if (scheduleExistente != null) {
                await dynamo.Insertar(nombreTablaCalendarizaciones, new Dictionary<string, object?> {
                    ["IdCalendarizacion"] = scheduleExistente.Nombre,
                    ["Nombre"] = scheduleExistente.Nombre,
                    ["Descripcion"] = scheduleExistente.Descripcion,
                    ["Grupo"] = scheduleExistente.Grupo,
                    ["Cron"] = scheduleExistente.Cron,
                    ["Arn"] = scheduleExistente.Arn,
                    ["FechaCreacion"] = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
                });
            }
        }

        // Se valida si ya existe el proceso en dynamoDB, si no existe entonces se registra...
        string idProceso = $"proceso-{Convert.ToBase64String(Encoding.UTF8.GetBytes(entrada.Nombre)).Replace("+", "-").Replace("/", "_").Replace("=", ".")}";
        Dictionary<string, object?>? procesoExistente = await dynamo.Obtener(nombreTablaProcesos, new Dictionary<string, object?> {
            ["IdProceso"] = idProceso
        });
        procesoExistente ??= await dynamo.Insertar(nombreTablaProcesos, new Dictionary<string, object?> {
            ["IdProceso"] = idProceso,
            ["IdCalendarizacion"] = idCalendarizacion,
            ["Nombre"] = entrada.Nombre,
            ["ArnRol"] = entrada.ArnRol,
            ["ArnProceso"] = entrada.ArnProceso,
            ["Parametros"] = entrada.Parametros,
            ["Habilitado"] = entrada.Habilitado,
            ["FechaCreacion"] = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
        });

        return Results.Ok(procesoExistente);
    } catch (Exception ex) {
        return Results.Problem(...);
    }
});
```
#### Código Endpoint DELETE

```csharp
routes.MapDelete("/{idProceso}", async (string idProceso, VariableEntornoHelper variableEntorno, ParameterStoreHelper parameterStore, SchedulerHelper scheduler, DynamoHelper dynamo) => {
    Stopwatch stopwatch = Stopwatch.StartNew();

    try {
        // Se obtiene el proceso a eliminar...
        Dictionary<string, object?>? proceso = await dynamo.Obtener(nombreTablaProcesos, new Dictionary<string, object?> {
            ["IdProceso"] = idProceso
        });

        // Si el proceso existe, se elimina...
        if (proceso != null && proceso.TryGetValue("IdProceso", out object? value)) {

            // Antes de eliminar el proceso, si no quedan otros procesos en la calendarización, tambien se elimina ésta...
            if (proceso.TryGetValue("IdCalendarizacion", out object? idCalendarizacion) && idCalendarizacion != null) {
                List<Dictionary<string, object?>> procesos = await dynamo.ObtenerPorIndice(
                    nombreTablaProcesos,
                    "PorIdCalendarizacion",
                    "IdCalendarizacion",
                    (string)idCalendarizacion
                );

                if (procesos.Count == 1) {
                    // Si existe el schedule de eventbridge, se elimina...
                    Schedule? schedule = await scheduler.Obtener((string)idCalendarizacion, nombreScheduleGroup);
                    if (schedule != null) {
                        await scheduler.Eliminar(schedule.Nombre, schedule.Grupo);
                    }

                    // Si existe el registro en dynamo, se elimina...
                    Dictionary<string, object?>? calendarizacion = await dynamo.Obtener(nombreTablaCalendarizaciones, new Dictionary<string, object?> {
                        ["IdCalendarizacion"] = idCalendarizacion
                    });
                    if (calendarizacion != null) {
                        await dynamo.Eliminar(nombreTablaCalendarizaciones, new Dictionary<string, object?> {
                            ["IdCalendarizacion"] = idCalendarizacion
                        });
                    }
                } 
            }

            await dynamo.Eliminar(nombreTablaProcesos, new Dictionary<string, object?> {
                ["IdProceso"] = value
            });
        }

        return Results.Ok();
    } catch (Exception ex) {
        return Results.Problem(...);
    }
});
```

### Lambda Dispatcher

El principal proposito de la Lambda Dispatcher es ingresar a la cola todos los procesos relacionados con una calendarización en particular para su ejecución.

#### Código

```csharp
public async Task FunctionHandler(DispatcherInput input, ILambdaContext context) {
        Stopwatch stopwatch = Stopwatch.StartNew();

        List<Dictionary<string, object?>> procesos = await dynamoHelper.ObtenerPorIndice(nombreTablaProcesos, "PorIdCalendarizacion", "IdCalendarizacion", input.IdCalendarizacion);

        foreach (Dictionary<string, object?> proceso in procesos) {
            try {
                if (!proceso.ContainsKey("Habilitado") || proceso["Habilitado"] == null || !(bool)proceso["Habilitado"]!) {
                    continue;
                }

                SendMessageRequest request = new() {
                    QueueUrl = sqsQueueUrl,
                    MessageBody = JsonSerializer.Serialize(proceso)
                };

                SendMessageResponse response = await sqsClient.SendMessageAsync(request);
            } catch(Exception ex) {

            }
        }
    }
```

### Lambda Executor

El principal proposito de la Lambda Executor es gatillar la ejecución de los distintos procesos que se encuentran en la cola.

#### Código

```csharp
public async Task<SQSBatchResponse> FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        List<BatchItemFailure> listaMensajesError = [];

        Stopwatch stopwatch = Stopwatch.StartNew();

        foreach (SQSMessage mensaje in evnt.Records) {
            try { 
                Dictionary<string, object?> proceso = JsonConvert.DeserializeObject<Dictionary<string, object?>>(mensaje.Body)!;

                if (proceso.TryGetValue("ArnRol", out object? arnRole) && proceso.TryGetValue("ArnProceso", out object? arnProceso) && proceso.TryGetValue("Parametros", out object? parametros)) {
                    // Si no viene el ARN del role o del proceso a gatillar, se omite la ejecución...
                    if (arnRole == null || arnProceso == null) {
                        throw new Exception(...);
                    }

                    // Se asume el rol para ejecutar el proceso...
                    AssumeRoleRequest requestAssumeRole = new() {
                        RoleSessionName = $"{...}-Execute-Session",
                        RoleArn = (string)arnRole
                    };
                    AssumeRoleResponse responseAssumeRole = await securityTokenClient.AssumeRoleAsync(requestAssumeRole);

                    // Se manda a procesar el mensaje...
                    if (((string)arnProceso).StartsWith("arn:aws:lambda:")) {
                        AmazonLambdaClient lambdaClient = new(
                            responseAssumeRole.Credentials.AccessKeyId,
                            responseAssumeRole.Credentials.SecretAccessKey,
                            responseAssumeRole.Credentials.SessionToken
                        );

                        InvokeRequest request = new() {
                            FunctionName = (string)arnProceso,
                            InvocationType = InvocationType.Event,
                        };
                        if (parametros != null) request.Payload = (string)parametros;

                        await lambdaClient.InvokeAsync(request);
                    } else if (((string)arnProceso).StartsWith("arn:aws:states:")) {    
                        AmazonStepFunctionsClient stepFunctionClient = new(
                            responseAssumeRole.Credentials.AccessKeyId,
                            responseAssumeRole.Credentials.SecretAccessKey,
                            responseAssumeRole.Credentials.SessionToken
                        );

                        StartExecutionRequest request = new() {
                            StateMachineArn = (string)arnProceso,
                        };
                        if (parametros != null) request.Input = (string)parametros;

                        await stepFunctionClient.StartExecutionAsync(request);
                    } else if (((string)arnProceso).StartsWith("arn:aws:sns:")) {
                        AmazonSimpleNotificationServiceClient snsClient = new(
                            responseAssumeRole.Credentials.AccessKeyId,
                            responseAssumeRole.Credentials.SecretAccessKey,
                            responseAssumeRole.Credentials.SessionToken
                        );

                        PublishRequest request = new() {
                            TopicArn = (string)arnProceso
                        };
                        if (parametros != null) request.Message = (string)parametros;

                        await snsClient.PublishAsync(request);
                    } else if (((string)arnProceso).StartsWith("arn:aws:sqs:")) {
                        AmazonSQSClient sqsAssumeClient = new(
                            responseAssumeRole.Credentials.AccessKeyId,
                            responseAssumeRole.Credentials.SecretAccessKey,
                            responseAssumeRole.Credentials.SessionToken
                        );

                        string[] arnParts = ((string)arnProceso).Split(':');

                        SendMessageRequest request = new() {
                            QueueUrl = $"https://sqs.{arnParts[3]}.amazonaws.com/{arnParts[4]}/{arnParts[5]}"
                        };
                        if (parametros != null) request.MessageBody = (string)parametros;

                        await sqsAssumeClient.SendMessageAsync(request);
                    } else {
                        throw new NotSupportedException(...);
                    }
                } else {
                    throw new Exception(...);
                }
            } catch(Exception ex) {
                listaMensajesError.Add(new BatchItemFailure {
                    ItemIdentifier = mensaje.MessageId,
                });
            }
        }

        return new SQSBatchResponse {
            BatchItemFailures = listaMensajesError
        };
    }
```

## Despliegue

El despliegue se lleva a cabo mediante GitHub Actions, para ello se configura la receta de despliegue con los siguientes pasos:

| Paso | Comando | Descripción |
|------|---------|-------------|
| Checkout Repositorio | `actions/checkout@v4` | Se descarga el repositorio en runner. |
| Instalar .NET | `actions/setup-dotnet@v4` | Se instala .NET en el runner. |
| Instalar Node.js | `actions/setup-node@v4` | Se instala Node.js en el runner. | 
| Instalar AWS CDK | `npm install -g aws-cdk` | Se instala aws-cdk con NPM. |
| Publish .NET AoT Minimal API | `docker run --rm -v ...:/src -w /src .../amazonlinux:2023 \bash -c "`<br> `yum install -y dotnet-sdk-8.0 gcc zlib-devel &&`<br> `dotnet publish /p:PublishAot=true -r linux-x64 --self-contained &&`<br> `cd ./publish &&`<br> `zip -r -T ./publish.zip ./*"`| Se publica y comprime el proyecto de la API AoT.<br> Por ser AoT, se publica usando docker con la imagen de Amazon Linux 2023. |
| Publish .NET Lambda | `dotnet publish /p:PublishReadyToRun=true -r linux-x64 --no-self-contained` | Se publica el proyecto de la Lambda Dispatcher |
| Compress Publish Directory .NET Lambda | `zip -r -T ./publish.zip ./*` | Se comprime la publicación de la Lambda Dispatcher |
| Publish .NET Lambda | `dotnet publish /p:PublishReadyToRun=true -r linux-x64 --no-self-contained` | Se publica el proyecto de la Lambda Executor |
| Compress Publish Directory .NET Lambda | `zip -r -T ./publish.zip ./*` | Se comprime la publicación de la Lambda Executor |
| Configure AWS Credentials | `aws-actions/configure-aws-credentials` | Se configuran credenciales para despliegue en AWS. |
| CDK Synth | `cdk synth` | Se sintetiza la aplicación CDK. |
| CDK Diff | `cdk --app cdk.out diff` | Se obtienen las diferencias entre nueva versión y versión desplegada. |
| CDK Deploy | `cdk --app cdk.out deploy --require-approval never` | Se despliega la aplicación CDK. |

### Variables y Secretos de Entorno

A continuación se presentan las variables que se deben configurar en el Environment para el correcto despliegue:

| Variable de Entorno | Tipo | Descripción |
|---------------------|------|-------------|
| `VERSION_DOTNET` | Variable | Versión del .NET del CDK. Por ejemplo "8". |
| `VERSION_NODEJS` | Variable | Versión de Node.js. Por ejemplo "20". |
| `ARN_GITHUB_ROLE` | Variable | ARN del Rol en IAM que se usará para el despliegue. |
| `ACCOUNT_AWS` | Variable | ID de la cuenta AWS donde desplegar. |
| `REGION_AWS` | Variable | Región primaria donde desplegar. Por ejemplo "us-west-1". |
| `DIRECTORIO_CDK` | Variable | Directorio donde se encuentra archivo cdk.json. En este caso sería ".". |
| `APP_NAME` | Variable | El nombre de la aplicación a desplegar. Por ejemplo "Kairos" |
| `AOT_MINIMAL_API_DIRECTORY` | Variable | Directorio donde se encuentra el proyecto de la Minimal API AoT. Por ejemplo "./ApiCalendarizarProcesos" |
| `AOT_MINIMAL_API_LAMBDA_HANDLER` | Variable | Handler de la Minimal API AoT. Por ejemplo "ApiCalendarizarProcesos" |
| `AOT_MINIMAL_API_LAMBDA_MEMORY_SIZE` | Variable | Cantidad de memoria para la Lambda de la Minimal API AoT. Por ejemplo "256". |
| `AOT_MINIMAL_API_LAMBDA_TIMEOUT` | Variable | Tiempo en segundos de timeout para la Lambda de la Minimal API AoT. Por ejemplo "120". |
| `AOT_MINIMAL_API_MAPPING_DOMAIN_NAME` | Variable | El Custom Domain Name de API Gateway que se usará para la Minimal API AoT. |
| `AOT_MINIMAL_API_MAPPING_KEY` | Variable | Mapping a usar en el Custom Domain de API Gateway. Por ejemplo "kairos". |
| `DISPATCHER_DIRECTORY` | Variable | Directorio donde se encuentra el proyecto de la Lambda Dispatcher. Por ejemplo "./LambdaDispatcher". |
| `DISPATCHER_LAMBDA_HANDLER` | Variable | Handler de la Lambda Dispatcher. Por ejemplo "LambdaDispatcher::LambdaDispatcher.Function::FunctionHandler". |
| `DISPATCHER_LAMBDA_MEMORY_SIZE` | Variable | Cantidad de memoria para la Lambda Dispatcher. Por ejemplo "256". |
| `DISPATCHER_LAMBDA_TIMEOUT` | Variable | Tiempo en segundos de timeout para la Lambda Dispatcher. Por ejemplo "900". |
| `EXECUTOR_DIRECTORY` | Variable | Directorio donde se encuentra el proyecto de la Lambda Executor. Por ejemplo "./LambdaExecutor". |
| `EXECUTOR_LAMBDA_HANDLER` | Variable | Handler de la Lambda Executor. Por ejemplo "LambdaExecutor::LambdaExecutor.Function::FunctionHandler". |
| `EXECUTOR_LAMBDA_MEMORY_SIZE` | Variable | Cantidad de memoria para la Lambda Executor. Por ejemplo "256". |
| `EXECUTOR_LAMBDA_TIMEOUT` | Variable | Tiempo en segundos de timeout para la Lambda Executor. Por ejemplo "120". |
| `EXECUTOR_LAMBDA_PREFIX_ROLES` | Variable | Prefijo que deberán tener los roles a usar por Lambda Executor para la ejecución de procesos. Por ejemplo "KairosExecutor-". |
| `NOTIFICATION_EMAILS` | Variable | Emails a los que notificar cuando mensajes lleguen a los DLQ (separados por ","). Por ejemplo "correo01@ejemplo.cl,correo02@ejemplo.cl". |