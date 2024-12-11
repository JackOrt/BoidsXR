using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(SwarmManager))]
public class SwarmManagerEditor : Editor
{
    SerializedProperty swarmsProp;
    SwarmManager manager;

    // Variables for adding food attractor
    Vector3 foodAttractorPosition = Vector3.zero;

    private void OnEnable()
    {
        swarmsProp = serializedObject.FindProperty("swarms");
        manager = (SwarmManager)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Swarm Management", EditorStyles.boldLabel);

        // Dropdown to select swarm
        int selectedSwarmIndex = 0;
        if (manager.swarms.Count > 0)
        {
            string[] swarmNames = new string[manager.swarms.Count];
            for (int i = 0; i < manager.swarms.Count; i++)
            {
                swarmNames[i] = $"Swarm {manager.swarms[i].swarmID} ({manager.swarms[i].type})";
            }
            selectedSwarmIndex = EditorGUILayout.Popup("Select Swarm", selectedSwarmIndex, swarmNames);
        }
        else
        {
            EditorGUILayout.LabelField("No swarms available.");
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Boid"))
        {
            if (manager.swarms.Count > 0)
            {
                manager.AddBoidToSwarm(manager.swarms[selectedSwarmIndex].swarmID);
            }
        }

        if (GUILayout.Button("Remove Boid"))
        {
            if (manager.swarms.Count > 0)
            {
                manager.RemoveBoidFromSwarm(manager.swarms[selectedSwarmIndex].swarmID);
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Adjust Swarm Parameters", EditorStyles.boldLabel);

        if (manager.swarms.Count > 0)
        {
            Swarm selectedSwarm = manager.swarms[selectedSwarmIndex];

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Swarm {selectedSwarm.swarmID} Parameters", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            selectedSwarm.parameters.separationWeight = EditorGUILayout.FloatField("Separation Weight", selectedSwarm.parameters.separationWeight);
            selectedSwarm.parameters.alignmentWeight = EditorGUILayout.FloatField("Alignment Weight", selectedSwarm.parameters.alignmentWeight);
            selectedSwarm.parameters.cohesionWeight = EditorGUILayout.FloatField("Cohesion Weight", selectedSwarm.parameters.cohesionWeight);
            selectedSwarm.parameters.obstacleAvoidanceWeight = EditorGUILayout.FloatField("Obstacle Avoidance Weight", selectedSwarm.parameters.obstacleAvoidanceWeight);
            selectedSwarm.parameters.boundaryStrength = EditorGUILayout.FloatField("Boundary Strength", selectedSwarm.parameters.boundaryStrength);
            selectedSwarm.parameters.boundaryAvoidDistance = EditorGUILayout.FloatField("Boundary Avoid Distance", selectedSwarm.parameters.boundaryAvoidDistance);
            selectedSwarm.parameters.obstacleAvoidDistance = EditorGUILayout.FloatField("Obstacle Avoid Distance", selectedSwarm.parameters.obstacleAvoidDistance);

            selectedSwarm.speed = EditorGUILayout.FloatField("Speed", selectedSwarm.speed);

            selectedSwarm.rotation = EditorGUILayout.Vector3Field("Mesh Rotation (Euler)", selectedSwarm.rotation);
            selectedSwarm.scale = EditorGUILayout.Vector3Field("Mesh Scale", selectedSwarm.scale);

            if (EditorGUI.EndChangeCheck())
            {
                // Apply changes
                EditorUtility.SetDirty(manager);
                manager.AdjustSwarmParameters(selectedSwarm.swarmID, selectedSwarm.parameters);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Add Food Attractor", EditorStyles.boldLabel);

        foodAttractorPosition = EditorGUILayout.Vector3Field("Position", foodAttractorPosition);

        if (GUILayout.Button("Add Food Attractor"))
        {
            manager.AddFoodAttractor(foodAttractorPosition);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
