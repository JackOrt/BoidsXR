using UnityEngine;
using System.Collections;

// Make sure you have a tag "Obstacle" created in Unity's Tag Manager
[RequireComponent(typeof(OVRSceneManager))]
public class Quest3SceneMeshLoader : MonoBehaviour
{
    private OVRSceneManager sceneManager;

    private void Awake()
    {
        sceneManager = GetComponent<OVRSceneManager>();
        
        // Optional: If sceneManager not found as a component on the same gameObject
        // you can manually find it:
        // sceneManager = FindObjectOfType<OVRSceneManager>();
    }

    void Start()
    {
        // Ensure OVRSceneManager is initialized and attempts to load the environment scene
        // Scene Understanding feature must be enabled in the Oculus Project Settings.

        // Once here, the scene should be loaded. We can now find the environment mesh.
        LoadSceneMeshAsObstacle();
    }

    private void LoadSceneMeshAsObstacle()
    {
        // OVRSceneManager, once loaded, provides access to OVRSceneAnchors and potentially an OVRSceneModel
        OVRSceneAnchor[] anchors = FindObjectsOfType<OVRSceneAnchor>();

        bool meshFound = false;

        foreach (var anchor in anchors)
        {
            // Check if this anchor has a MeshFilter and MeshRenderer (indicating a mesh object)
            // Typically, the environment scene mesh should come in as OVRSceneModel or anchors with a mesh
            MeshFilter mf = anchor.GetComponentInChildren<MeshFilter>();
            MeshRenderer mr = anchor.GetComponentInChildren<MeshRenderer>();

            if (mf != null && mr != null)
            {
                // This likely is a piece of the environment scene mesh
                // Tag it as "Obstacle"
                anchor.gameObject.tag = "Obstacle";

                meshFound = true;
                Debug.Log("Environment mesh found and tagged as Obstacle: " + anchor.gameObject.name);
            }
        }

        if (!meshFound)
        {
            Debug.Log("No environment mesh found. Make sure Scene Understanding is enabled and environment is scanned.");
        }
    }
}
