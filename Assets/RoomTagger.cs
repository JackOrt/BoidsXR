using UnityEngine;
using System.Collections.Generic; // Required for HashSet

public class RoomTagger : MonoBehaviour
{
    // Keep track of already processed game objects
    private HashSet<GameObject> processedRooms = new HashSet<GameObject>();

    void Awake()
    {
        // Find all game objects that start with "Room -"
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.StartsWith("Room -") && !processedRooms.Contains(obj))
            {
                // Tag all children recursively
                TagChildrenRecursively(obj.transform, "Obstacle");

                // Add to processed list
                processedRooms.Add(obj);
            }
        }
    }

    private void TagChildrenRecursively(Transform parent, string tag)
    {
        foreach (Transform child in parent)
        {
            child.gameObject.tag = tag;
            // Recursively tag all children's children
            TagChildrenRecursively(child, tag);
        }
    }
}
