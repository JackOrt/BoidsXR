using UnityEngine;

public class MQTTManager : MonoBehaviour
{
    private MQTTSubscriber subscriber;

    [Header("MQTT Settings")]
    public string brokerUrl = "127.0.0.1"; // The address of the MQTT Broker
    public string topic = "aquarium/updates"; // The topic to subscribe to

    void Start()
    {
        // Initialize the MQTTSubscriber and define the message handling logic
        subscriber = new MQTTSubscriber(brokerUrl, topic, HandleMQTTMessage);
        subscriber.Start().ConfigureAwait(false); // Start the subscriber asynchronously
    }

    void OnDestroy()
    {
        // Ensure the MQTTSubscriber is stopped when the scene is destroyed
        if (subscriber != null)
        {
            subscriber.Stop().ConfigureAwait(false); // Stop the subscriber asynchronously
        }
    }

    private void HandleMQTTMessage(string payload)
    {
        // Example: Handle the received MQTT message
        Debug.Log($"MQTT Message Received: {payload}");

        // Update the Unity scene based on the project requirements, such as modifying the swarm
        // Example:
        // SwarmManager.Instance.UpdateSwarm(payload);
    }
}
