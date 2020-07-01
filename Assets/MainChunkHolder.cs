using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
//using static MeshHolder;

[ExecuteInEditMode]
public class MainChunkHolder : MonoBehaviour
{
    public bool isColliderConvex = false;
    public bool isLowPoly = false;

    const int threadGroupSize = 8;

    [Header("General Settings")]
    public DensityGenerator densityGenerator;

    public bool fixedMapSize;
    [ConditionalHide(nameof(fixedMapSize), true)]
    public Vector3Int numMeshChunks = Vector3Int.one;
    [ConditionalHide(nameof(fixedMapSize), false)]
    public Transform viewer;
    [ConditionalHide(nameof(fixedMapSize), false)]
    public float viewDistance = 30;

    [Space()]
    public bool autoUpdateInEditor = true;
    public bool autoUpdateInGame = true;
    public ComputeShader shader;
    public Material mat;
    public bool generateColliders;

    [Header("Voxel Settings")]
    public float isoLevel;
    public float boundsSize = 1;
    public Vector3 offset = Vector3.zero;

    [Range(2, 100)]
    public int numPointsPerAxis = 30;

    [Header("Mesh Settings")]
    public float normalDegrees = 180;

    [Header("Gizmos")]
    public bool showBoundsGizmo = true;
    public Color boundsGizmoCol = Color.white;

    GameObject MeshChunkHolder;
    public string MeshChunkHolderName = "MeshChunks Holder";
    List<MeshChunk> MeshChunks;
    Dictionary<Vector3Int, MeshChunk> existingMeshChunks;
    Queue<MeshChunk> recycleableMeshChunks;

    // Buffers
    ComputeBuffer triangleBuffer;
    ComputeBuffer pointsBuffer;
    ComputeBuffer triCountBuffer;

    bool settingsUpdated;
    public float fpsBreakout = 60f;
    public static MainChunkHolder mainHolder;
    void Awake()
    {
        mainHolder = this;
        if (Application.isPlaying && !fixedMapSize) {
            InitVariableMeshChunkStructures();

            var oldMeshChunks = FindObjectsOfType<MeshChunk>();
            for (int i = oldMeshChunks.Length - 1; i >= 0; i--) {
                Destroy(oldMeshChunks[i].gameObject);
            }
        }
    }

    void Update()
    {
        // Update endless terrain
        if ((Application.isPlaying && !fixedMapSize)) {
            Run();
        }

        if (settingsUpdated) {
            RequestMeshUpdate();
            settingsUpdated = false;
        }
    }

    public void Run()
    {
        CreateBuffers();

        if (fixedMapSize) {
            InitMeshChunks();
            UpdateAllMeshChunks();

        }
        else {
            if (Application.isPlaying) {
                InitVisibleMeshChunks();
            }
        }

        // Release buffers immediately in editor
        if (!Application.isPlaying) {
            ReleaseBuffers();
        }

    }

    public void RequestMeshUpdate()
    {
        if ((Application.isPlaying && autoUpdateInGame) || (!Application.isPlaying && autoUpdateInEditor)) {
            Run();
        }
    }

    void InitVariableMeshChunkStructures()
    {
        recycleableMeshChunks = new Queue<MeshChunk>();
        MeshChunks = new List<MeshChunk>();
        existingMeshChunks = new Dictionary<Vector3Int, MeshChunk>();
    }



