BoidComputeShader.compute:

#pragma kernel CSMain

// Constants for swarm types
#define SwarmType_Prey 0
#define SwarmType_Predator 1
#define SwarmType_Omnivore 2
#define SwarmType_FoodSource 3

// Boid data structure
struct BoidData
{
    float3 position;
    float3 velocity;
    int swarmType;
    int swarmID;
};

// Triangle data structure
struct TriangleData
{
    float3 vertexA;
    float3 vertexB;
    float3 vertexC;
};

// Swarm data structure
struct SwarmData
{
    int swarmID;
    int type; // SwarmType as integer
    float separationWeight;
    float alignmentWeight;
    float cohesionWeight;
    float obstacleAvoidanceWeight;
    float boundaryStrength;
    float boundaryAvoidDistance;
    float4 swarmColor;
    float speed;
    float pad; // padding for alignment
};

// Instance data structure (for rendering)
struct InstanceData
{
    float4x4 transform;
    float4 color;
};

// Buffers
RWStructuredBuffer<BoidData> boidBuffer;
StructuredBuffer<TriangleData> triangleBuffer;
StructuredBuffer<SwarmData> swarmBuffer;
RWStructuredBuffer<InstanceData> instanceTransforms;

// Constants
int boidCount;
int triangleCount;
int swarmCount;
float deltaTime;
float3 managerPosition;
float3 boundsSize;

// Utility function: Ray-triangle intersection using Möller–Trumbore algorithm
bool RayIntersectsTriangle(float3 rayOrigin, float3 rayDir, float3 v0, float3 v1, float3 v2, out float t, out float3 normal)
{
    float3 edge1 = v1 - v0;
    float3 edge2 = v2 - v0;
    float3 h = cross(rayDir, edge2);
    float a = dot(edge1, h);

    if (abs(a) < 1e-5)
    {
        t = 0.0;
        normal = float3(0.0, 0.0, 0.0);
        return false;
    }

    float f = 1.0 / a;
    float3 s = rayOrigin - v0;
    float u = f * dot(s, h);
    if (u < 0.0 || u > 1.0)
    {
        t = 0.0;
        normal = float3(0.0, 0.0, 0.0);
        return false;
    }

    float3 q = cross(s, edge1);
    float v = f * dot(rayDir, q);
    if (v < 0.0 || u + v > 1.0)
    {
        t = 0.0;
        normal = float3(0.0, 0.0, 0.0);
        return false;
    }

    t = f * dot(edge2, q);
    if (t > 1e-5)
    {
        normal = normalize(cross(edge1, edge2));
        return true;
    }
    else
    {
        t = 0.0;
        normal = float3(0.0, 0.0, 0.0);
        return false;
    }
}

