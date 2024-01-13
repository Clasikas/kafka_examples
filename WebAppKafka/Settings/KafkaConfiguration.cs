namespace WebAppKafka.Settings
{
    public class KafkaConfiguration
    {
        public string GroupId { get; set; }
        public string Debug { get; set; }
        public string TopicName { get; set; }
        public string BootstrapServers { get; set; }
    }
}