    void InitVisibleMeshChunks()
    {
        if (MeshChunks == null) {
            return;
        }
        CreateMeshChunkHolder();



        Vector3 p = viewer.position;
        Vector3 ps = p / boundsSize;
        Vector3Int viewerCoord = new Vector3Int(Mathf.RoundToInt(ps.x), Mathf.RoundToInt(ps.y), Mathf.RoundToInt(ps.z));

        int maxMeshChunksInView = Mathf.CeilToInt(viewDistance / boundsSize);
        float sqrViewDistance = viewDistance * viewDistance;

        // Go through all existing MeshChunks and flag for recyling if outside of max view dst
        for (int i = MeshChunks.Count - 1; i >= 0; i--) {
            MeshChunk MeshChunk = MeshChunks[i];
            Vector3 centre = CentreFromCoord(MeshChunk.coord);
            Vector3 viewerOffset = p - centre;
            Vector3 o = new Vector3(Mathf.Abs(viewerOffset.x), Mathf.Abs(viewerOffset.y), Mathf.Abs(viewerOffset.z)) - Vector3.one * boundsSize / 2;
            float sqrDst = new Vector3(Mathf.Max(o.x, 0), Mathf.Max(o.y, 0), Mathf.Max(o.z, 0)).sqrMagnitude;
            if (sqrDst > sqrViewDistance) {
                existingMeshChunks.Remove(MeshChunk.coord);
                recycleableMeshChunks.Enqueue(MeshChunk);
                MeshChunks.RemoveAt(i);
            }
        }

        float t0 = Time.realtimeSinceStartup;

        foreach (Vector3Int spi in SpiralIterators.Cube(new Vector3Int(maxMeshChunksInView, maxMeshChunksInView, maxMeshChunksInView))) {
            if (Time.realtimeSinceStartup - t0 > (1.0 / fpsBreakout)) {
                return;
            }

            Vector3Int coord = spi + viewerCoord;

            if (existingMeshChunks.ContainsKey(coord)) {
                continue;
            }

            Vector3 centre = CentreFromCoord(coord);
            Vector3 viewerOffset = p - centre;
            Vector3 o = new Vector3(Mathf.Abs(viewerOffset.x), Mathf.Abs(viewerOffset.y), Mathf.Abs(viewerOffset.z)) - Vector3.one * boundsSize / 2;
            float sqrDst = new Vector3(Mathf.Max(o.x, 0), Mathf.Max(o.y, 0), Mathf.Max(o.z, 0)).sqrMagnitude;

            // MeshChunk is within view distance and should be created (if it doesn't already exist)
            if (sqrDst <= sqrViewDistance) {

                Bounds bounds = new Bounds(CentreFromCoord(coord), Vector3.one * boundsSize);
                if (IsVisibleFrom(bounds, Camera.main)) {
                    if (recycleableMeshChunks.Count > 0) {
                        MeshChunk MeshChunk = recycleableMeshChunks.Dequeue();
                        MeshChunk.coord = coord;
                        existingMeshChunks.Add(coord, MeshChunk);
                        MeshChunks.Add(MeshChunk);
                        UpdateMeshChunkMesh(MeshChunk);
                    }
                    else {
                        MeshChunk MeshChunk = CreateMeshChunk(coord);
                        MeshChunk.coord = coord;
                        MeshChunk.SetUp(mat, generateColliders);
                        existingMeshChunks.Add(coord, MeshChunk);
                        MeshChunks.Add(MeshChunk);
                        UpdateMeshChunkMesh(MeshChunk);
                    }
                }
            }

        }
    }

    public bool IsVisibleFrom(Bounds bounds, Camera camera)
    {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }
    [System.Serializable]
    public struct float4
    {
        public float x;
        public float y;
        public float z;
        public float w;
        public float4(float X, float Y, float Z, float W)
        {
            x = X;
            y = Y;
            z = Z;
            w = W;
        }

        public static explicit operator Vector4(float4 v)
        {
            return new Vector4(v.x, v.y, v.z, v.w);
        }
        public static explicit operator float4(Vector4 v)
        {
            return new float4(v.x, v.y, v.z, v.w);
        }
        public static float4 operator +(float4 a, float4 b)
        {
            return new float4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        }

        public static float4 operator -(float4 a, float4 b)
        {
            return new float4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
        }
    }

    const float _pointSpacing = 1;

