using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using WebAppKafka.Entities;
using WebAppKafka.Services;
using WebAppKafka.Settings;

namespace WebAppKafka.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class KafkaController : ControllerBase
    {
        IKafkaProducer producer;
        string kafkaTopic;
        
        public KafkaController(IOptions<KafkaConfiguration> kafkaConfiguration, IKafkaProducer producer)
        {
            Guard.Against.Null(kafkaConfiguration, nameof(kafkaConfiguration));
            this.kafkaTopic = kafkaConfiguration.Value.TopicName;
            this.producer = producer;
        }

        /// <summary>
        /// Send message/messages to Kafka topic provided in config file
        /// </summary>
        /// <param name="userInstances">The User Instances to send.</param>
        /// <response code="204">The messages were sent.</response>
        [HttpPost()]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> Create([Required] IEnumerable<UserEntityRequest> userInstances)
        {
            if (userInstances.Count() == 1)
            {
                await this.producer.SendJsonAsync(this.kafkaTopic, userInstances.First(), null, null);
                return NoContent();
            }

            if (userInstances.Count() > 1)
            {
                var kafkaKey = Guid.NewGuid().ToString();
                this.producer.SendBulkJson<UserEntityRequest>(this.kafkaTopic, userInstances, kafkaKey, new Dictionary<string, string>());
            }

            return NoContent();
        }
    }
}
