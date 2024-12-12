using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct BoidData
{
    public Vector3 position;
    public Vector3 velocity;
    public int swarmType;
    public int swarmID;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct TriangleData
{
    public Vector3 vertexA;
    public Vector3 vertexB;
    public Vector3 vertexC;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct SwarmData
{
    public int swarmID;
    public int type;
    public float separationWeight;
    public float alignmentWeight;
    public float cohesionWeight;
    public float obstacleAvoidanceWeight;
    public float boundaryStrength;
    public float boundaryAvoidDistance;
    public Vector4 swarmColor;
    public float speed;
    public float obstacleAvoidDistance;
}

public enum SwarmType
{
    Prey = 0,
    Predator = 1,
    Omnivore = 2
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
    public float obstacleAvoidDistance = 15f;
}

[System.Serializable]
public class Swarm
{
    public SwarmType type;
    public int swarmID;
    public int boidCount;
    public Mesh boidMesh;
    public Material boidMaterial;
    public SwarmParameters parameters;
    public float speed = 5f;
    [HideInInspector]
    public Vector4 swarmColor;

    public Vector3 rotation;
    public Vector3 scale = Vector3.one;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct InstanceData
{
    public Matrix4x4 transform;
    public Vector4 color;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct FoodAttractor
{
    public Vector3 position;
    public float expirationTime;
}

public class SwarmManager : MonoBehaviour
{
    [Header("Swarms Configuration")]
    public List<Swarm> swarms = new List<Swarm>();

    [Header("Compute Shader")]
    public ComputeShader boidComputeShader;

    [Header("Food Attractor Settings")]
    public GameObject foodAttractorPrefab;
    public float foodSpawnInterval = 5f;
    public float foodLifetime = 10f;

    [Header("Rendering Bounds")]
    public Vector3 boundsSize = new Vector3(100f, 100f, 100f);

    private ComputeBuffer boidBuffer;
    private ComputeBuffer swarmBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer foodAttractorBuffer;
    private List<ComputeBuffer> argsBuffers = new List<ComputeBuffer>();
    private ComputeBuffer instanceBuffer;

    private List<BoidData> boidDataList = new List<BoidData>();
    private List<InstanceData> instanceDataList = new List<InstanceData>();
    private List<SwarmData> swarmDataList = new List<SwarmData>();
    private List<FoodAttractor> foodAttractors = new List<FoodAttractor>();

    private int kernelHandle = -1;
    private Collider[] obstacleColliders;
    private MaterialPropertyBlock propBlock;
    private int[] swarmOffsets;
    private Dictionary<FoodAttractor, GameObject> foodVisuals = new Dictionary<FoodAttractor, GameObject>();

   private bool roomInitialized = false;

    void Start()
    {
        if (!CheckRoomInitialization())
        {
            Debug.LogWarning("No room object found. SwarmManager will not initialize.");
            return;
        }

        InitializeManager();
    }

    void Update()
    {
        if (!roomInitialized)
        {
            if (CheckRoomInitialization())
            {
                Debug.Log("Room object detected. Initializing SwarmManager...");
                InitializeManager();
            }
            else
            {
                return; // Skip update until the room is initialized
            }
        }

        UpdateFoodAttractors();
        DispatchComputeShader();
        RenderBoids();
    }

    private bool CheckRoomInitialization()
    {
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("oom"))
            {
                roomInitialized = true;
                return true;
            }
        }
        return false;
    }

    private void InitializeManager()
    {
        roomInitialized = true;

        AssignSwarmIDs();
        AssignSwarmColors();

        if (boidComputeShader == null)
        {
            Debug.LogError("Compute Shader not assigned!");
            return;
        }

        kernelHandle = boidComputeShader.FindKernel("CSMain");
        if (kernelHandle < 0)
        {
            Debug.LogError("Failed to find kernel CSMain in the compute shader.");
            return;
        }

        propBlock = new MaterialPropertyBlock();

        InitializeObstacleMeshes();
        InitializeSwarms();
        InitializeFoodAttractorBuffer();
        StartCoroutine(SpawnFoodAttractors());
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
            case SwarmType.Prey: return Color.green;
            case SwarmType.Predator: return Color.red;
            case SwarmType.Omnivore: return Color.yellow;
            default: return Color.white;
        }
    }

   private void InitializeObstacleMeshes()
   {
      // Tag all children of "Room -" objects as "Obstacle"
      TagRoomChildrenAsObstacles();

      // Find all objects with the "Obstacle" tag
      GameObject[] obstacleObjects = GameObject.FindGameObjectsWithTag("Obstacle");
      List<TriangleData> allTriangles = new List<TriangleData>();

      foreach (GameObject obstacle in obstacleObjects)
      {
         MeshFilter[] meshFilters = obstacle.GetComponentsInChildren<MeshFilter>();

         foreach (MeshFilter mf in meshFilters)
         {
               Mesh mesh = mf.sharedMesh;
               if (mesh == null) continue;

               Vector3[] vertices = mesh.vertices;
               int[] indices = mesh.triangles;
               Matrix4x4 localToWorld = mf.transform.localToWorldMatrix;

               for (int i = 0; i < indices.Length; i += 3)
               {
                  allTriangles.Add(new TriangleData
                  {
                     vertexA = localToWorld.MultiplyPoint3x4(vertices[indices[i]]),
                     vertexB = localToWorld.MultiplyPoint3x4(vertices[indices[i + 1]]),
                     vertexC = localToWorld.MultiplyPoint3x4(vertices[indices[i + 2]])
                  });
               }
         }
      }

      Debug.Log("Uploaded Triangles" + allTriangles.Count);
      // Upload triangle data to a buffer
      UploadTriangleBuffer(allTriangles.ToArray());
      obstacleColliders = FindObjectsOfType<Collider>();
   }

   private void TagRoomChildrenAsObstacles()
   {
      GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

      foreach (GameObject obj in allObjects)
      {
         if (obj.name.StartsWith("Room -"))
         {
               TagChildrenRecursively(obj.transform, "Obstacle");
         }
      }
   }

   private void TagChildrenRecursively(Transform parent, string tag)
   {
      foreach (Transform child in parent)
      {
         child.gameObject.tag = tag;
         TagChildrenRecursively(child, tag);
      }
   }
    private void UploadTriangleBuffer(TriangleData[] triangles)
    {
        if (triangleBuffer != null) triangleBuffer.Release();
        triangleBuffer = new ComputeBuffer(triangles.Length, Marshal.SizeOf(typeof(TriangleData)));
        triangleBuffer.SetData(triangles);
        boidComputeShader.SetBuffer(kernelHandle, "triangleBuffer", triangleBuffer);
        boidComputeShader.SetInt("triangleCount", triangles.Length);
    }

    private void InitializeSwarms()
    {
        boidDataList.Clear();
        instanceDataList.Clear();
        swarmDataList.Clear();

      GameObject[] camera = GameObject.FindGameObjectsWithTag("MainCamera");

        swarmOffsets = new int[swarms.Count];
        int cumulativeCount = 0;

        // Build SwarmData
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
                speed = swarm.speed,
                obstacleAvoidDistance = swarm.parameters.obstacleAvoidDistance
            };
            swarmDataList.Add(sData);

            swarmOffsets[swarm.swarmID] = cumulativeCount;
            cumulativeCount += swarm.boidCount;

            for (int i = 0; i < swarm.boidCount; i++)
            {
                Vector3 startPos = GetValidSpawnPosition();
                BoidData bData = new BoidData
                {
                    position = camera[0].transform.position,
                    velocity = -startPos * 0.1f,
                    swarmType = (int)swarm.type,
                    swarmID = swarm.swarmID
                };
                boidDataList.Add(bData);

                InstanceData instance = new InstanceData
                {
                    transform = Matrix4x4.identity,
                    color = swarm.swarmColor
                };
                instanceDataList.Add(instance);
            }
        }

        InitializeComputeBuffers();
    }

    private void InitializeComputeBuffers()
    {
        // Boid buffer
        if (boidBuffer != null) boidBuffer.Release();
        boidBuffer = new ComputeBuffer(boidDataList.Count, Marshal.SizeOf(typeof(BoidData)));
        boidBuffer.SetData(boidDataList);
        boidComputeShader.SetBuffer(kernelHandle, "boidBuffer", boidBuffer);
        boidComputeShader.SetInt("boidCount", boidDataList.Count);

        // Swarm buffer
        if (swarmBuffer != null) swarmBuffer.Release();
        int swarmStride = Marshal.SizeOf(typeof(SwarmData));
        swarmBuffer = new ComputeBuffer(swarmDataList.Count, swarmStride);
        swarmBuffer.SetData(swarmDataList);
        boidComputeShader.SetBuffer(kernelHandle, "swarmBuffer", swarmBuffer);
        boidComputeShader.SetInt("swarmCount", swarms.Count);

        // Instance buffer
        if (instanceBuffer != null) instanceBuffer.Release();
        instanceBuffer = new ComputeBuffer(instanceDataList.Count, Marshal.SizeOf(typeof(InstanceData)));
        instanceBuffer.SetData(instanceDataList);
        boidComputeShader.SetBuffer(kernelHandle, "instanceTransforms", instanceBuffer);

        InitializeArgsBuffers();
    }

   private void InitializeArgsBuffers()
   {
      // Release old argsBuffers
      if (argsBuffers != null)
      {
         foreach (var ab in argsBuffers)
               if (ab != null) ab.Release();
      }
      argsBuffers.Clear();

      for (int i = 0; i < swarms.Count; i++)
      {
         var swarm = swarms[i];
         if (swarm.boidMesh != null && swarm.boidCount > 0)
         {
               uint indexCount = (uint)swarm.boidMesh.GetIndexCount(0);
               // Double the instance count for single-pass instanced rendering
               uint instanceCount = (uint)swarm.boidCount * 2;
               uint startInstanceLocation = (uint)swarmOffsets[i];

               uint[] args = new uint[5]
               {
                  indexCount,
                  instanceCount,
                  0,
                  0,
                  startInstanceLocation
               };

               ComputeBuffer argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
               argsBuffer.SetData(args);
               argsBuffers.Add(argsBuffer);
         }
         else
         {
               argsBuffers.Add(null);
         }
      }
   }

    private void InitializeFoodAttractorBuffer()
    {
        if (foodAttractorBuffer != null)
        {
            foodAttractorBuffer.Release();
        }

        int maxFoodAttractors = 100;
        foodAttractorBuffer = new ComputeBuffer(maxFoodAttractors, Marshal.SizeOf(typeof(FoodAttractor)));
        FoodAttractor[] initialFood = new FoodAttractor[maxFoodAttractors];
        foodAttractorBuffer.SetData(initialFood);
        boidComputeShader.SetBuffer(kernelHandle, "foodAttractorBuffer", foodAttractorBuffer);
        boidComputeShader.SetInt("foodAttractorCount", 0);
    }

    public void AddBoidToSwarm(int swarmID)
    {
        Swarm targetSwarm = swarms.Find(s => s.swarmID == swarmID);
        if (targetSwarm == null)
        {
            Debug.LogError($"Swarm with ID {swarmID} not found.");
            return;
        }

        Vector3 startPos = GetValidSpawnPosition();
        BoidData bData = new BoidData
        {
            position = startPos,
            velocity = -startPos * 0.1f,
            swarmType = (int)targetSwarm.type,
            swarmID = swarmID
        };
        boidDataList.Add(bData);

        InstanceData instance = new InstanceData
        {
            transform = Matrix4x4.identity,
            color = targetSwarm.swarmColor
        };
        instanceDataList.Add(instance);

        targetSwarm.boidCount += 1;

        RecalculateSwarmOffsets();
        RefreshSwarmData();
        InitializeComputeBuffers();
    }

    public void RemoveBoidFromSwarm(int swarmID)
    {
        Swarm targetSwarm = swarms.Find(s => s.swarmID == swarmID);
        if (targetSwarm == null)
        {
            Debug.LogError($"Swarm with ID {swarmID} not found.");
            return;
        }

        int lastIndex = boidDataList.FindLastIndex(b => b.swarmID == swarmID);
        if (lastIndex == -1)
        {
            Debug.LogWarning($"No boids found in swarm ID {swarmID} to remove.");
            return;
        }

        boidDataList.RemoveAt(lastIndex);
        instanceDataList.RemoveAt(lastIndex);

        targetSwarm.boidCount -= 1;

        RecalculateSwarmOffsets();
        RefreshSwarmData();
        InitializeComputeBuffers();
    }

    public void AdjustSwarmParameters(int swarmID, SwarmParameters newParams)
    {
        Swarm targetSwarm = swarms.Find(s => s.swarmID == swarmID);
        if (targetSwarm == null)
        {
            Debug.LogError($"Swarm with ID {swarmID} not found.");
            return;
        }

        targetSwarm.parameters = newParams;
        targetSwarm.speed = targetSwarm.speed; // speed may also be adjusted if needed

        RefreshSwarmData();
        InitializeComputeBuffers();
    }

    private void RefreshSwarmData()
    {
        swarmDataList.Clear();
        for (int i = 0; i < swarms.Count; i++)
        {
            var swarm = swarms[i];
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
                speed = swarm.speed,
                obstacleAvoidDistance = swarm.parameters.obstacleAvoidDistance
            };
            swarmDataList.Add(sData);
        }
    }

    private void RecalculateSwarmOffsets()
    {
        int cumulativeCount = 0;
        swarmOffsets = new int[swarms.Count];
        for (int i = 0; i < swarms.Count; i++)
        {
            swarmOffsets[i] = cumulativeCount;
            cumulativeCount += swarms[i].boidCount;
        }
    }

    public void AddFoodAttractor(Vector3 position)
    {
        if (foodAttractorPrefab == null)
        {
            Debug.LogError("Food Attractor Prefab is not assigned in the Inspector.");
            return;
        }

        GameObject visual = Instantiate(foodAttractorPrefab, position, Quaternion.identity);
        visual.transform.parent = this.transform;

        FoodAttractor fa = new FoodAttractor
        {
            position = position,
            expirationTime = Time.time + foodLifetime
        };
        foodAttractors.Add(fa);
        foodVisuals.Add(fa, visual);

        UpdateFoodAttractorBuffer();
    }

    private void UpdateFoodAttractors()
    {
        bool bufferNeedsUpdate = false;
        for (int i = foodAttractors.Count - 1; i >= 0; i--)
        {
            if (Time.time >= foodAttractors[i].expirationTime)
            {
                if (foodVisuals.ContainsKey(foodAttractors[i]))
                {
                    Destroy(foodVisuals[foodAttractors[i]]);
                    foodVisuals.Remove(foodAttractors[i]);
                }
                foodAttractors.RemoveAt(i);
                bufferNeedsUpdate = true;
            }
        }

        if (bufferNeedsUpdate)
        {
            UpdateFoodAttractorBuffer();
        }
    }

    private void UpdateFoodAttractorBuffer()
    {
        if (foodAttractorBuffer == null)
        {
            InitializeFoodAttractorBuffer();
        }

        int maxFoodAttractors = foodAttractorBuffer.count;
        int count = Mathf.Min(foodAttractors.Count, maxFoodAttractors);

        FoodAttractor[] faArray = new FoodAttractor[maxFoodAttractors];
        for (int i = 0; i < count; i++)
        {
            faArray[i] = foodAttractors[i];
        }

        foodAttractorBuffer.SetData(faArray);
        boidComputeShader.SetInt("foodAttractorCount", count);
    }

    private IEnumerator SpawnFoodAttractors()
    {
        while (true)
        {
            Vector3 randomPosition = new Vector3(
                Random.Range(-boundsSize.x / 2f, boundsSize.x / 2f),
                Random.Range(-boundsSize.y / 2f, boundsSize.y / 2f),
                Random.Range(-boundsSize.z / 2f, boundsSize.z / 2f)
            ) + transform.position;

            AddFoodAttractor(randomPosition);

            yield return new WaitForSeconds(foodSpawnInterval);
        }
    }

    private Vector3 GetValidSpawnPosition()
    {
        Vector3 position;
        bool valid;
        int attempts = 0;
        int maxAttempts = 100;

        do
        {
            position = Random.insideUnitSphere * (boundsSize.x / 2f) + transform.position;
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

    private void DispatchComputeShader()
    {
        if (kernelHandle < 0) return;
        if (boidBuffer == null || !boidBuffer.IsValid()) return;

        boidComputeShader.SetFloat("deltaTime", Time.deltaTime);
        boidComputeShader.SetVector("managerPosition", transform.position);
        boidComputeShader.SetVector("boundsSize", boundsSize);

        int threadGroups = Mathf.CeilToInt((float)boidDataList.Count / 256);
        boidComputeShader.Dispatch(kernelHandle, threadGroups, 1, 1);
    }

    private void RenderBoids()
    {
        if (swarms.Count == 0) return;

        Bounds bounds = new Bounds(transform.position, boundsSize);

        for (int i = 0; i < swarms.Count; i++)
        {
            var swarm = swarms[i];
            var argsBuffer = argsBuffers[i];

            if (swarm.boidMesh == null || swarm.boidMaterial == null || argsBuffer == null || swarm.boidCount == 0)
            {
                continue;
            }

            Quaternion rot = Quaternion.Euler(swarm.rotation);
            Matrix4x4 swarmTransform = Matrix4x4.TRS(Vector3.zero, rot, swarm.scale);

            propBlock.Clear();
            propBlock.SetBuffer("instanceTransforms", instanceBuffer);
            propBlock.SetMatrix("_SwarmTransform", swarmTransform);
            propBlock.SetFloat("_StartInstanceOffset", swarmOffsets[i]);

            Graphics.DrawMeshInstancedIndirect(
                swarm.boidMesh,
                0,
                swarm.boidMaterial,
                bounds,
                argsBuffer,
                0,
                propBlock,
                UnityEngine.Rendering.ShadowCastingMode.On,
                true,
                0,
                null,
                UnityEngine.Rendering.LightProbeUsage.Off,
                null
            );
        }
    }

    private void ReleaseBuffers()
    {
        if (boidBuffer != null) boidBuffer.Release();
        if (swarmBuffer != null) swarmBuffer.Release();
        if (triangleBuffer != null) triangleBuffer.Release();
        if (instanceBuffer != null) instanceBuffer.Release();
        if (foodAttractorBuffer != null) foodAttractorBuffer.Release();

        if (argsBuffers != null)
        {
            foreach (var ab in argsBuffers)
            {
                if (ab != null) ab.Release();
            }
        }
    }

private void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(transform.position, boundsSize);
    }

}