    public string GetNameFromCoord(Vector3Int coord)
    {
        return "MeshChunk " + coord;
    }
    public void UpdateMeshChunkMesh(MeshChunk MeshChunk, bool updateFromEdit = false)
    {
        MeshChunk.name = GetNameFromCoord(MeshChunk.coord);

        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float)threadGroupSize);
        //float pointSpacing = boundsSize / (numPointsPerAxis - 1);
        float pointSpacing = _pointSpacing;
        Vector3Int coord = MeshChunk.coord;
        Vector3 centre = CentreFromCoord(coord);

        Vector3 worldBounds = new Vector3(numMeshChunks.x, numMeshChunks.y, numMeshChunks.z) * boundsSize;

        if (updateFromEdit) {
            pointsBuffer.SetData(MeshChunk.SaveData);
        }
        else {
            /*
            var save = SaveSystem<List<float4>>.Get(new List<float4>(), gameObject, "SAVE" + coord);
            if (save.Count > 1) {
                print("GOT SAVE");
                pointsBuffer.SetData(save);
                chunk.SaveData = save;
            }
            else {
                */
            densityGenerator.Generate(pointsBuffer, numPointsPerAxis, boundsSize, worldBounds, centre, offset, pointSpacing);
            //int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis; 
            float4[] data = new float4[numPointsPerAxis * numPointsPerAxis * numPointsPerAxis];
            pointsBuffer.GetData(data);
            MeshChunk.SaveData = data.ToList();
            // SaveSystem<List<float4>>.Save(chunk.SaveData, gameObject, "SAVE" + coord);
            //}

        }


        //densityGenerator.Generate(pointsBuffer, numPointsPerAxis, boundsSize, worldBounds, centre, offset, pointSpacing);

        triangleBuffer.SetCounterValue(0);
        shader.SetBuffer(0, "points", pointsBuffer);
        shader.SetBuffer(0, "triangles", triangleBuffer);
        shader.SetInt("numPointsPerAxis", numPointsPerAxis);
        shader.SetFloat("isoLevel", isoLevel);

        shader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        // Get triangle data from shader
        Triangle[] tris = new Triangle[numTris];
        triangleBuffer.GetData(tris, 0, 0, numTris);

        Mesh mesh = MeshChunk.mesh;
        mesh.Clear();

        var vertices = new Vector3[numTris * 3];
        var meshTriangles = new int[numTris * 3];

        for (int i = 0; i < numTris; i++) {
            for (int j = 0; j < 3; j++) {
                meshTriangles[i * 3 + j] = i * 3 + j;
                vertices[i * 3 + j] = tris[i][j];
            }
        }
        mesh.vertices = vertices;
        mesh.triangles = meshTriangles;

        if (isLowPoly) {
            mesh.RecalculateNormals();
        }
        else {
            var scale = MeshChunk.GetComponent<Transform>().localScale;
            mesh.SetUVs(0, UvCalculator.CalculateUVs(vertices, scale.magnitude));
            NormalSolver.RecalculateNormals(mesh, normalDegrees);
        }

        MeshChunk.UpdateColliders();


    }


    public void RemoveChunk(MeshChunk old)
    {
        MeshChunks.Remove(old);
        recycleableMeshChunks.Enqueue(old);
        existingMeshChunks.Remove(old.coord);
        Destroy(old.gameObject);
    }


    public void AddChuck(Vector3Int coord)
    {
        if (existingMeshChunks.ContainsKey(coord)) {
            RemoveChunk(existingMeshChunks[coord]);
        }
        MeshChunk _chunk = CreateMeshChunk(coord);
        _chunk.coord = coord;
        //_chunk.SaveData = new List<float4>();
        _chunk.SetUp(mat, generateColliders, isColliderConvex);
        existingMeshChunks.Add(coord, _chunk);
        MeshChunks.Add(_chunk);

        UpdateMeshChunkMesh(_chunk);
    }

    public void HotReloadChunk(Vector3Int coord, MeshChunk old)
    {
        RemoveChunk(old);
        // old.transform.position = new Vector3(0, 1, 0);
        AddChuck(coord);


        print("UPDATED");
    }


    public void UpdateAllMeshChunks()
    {

        // Create mesh for each MeshChunk
        foreach (MeshChunk MeshChunk in MeshChunks) {
            UpdateMeshChunkMesh(MeshChunk);
        }

    }

    void OnDestroy()
    {
        if (Application.isPlaying) {
            ReleaseBuffers();
        }
    }

    void CreateBuffers()
    {
        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;

        // Always create buffers in editor (since buffers are released immediately to prevent memory leak)
        // Otherwise, only create if null or if size has changed
        if (!Application.isPlaying || (pointsBuffer == null || numPoints != pointsBuffer.count)) {
            if (Application.isPlaying) {
                ReleaseBuffers();
            }
            triangleBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
            pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
            triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        }
    }

    void ReleaseBuffers()
    {
        if (triangleBuffer != null) {
            triangleBuffer.Release();
            pointsBuffer.Release();
            triCountBuffer.Release();
        }
    }

    public Vector3 CentreFromCoord(Vector3Int coord)
    {
        // Centre entire map at origin
        if (fixedMapSize) {
            Vector3 totalBounds = (Vector3)numMeshChunks * boundsSize;
            return -totalBounds / 2 + (Vector3)coord * boundsSize + Vector3.one * boundsSize / 2;
        }

        return new Vector3(coord.x, coord.y, coord.z) * boundsSize;
    }

    void CreateMeshChunkHolder()
    {
        // Create/find mesh holder object for organizing MeshChunks under in the hierarchy
        if (MeshChunkHolder == null) {
            if (GameObject.Find(MeshChunkHolderName)) {
                MeshChunkHolder = GameObject.Find(MeshChunkHolderName);
            }
            else {
                MeshChunkHolder = new GameObject(MeshChunkHolderName);

                if (generateColliders) {
                    //add rigidbody so collisions are enforced
                    var rigidBody = MeshChunkHolder.AddComponent<Rigidbody>();
                    rigidBody.useGravity = false;
                    rigidBody.isKinematic = true;
                    rigidBody.constraints = RigidbodyConstraints.FreezeAll;
                    rigidBody.detectCollisions = true;
                }

            }
        }
    }

    // Create/get references to all MeshChunks
    void InitMeshChunks()
    {
        CreateMeshChunkHolder();
        MeshChunks = new List<MeshChunk>();
        List<MeshChunk> oldMeshChunks = new List<MeshChunk>(FindObjectsOfType<MeshChunk>());

        // Go through all coords and create a MeshChunk there if one doesn't already exist
        for (int x = 0; x < numMeshChunks.x; x++) {
            for (int y = 0; y < numMeshChunks.y; y++) {
                for (int z = 0; z < numMeshChunks.z; z++) {
                    Vector3Int coord = new Vector3Int(x, y, z);
                    bool MeshChunkAlreadyExists = false;

                    // If MeshChunk already exists, add it to the MeshChunks list, and remove from the old list.
                    for (int i = 0; i < oldMeshChunks.Count; i++) {
                        if (oldMeshChunks[i].coord == coord) {
                            MeshChunks.Add(oldMeshChunks[i]);
                            oldMeshChunks.RemoveAt(i);
                            MeshChunkAlreadyExists = true;
                            break;
                        }
                    }

                    // Create new MeshChunk
                    if (!MeshChunkAlreadyExists) {
                        var newMeshChunk = CreateMeshChunk(coord);
                        MeshChunks.Add(newMeshChunk);
                    }

                    MeshChunks[MeshChunks.Count - 1].SetUp(mat, generateColliders);

                }
            }
        }

        // Delete all unused MeshChunks
        for (int i = 0; i < oldMeshChunks.Count; i++) {
            oldMeshChunks[i].DestroyOrDisable();
        }
    }

    MeshChunk CreateMeshChunk(Vector3Int coord)
    {
        GameObject MeshChunk = new GameObject(GetNameFromCoord(coord));//$"{MeshChunkHolderName} MeshChunk ({coord.x}, {coord.y}, {coord.z})");
        MeshChunk.transform.parent = MeshChunkHolder.transform;
        MeshChunk newMeshChunk = MeshChunk.AddComponent<MeshChunk>();
        newMeshChunk.coord = coord;
        return newMeshChunk;
    }

    void OnValidate()
    {
        settingsUpdated = true;
    }

    struct Triangle
    {
#pragma warning disable 649 // disable unassigned variable warning
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;

        public Vector3 this[int i]
        {
            get {
                switch (i) {
                    case 0:
                        return a;
                    case 1:
                        return b;
                    default:
                        return c;
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        if (showBoundsGizmo) {
            Gizmos.color = boundsGizmoCol;

            List<MeshChunk> MeshChunks = (this.MeshChunks == null) ? new List<MeshChunk>(FindObjectsOfType<MeshChunk>()) : this.MeshChunks;
            foreach (var MeshChunk in MeshChunks) {
                Bounds bounds = new Bounds(CentreFromCoord(MeshChunk.coord), Vector3.one * boundsSize);
                Gizmos.color = boundsGizmoCol;
                Gizmos.DrawWireCube(CentreFromCoord(MeshChunk.coord), Vector3.one * boundsSize);
            }
        }
    }

}