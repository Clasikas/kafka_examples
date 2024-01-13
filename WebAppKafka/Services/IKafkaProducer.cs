namespace WebAppKafka.Services
{
    public interface IKafkaProducer
    {
        void SendBulkJson<T>(string targetTopic, IEnumerable<T> messages, string key, Dictionary<string, string> headers);
        Task SendJsonAsync(string targetTopic, object message, string messageKey, Dictionary<string, string> headers);
    }
}
