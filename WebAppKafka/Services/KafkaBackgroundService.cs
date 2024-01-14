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
