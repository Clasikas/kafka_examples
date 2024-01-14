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
