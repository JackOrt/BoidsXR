using System;
using System.Text;
using System.Threading.Tasks;

public class MQTTSubscriber
{
    private readonly string brokerUrl;
    private readonly string topic;
    private readonly Action<string> messageConsumer;
    private object client; // Abstracting the MQTT client dependency

    public MQTTSubscriber(string brokerUrl, string topic, Action<string> messageConsumer)
    {
        this.brokerUrl = brokerUrl;
        this.topic = topic;
        this.messageConsumer = messageConsumer;
    }

    public async Task Start()
    {
        try
        {
            // This assumes a custom MQTT client or adapter is implemented
            client = MQTTClientFactory.CreateClient(brokerUrl, topic, messageConsumer);

            if (client != null)
            {
                Console.WriteLine($"Connected to broker at {brokerUrl}");
                await Task.CompletedTask; // Simulated async connection
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting MQTTSubscriber: {ex.Message}");
        }
    }

    public async Task Stop()
    {
        try
        {
            if (client != null)
            {
                // Simulate disconnect logic
                client = null;
                Console.WriteLine("MQTTSubscriber disconnected from broker.");
                await Task.CompletedTask; // Simulated async disconnect
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping MQTTSubscriber: {ex.Message}");
        }
    }
}

// Custom MQTT client factory for Unity-specific projects
public static class MQTTClientFactory
{
    public static object CreateClient(string brokerUrl, string topic, Action<string> messageConsumer)
    {
        // Simulate client creation and binding for Unity-friendly scenarios
        Console.WriteLine($"Creating client for {brokerUrl} and topic {topic}");
        return new { brokerUrl, topic, messageConsumer }; // Placeholder for actual client implementation
    }
}
