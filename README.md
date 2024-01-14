# kafka_examples
simple kafka Producer/Consumer implementation with docker-compose


## Requirements

Check if installed docker-desktop on your local PC (docker settings -> Linux containers supports). 
Also check that docker-compose was installed on PC too.

## Start Kafka container (All-In-One)

For the simple example Producer/Consumer. it will be enough to use Kafka All-In-One (link [here](https://hub.docker.com/r/landoop/fast-data-dev))

follow steps :
- Clone repository
- open terminal and go to cloned repository.
- execute command `docker-compose up`
- when docker image will be pulled and docker container created, open browser and type `http://localhost:3040/` 

## Run WebAPi 

Check if you have installed last version of [Visual Studio 2022](https://visualstudio.microsoft.com/vs/#download)

follow steps :
- find  and open via Visual Studio 
- build project and run (F5) 

### Basic Producer Examples

```csharp
using Ardalis.GuardClauses;
using Confluent.Kafka;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;

namespace WebAppKafka.Services
{
    public class JsonKafkaProducer : IKafkaProducer
    {
        private readonly ILogger<JsonKafkaProducer> logger;
        IProducer<string, string> kafkaHandle;

        public JsonKafkaProducer(KafkaClientHandle handle, ILogger<JsonKafkaProducer> logger)
        {
            this.logger = logger;
            kafkaHandle = new DependentProducerBuilder<string, string>(handle.Handle)
                    .SetKeySerializer(Serializers.Utf8)
                    .SetValueSerializer(Serializers.Utf8)
                .Build();
        }

        private Message<string, string> CreateMessage(object value, Dictionary<string, string> headers, string messagekey)
        {
            var serializerSettings = new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture
            };
            string jsonPayload = JsonConvert.SerializeObject(value, serializerSettings);

            Message<string, string> kafkaMessage = new()
            {
                Value = jsonPayload
            };

            //Attaching a key to messages will ensure messages with the same key always go to the same partition in a topic.
            //Kafka guarantees order within a partition, but not across partitions in a topic, so alternatively not providing a key
            if (!Equals(messagekey, default(string)))
            {
                kafkaMessage.Key = messagekey;
            }

            if (headers != null)
            {
                kafkaMessage.Headers = new Headers();
                foreach (var item in headers)
                {
                    if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value))
                    {
                        continue;
                    }
                    kafkaMessage.Headers.Add(item.Key, Encoding.UTF8.GetBytes(item.Value));
                }
            }

            return kafkaMessage;
        }

        public void SendBulkJson<T>(string targetTopic, IEnumerable<T> messages, string messageKey, Dictionary<string, string> headers)
        {
            try
            {
                Guard.Against.NullOrEmpty(targetTopic, nameof(targetTopic));
                Guard.Against.Null(messages, nameof(messages));

                foreach (var message in messages)
                {
                    var jsonMessage = CreateMessage(message, headers, messageKey);
                    kafkaHandle.Produce(targetTopic, jsonMessage, ErrorHandler);
                }

                // wait for up to X seconds for any inflight messages to be delivered.
                kafkaHandle.Flush(TimeSpan.FromSeconds(2));
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Error producing for topic {targetTopic}: {e.Message}");
            }
        }

        protected virtual void ErrorHandler(DeliveryReport<string, string> deliveryReport)
        {
            if (deliveryReport?.Status == PersistenceStatus.NotPersisted)
            {
                logger.LogError($"Message: {deliveryReport.Message.Value} Error: {deliveryReport.Error} ");
            }
        }

        public async Task SendJsonAsync(string targetTopic, object message, string messageKey, Dictionary<string, string> headers)
        {
            Guard.Against.NullOrEmpty(targetTopic, nameof(targetTopic));
            Guard.Against.Null(message, nameof(message));

            try
            {
                var kafkaMessage = CreateMessage(message, headers, messageKey);

                var deliveryResult = await kafkaHandle.ProduceAsync(targetTopic, kafkaMessage);
                logger.LogTrace(deliveryResult.Message.Value.ToString());
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Error producing for topic {targetTopic}: {e.Message}");
            }
        }
    }
}

```

### Basic Consumer Examples as part of BackgroundService

```csharp
using Ardalis.GuardClauses;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using WebAppKafka.Settings;

namespace WebAppKafka.Services
{
    public class KafkaBackgroundService : BackgroundService
    {
        private readonly ILogger<KafkaBackgroundService> logger;
        private readonly KafkaConfiguration kafkaConfiguration;

        public KafkaBackgroundService(IOptions<KafkaConfiguration> kafkaConfiguration, ILogger<KafkaBackgroundService> logger)
        {
            this.logger = logger;
            this.kafkaConfiguration = kafkaConfiguration.Value;
        }

        private IConsumer<string, string> CreateConsumer(KafkaConfiguration kafkaConfiguration)
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = kafkaConfiguration.BootstrapServers,
                GroupId = kafkaConfiguration.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                Debug = this.kafkaConfiguration.Debug
            };

            var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();

            consumer.Subscribe(kafkaConfiguration.TopicName);
            logger.LogInformation($"Subscribed to topic {kafkaConfiguration.TopicName}");

            return consumer;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = CreateConsumer(kafkaConfiguration);
            var task = Task.Run(() => ConsumeAsync(consumer, stoppingToken), stoppingToken);
            return task;
        }

        protected virtual async Task ConsumeAsync(IConsumer<string, string> consumer, CancellationToken stoppingToken)
        {
            Guard.Against.Null(consumer, nameof(consumer));
            
            // This NOP is here to free the calling thread during startup.
            // If we don't do that the startup isn't finalised (log "Application started. Press Ctrl+C to shut down.") until a first message is received.
            await Task.Delay(1);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeMessage = consumer.Consume(stoppingToken);


                    // here you can process your received message                   
                    logger.LogInformation($"receive message with key = '{consumeMessage.Key}' Value = {consumeMessage.Value}");


                    // Finally commit the event (also this is why we disabled AutoCommit, to precisely wait after the event is processed to commit it)
                    consumer.Commit(consumeMessage);
                }
                catch (OperationCanceledException)
                {
                    // Ensure the consumer leaves the group cleanly and final offsets are committed.
                    consumer.Close();
                }
                catch (ConsumeException consumeException)
                {
                    // it's possible to write mechanism to resubscribe   
                    logger.LogError(consumeException, "Unexpected Consume Exception raised");
                }
                catch (Exception e)
                {
                   logger.LogError(e, "Unknown error occured.");
                }
            }
        }
    }
}
```