[numthreads(256, 1, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    uint index = id.x;
    if (index >= (uint)boidCount) return;

    BoidData boid = boidBuffer[index];
    SwarmData swarm = swarmBuffer[boid.swarmID];

    float3 separation = float3(0.0, 0.0, 0.0);
    float3 alignment = float3(0.0, 0.0, 0.0);
    float3 cohesion = float3(0.0, 0.0, 0.0);

    int neighborCount = 0;

    // For each boid, determine how to react based on swarm type
    // Behavior rules:
    // Prey: Avoid Predators & Omnivores, Seek Food
    // Predator: Seek Prey & Omnivores, Ignore Food
    // Omnivore: Avoid Predators, Seek Prey & Food
    // FoodSource: No movement needed, just static attractor

    // Determine what this boid avoids or seeks
    bool isPrey = (swarm.type == SwarmType_Prey);
    bool isPredator = (swarm.type == SwarmType_Predator);
    bool isOmnivore = (swarm.type == SwarmType_Omnivore);
    bool isFoodSource = (swarm.type == SwarmType_FoodSource);

    for (uint i = 0; i < (uint)boidCount; i++)
    {
        if (i == index) continue;

        BoidData other = boidBuffer[i];
        float3 toOther = other.position - boid.position;
        float distance = length(toOther);

        if (distance > 0.0 && distance < swarm.boundaryAvoidDistance)
        {
            bool avoid = false;
            bool seek = false;

            // Determine relationships based on types
            // Prey avoids Predators, Omnivores; seeks Food
            if (isPrey)
            {
                if (other.swarmType == SwarmType_Predator || other.swarmType == SwarmType_Omnivore)
                    avoid = true;
                if (other.swarmType == SwarmType_FoodSource)
                    seek = true;
            }
            // Predator seeks Prey and Omnivores
            else if (isPredator)
            {
                if (other.swarmType == SwarmType_Prey || other.swarmType == SwarmType_Omnivore)
                    seek = true;
            }
            // Omnivore avoids Predators, seeks Prey and Food
            else if (isOmnivore)
            {
                if (other.swarmType == SwarmType_Predator)
                    avoid = true;
                if (other.swarmType == SwarmType_Prey || other.swarmType == SwarmType_FoodSource)
                    seek = true;
            }
            // FoodSource doesn't move, no avoid/seek needed

            // Apply standard boid rules if it's another boid (not just food)
            // Food sources are static points to seek (if applicable), they do not align or cohere.
            if (other.swarmType != SwarmType_FoodSource)
            {
                // All boids still do flocking behavior with each other boid (except ignoring FoodSource in alignment/cohesion)
                // Separation: If close enough, push away
                separation -= (toOther / distance);

                // Accumulate for alignment and cohesion
                alignment += other.velocity;
                cohesion += other.position;

                neighborCount++;
            }

            // If we are seeking or avoiding (based on type relationships)
            // seek will show up indirectly via boid rules, but we can add slight nudges:
            // Actually, we rely on the standard flocking rules + the presence of these boids:
            // Avoid is handled via separation already if they are close.
            // If seeking FoodSource, just treat it as a special case below.

        }
    }

    // Compute average alignment and cohesion if neighbors were found
    if (neighborCount > 0)
    {
        alignment /= neighborCount;
        alignment = normalize(alignment);

        cohesion /= neighborCount;
        cohesion = cohesion - boid.position; // direction toward the average position
    }

    // Additionally, if this boid type seeks food and food is in the scene,
    // we can add a small attraction toward the nearest food:
    // This is optional. If you want to handle large amounts of obstacles or food,
    // you'd need a more complex approach. For simplicity, let's say we just
    // rely on the presence of food boids to act as cohesion targets.

    // Obstacle avoidance via raycasting
    float3 rayOrigin = boid.position;
    float3 rayDir = normalize(boid.velocity);

    float3 collisionNormal = float3(0.0, 0.0, 0.0);
    bool obstacleDetected = false;
    float minT = swarm.boundaryAvoidDistance;

    for (uint j = 0; j < (uint)triangleCount; j++)
    {
        TriangleData tri = triangleBuffer[j];
        float t;
        float3 normal;
        bool hit = RayIntersectsTriangle(rayOrigin, rayDir, tri.vertexA, tri.vertexB, tri.vertexC, t, normal);
        if (hit && t < minT)
        {
            minT = t;
            collisionNormal = normal;
            obstacleDetected = true;
        }
    }

    float3 obstacleAvoidance = float3(0.0,0.0,0.0);
    if (obstacleDetected)
    {
        float3 velocity = boid.velocity;
        float dotProduct = dot(velocity, collisionNormal);
        float3 parallelVelocity = velocity - collisionNormal * dotProduct;
        float3 desiredVelocity = normalize(parallelVelocity) * swarm.speed;
        float3 steering = (desiredVelocity - velocity) * swarm.obstacleAvoidanceWeight;
        obstacleAvoidance += steering;
    }

    // Boundary handling
    float3 offset = boid.position - managerPosition;
    float distanceFromCenter = length(offset);
    float3 boundaryForce = float3(0.0,0.0,0.0);
    if (distanceFromCenter > swarm.boundaryAvoidDistance)
    {
        float3 toCenter = -offset / distanceFromCenter;
        boundaryForce = toCenter * (distanceFromCenter - swarm.boundaryAvoidDistance) * swarm.boundaryStrength;
    }

    // Now combine all forces
    // Multiply each by respective weights:
    float3 acceleration = float3(0.0, 0.0, 0.0);
    acceleration += separation * swarm.separationWeight;
    acceleration += alignment * swarm.alignmentWeight;
    acceleration += cohesion * swarm.cohesionWeight;
    acceleration += obstacleAvoidance; // already scaled internally if needed
    acceleration += boundaryForce;

    // Update velocity and position
    boid.velocity += acceleration * deltaTime;
    if (length(boid.velocity) > 0.0)
    {
        boid.velocity = normalize(boid.velocity) * swarm.speed;
    }

    boid.position += boid.velocity * deltaTime;

    // Write back updated boid data
    boidBuffer[index] = boid;

    // Update instance buffer with the new transform and color
    float3 forward = normalize(boid.velocity);
    float3 up = float3(0.0, 1.0, 0.0);
    float3 right = normalize(cross(up, forward));
    up = cross(forward, right);

    float4x4 transformMatrix = float4x4(
        float4(right, 0.0),
        float4(up, 0.0),
        float4(forward, 0.0),
        float4(boid.position, 1.0)
    );

    InstanceData instance;
    instance.transform = transformMatrix;
    instance.color = swarm.swarmColor;

    instanceTransforms[index] = instance;
}


BoidShader.shader:

Shader "Custom/BoidShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                fixed4 color : COLOR;
            };

            // Instance data structure
            struct InstanceData
            {
                float4x4 transform; // 64 bytes
                float4 color;       // 16 bytes
            };

            // Instance transforms buffer
            StructuredBuffer<InstanceData> instanceTransforms;

            fixed4 _Color; // Can be removed if per-instance color is used

            // Vertex shader with instance ID
            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
               v2f o;

               // Fetch the transformation matrix and color for this instance
               InstanceData instance = instanceTransforms[instanceID];
               
               // Transpose the matrix (workaround for debugging)
               float4x4 transform = transpose(instance.transform);

               // Transform the vertex position
               float4 worldPos = mul(transform, v.vertex);
               
               // Transform to clip space
               o.pos = UnityObjectToClipPos(worldPos);
               
               // Transform the normal
               float3 worldNormal = normalize(mul((float3x3)transform, v.normal));
               o.normal = worldNormal;
               
               // Set color from instance data
               o.color = instance.color;

               return o;
            }


            fixed4 frag (v2f i) : SV_Target
            {
                // Use the instance color
                fixed4 c = i.color;
                
                // Apply simple diffuse lighting
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float diff = max(dot(i.normal, lightDir), 0.0);
                c.rgb *= diff;
                
                return c;
            }
            ENDCG
        }
    }
}


SwarmManager.cs:

using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Runtime.InteropServices;

// Ensure that the struct is laid out sequentially with 4-byte packing
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct BoidData
{
    public Vector3 position; // 12 bytes
    public Vector3 velocity; // 12 bytes
    public int swarmType;    // 4 bytes
    public int swarmID;      // 4 bytes
    // Total: 32 bytes
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct TriangleData
{
    public Vector3 vertexA; // 12 bytes
    public Vector3 vertexB; // 12 bytes
    public Vector3 vertexC; // 12 bytes
    // Total: 36 bytes
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct SwarmData
{
    public int swarmID;                 // 4 bytes
    public int type;                    // 4 bytes
    public float separationWeight;      // 4 bytes
    public float alignmentWeight;       // 4 bytes
    public float cohesionWeight;        // 4 bytes
    public float obstacleAvoidanceWeight;// 4 bytes
    public float boundaryStrength;      // 4 bytes
    public float boundaryAvoidDistance; // 4 bytes
    public Vector4 swarmColor;          // 16 bytes (replaced Color with Vector4)
    public float speed;                 // 4 bytes
    public float pad;                   // 4 bytes to make total 56 bytes
    // Total: 56 bytes
}

public enum SwarmType
{
    Prey = 0,
    Predator = 1,
    Omnivore = 2,
    FoodSource = 3
}

[System.Serializable]
public class SwarmParameters
{
    public float separationWeight = 1.5f;
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;
    public float obstacleAvoidanceWeight = 2.0f;
    public float boundaryStrength = 0.5f;
    public float boundaryAvoidDistance = 10f;
}

[System.Serializable]
public class Swarm
{
    public SwarmType type;
    public int swarmID; // Assigned automatically
    public int boidCount;
    public Mesh boidMesh;
    public Material boidMaterial;
    public SwarmParameters parameters;

    [Tooltip("Speed of the boids in this swarm")]
    public float speed = 5f; // Default speed

    [HideInInspector]
    public Vector4 swarmColor; // Changed from Color to Vector4
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct InstanceData
{
    public Matrix4x4 transform; // 64 bytes
    public Vector4 color;       // 16 bytes
    // Total: 80 bytes
}

public class SwarmManager : MonoBehaviour
{
    [Header("Swarms Configuration")]
    public List<Swarm> swarms = new List<Swarm>();

    [Header("Compute Shader")]
    public ComputeShader boidComputeShader;

    [Header("Food Source Settings")]
    public GameObject foodPrefab;
    public float foodSpawnInterval = 5f;
    public float foodLifetime = 10f;

    [Header("Rendering Bounds")]
    public Vector3 boundsSize = new Vector3(100f, 100f, 100f);

    // Compute buffers
    private ComputeBuffer boidBuffer;
    private ComputeBuffer swarmBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer instanceBuffer; // Buffer for instance transforms and colors

    // Kernel handle
    private int kernelHandle = -1;

    // List to keep track of food sources
    private List<GameObject> activeFoodSources = new List<GameObject>();

    // Cached obstacle colliders
    private Collider[] obstacleColliders;

    // MaterialPropertyBlock for setting instance buffer
    private MaterialPropertyBlock propBlock;

    void Start()
    {
        AssignSwarmIDs();
        AssignSwarmColors();

        // Ensure the compute shader is assigned
        if (boidComputeShader == null)
        {
            Debug.LogError("Compute Shader not assigned!");
            return;
        }

        // Find the kernel
        kernelHandle = boidComputeShader.FindKernel("CSMain");
        if (kernelHandle < 0)
        {
            Debug.LogError("Failed to find kernel CSMain in the compute shader.");
            return;
        }

        // Initialize MaterialPropertyBlock
        propBlock = new MaterialPropertyBlock();

        InitializeObstacleMeshes();
        InitializeSwarms();
        StartCoroutine(SpawnFoodSources());
    }

    void Update()
    {
        DispatchComputeShader();
        RenderBoids();
    }

    void OnDestroy()
    {
        ReleaseBuffers();
    }

    private void AssignSwarmIDs()
    {
        for (int i = 0; i < swarms.Count; i++)
        {
            swarms[i].swarmID = i;
        }
    }

    private void AssignSwarmColors()
    {
        foreach (var swarm in swarms)
        {
            swarm.swarmColor = GetColorForSwarmType(swarm.type);
        }
    }

    private Vector4 GetColorForSwarmType(SwarmType type)
    {
        switch (type)
        {
            case SwarmType.Prey:
                return Color.green;
            case SwarmType.Predator:
                return Color.red;
            case SwarmType.Omnivore:
                return Color.yellow;
            case SwarmType.FoodSource:
                return Color.blue;
            default:
                return Color.white;
        }
    }

    private void InitializeObstacleMeshes()
    {
        GameObject[] obstacleObjects = GameObject.FindGameObjectsWithTag("Obstacle");
        List<TriangleData> allTriangles = new List<TriangleData>();

        foreach (GameObject obstacle in obstacleObjects)
        {
            MeshFilter[] meshFilters = obstacle.GetComponentsInChildren<MeshFilter>();

            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null) continue;

                Vector3[] vertices = mesh.vertices;
                int[] indices = mesh.triangles;
                Matrix4x4 localToWorld = meshFilter.transform.localToWorldMatrix;

                for (int i = 0; i < indices.Length; i += 3)
                {
                    allTriangles.Add(new TriangleData
                    {
                        vertexA = localToWorld.MultiplyPoint3x4(vertices[indices[i + 0]]),
                        vertexB = localToWorld.MultiplyPoint3x4(vertices[indices[i + 1]]),
                        vertexC = localToWorld.MultiplyPoint3x4(vertices[indices[i + 2]])
                    });
                }
            }
        }

        UploadTriangleBuffer(allTriangles.ToArray());
        obstacleColliders = FindObjectsOfType<Collider>();
    }

    private void UploadTriangleBuffer(TriangleData[] triangles)
    {
        if (triangleBuffer != null) triangleBuffer.Release();
        triangleBuffer = new ComputeBuffer(triangles.Length, Marshal.SizeOf(typeof(TriangleData))); // 36 bytes
        triangleBuffer.SetData(triangles);
        boidComputeShader.SetBuffer(kernelHandle, "triangleBuffer", triangleBuffer);
        boidComputeShader.SetInt("triangleCount", triangles.Length);
        Debug.Log($"Uploaded {triangles.Length} triangles to the compute shader.");
    }

    private void InitializeSwarms()
    {
        List<BoidData> boidDataList = new List<BoidData>();
        List<SwarmData> swarmDataList = new List<SwarmData>();

        foreach (var swarm in swarms)
        {
            SwarmData sData = new SwarmData
            {
                swarmID = swarm.swarmID,
                type = (int)swarm.type,
                separationWeight = swarm.parameters.separationWeight,
                alignmentWeight = swarm.parameters.alignmentWeight,
                cohesionWeight = swarm.parameters.cohesionWeight,
                obstacleAvoidanceWeight = swarm.parameters.obstacleAvoidanceWeight,
                boundaryStrength = swarm.parameters.boundaryStrength,
                boundaryAvoidDistance = swarm.parameters.boundaryAvoidDistance,
                swarmColor = swarm.swarmColor,
                speed = swarm.speed, // Set speed
                pad = 0f // Initialize padding
            };
            swarmDataList.Add(sData);

            for (int i = 0; i < swarm.boidCount; i++)
            {
                BoidData bData = new BoidData
                {
                    position = GetValidSpawnPosition(),
                    velocity = -GetValidSpawnPosition() * .1f,
                    swarmType = (int)swarm.type,
                    swarmID = swarm.swarmID
                };
                boidDataList.Add(bData);
            }
        }

        // Initialize Boid Buffer
        if (boidBuffer != null) boidBuffer.Release();
        boidBuffer = new ComputeBuffer(boidDataList.Count, Marshal.SizeOf(typeof(BoidData))); // 32 bytes
        boidBuffer.SetData(boidDataList);
        boidComputeShader.SetBuffer(kernelHandle, "boidBuffer", boidBuffer);
        boidComputeShader.SetInt("boidCount", boidDataList.Count);
        Debug.Log($"Initialized {boidDataList.Count} boids across {swarms.Count} swarms.");

        // Initialize Swarm Buffer
        if (swarmBuffer != null) swarmBuffer.Release();
        int swarmStride = Marshal.SizeOf(typeof(SwarmData)); // 56 bytes
        swarmBuffer = new ComputeBuffer(swarmDataList.Count, swarmStride); // 56 bytes
        swarmBuffer.SetData(swarmDataList);
        boidComputeShader.SetBuffer(kernelHandle, "swarmBuffer", swarmBuffer);
        boidComputeShader.SetInt("swarmCount", swarms.Count);
        Debug.Log($"Initialized {swarmDataList.Count} swarms.");

        // Initialize Instance Buffer
        if (instanceBuffer != null) instanceBuffer.Release();
        instanceBuffer = new ComputeBuffer(boidDataList.Count, Marshal.SizeOf(typeof(InstanceData))); // 80 bytes
        boidComputeShader.SetBuffer(kernelHandle, "instanceTransforms", instanceBuffer); // Correct buffer name
        Debug.Log($"Initialized instance buffer with {boidDataList.Count} instances.");

        // Initialize Args Buffer
        if (argsBuffer != null) argsBuffer.Release();
        if (boidDataList.Count > 0 && swarms.Count > 0 && swarms[0].boidMesh != null)
        {
            uint indexCount = (uint)swarms[0].boidMesh.GetIndexCount(0);
            uint instanceCount = (uint)boidDataList.Count;
            uint startIndexLocation = 0;
            uint baseVertexLocation = 0;
            uint startInstanceLocation = 0;

            uint[] args = new uint[5]
            {
                indexCount,
                instanceCount,
                startIndexLocation,
                baseVertexLocation,
                startInstanceLocation
            };
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);
            Debug.Log($"Args Buffer Initialized: IndexCount={indexCount}, InstanceCount={instanceCount}");
        }
        else
        {
            Debug.LogWarning("No swarms or boidMesh assigned. argsBuffer not initialized.");
        }

        // Log total swarms and boids
        Debug.Log($"Total Swarms: {swarmDataList.Count}, Total Boids: {boidDataList.Count}");
    }

    private Vector3 GetValidSpawnPosition()
    {
        Vector3 position;
        bool valid;
        int attempts = 0;
        int maxAttempts = 100;

        do
        {
            position = Random.insideUnitSphere * boundsSize.x / 2f;
            valid = true;
            foreach (Collider collider in obstacleColliders)
            {
                if (collider != null && collider.bounds.Contains(position))
                {
                    valid = false;
                    break;
                }
            }
            attempts++;
        } while (!valid && attempts < maxAttempts);

        return position;
    }

    private IEnumerator SpawnFoodSources()
    {
        while (true)
        {
            Vector3 position = GetValidSpawnPosition();
            GameObject food = Instantiate(foodPrefab, position, Quaternion.identity);
            activeFoodSources.Add(food);
            StartCoroutine(DestroyFoodAfterTime(food, foodLifetime));
            yield return new WaitForSeconds(Random.Range(foodSpawnInterval - 2f, foodSpawnInterval + 2f));
        }
    }

    private IEnumerator DestroyFoodAfterTime(GameObject food, float time)
    {
        yield return new WaitForSeconds(time);
        activeFoodSources.Remove(food);
        Destroy(food);
    }

    private void DispatchComputeShader()
    {
        if (kernelHandle < 0)
        {
            Debug.LogError("Invalid kernel handle.");
            return;
        }

        if (boidBuffer == null || !boidBuffer.IsValid())
        {
            Debug.LogError("Invalid boid buffer.");
            return;
        }

        boidComputeShader.SetFloat("deltaTime", Time.deltaTime);
        boidComputeShader.SetVector("managerPosition", transform.position);
        boidComputeShader.SetVector("boundsSize", boundsSize);

        int threadGroups = Mathf.CeilToInt((float)boidBuffer.count / 256);
        Debug.Log($"Dispatching Compute Shader with {threadGroups} thread groups.");
        boidComputeShader.Dispatch(kernelHandle, threadGroups, 1, 1);

        // Debugging: Read back boid positions and velocities
        BoidData[] boids = new BoidData[boidBuffer.count];
        boidBuffer.GetData(boids);

        // Log the first few boids to verify updates
        for(int i = 0; i < Mathf.Min(boids.Length, 5); i++)
        {
            Debug.Log($"Boid {i}: Position = {boids[i].position}, Velocity = {boids[i].velocity}");
        }

        // Debugging: Read back instanceTransforms
        InstanceData[] instances = new InstanceData[instanceBuffer.count];
        instanceBuffer.GetData(instances);

        // Log the first few transforms and colors
        for(int i = 0; i < Mathf.Min(instances.Length, 5); i++)
        {
            Debug.Log($"Instance {i}: Position = {instances[i].transform.GetColumn(3)}, Color = {instances[i].color}");
        }
    }

    private void RenderBoids()
    {
        if (argsBuffer == null)
        {
            Debug.LogError("Arguments buffer is null. Ensure it is initialized before rendering.");
            return;
        }

        if (swarms.Count == 0)
        {
            Debug.LogWarning("No swarms to render.");
            return;
        }

        // Use the first swarm's mesh and material for rendering all boids
        Swarm firstSwarm = swarms[0];

        if (firstSwarm.boidMesh == null || firstSwarm.boidMaterial == null)
        {
            Debug.LogError("Boid mesh or material is not assigned in the first swarm configuration.");
            return;
        }

        Bounds bounds = new Bounds(transform.position, boundsSize);

        Debug.Log($"Rendering {boidBuffer.count} boids.");

        // Set the instance buffer to the material via MaterialPropertyBlock
        propBlock.SetBuffer("instanceTransforms", instanceBuffer);

        // Draw all boids in a single call
        Graphics.DrawMeshInstancedIndirect(
            firstSwarm.boidMesh,
            0, // Submesh index
            firstSwarm.boidMaterial,
            bounds,
            argsBuffer,
            0, // argsOffset
            propBlock,
            UnityEngine.Rendering.ShadowCastingMode.On,
            true, // receiveShadows
            0, // layer
            null, // camera
            UnityEngine.Rendering.LightProbeUsage.Off, // Changed to Off
            null // lightProbeProxyVolume
        );
    }

    private void ReleaseBuffers()
    {
        if (boidBuffer != null) boidBuffer.Release();
        if (swarmBuffer != null) swarmBuffer.Release();
        if (triangleBuffer != null) triangleBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();
        if (instanceBuffer != null) instanceBuffer.Release();
    }

    private void OnDrawGizmos()
    {
        if (boidBuffer == null || !boidBuffer.IsValid())
            return;

        int totalBoids = boidBuffer.count;
        BoidData[] boids = new BoidData[totalBoids];
        boidBuffer.GetData(boids);

        // Draw simulation bounds
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(transform.position, boundsSize);

        // Draw each boid as a small sphere
        foreach (var boid in boids)
        {
            // Ensure boids are within simulation bounds
            // Determine color based on swarm type
            Color boidColor = Color.white;
            switch ((SwarmType)boid.swarmType)
            {
                case SwarmType.Prey:
                    boidColor = Color.green;
                    break;
                case SwarmType.Predator:
                    boidColor = Color.red;
                    break;
                case SwarmType.Omnivore:
                    boidColor = Color.yellow;
                    break;
                case SwarmType.FoodSource:
                    boidColor = Color.blue;
                    break;
            }

            Gizmos.color = boidColor;
            Gizmos.DrawSphere(boid.position, 0.2f); // Adjust size as needed
        }
    }
}
