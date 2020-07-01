using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using static MainChunkHolder;
//using static MainMeshChunkHolder;

#if false
[CustomEditor(typeof(MeshHolder))]
public class MeshHolder_customButton : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MeshGenerator myScript = (MeshHolder)target;
        if (GUILayout.Button("Save mesh to Data")) {
            //  myScript.SaveMeshToData();
        }
        if (GUILayout.Button("Clear Data")) {
            // myScript.ClearData();
        }
    }

}
#endif
[ExecuteInEditMode]
public class MainMeshHolder : MonoBehaviour
{
    public const float _pointSpacing = 1;
    public static MainMeshHolder mainHolder;

    public void SaveMeshToData()
    {
        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float)threadGroupSize);
        float pointSpacing = _pointSpacing; // boundsSize / (numPointsPerAxis - 1);

        Vector3 worldBounds = new Vector3(numChunks.x, numChunks.y, numChunks.z) * boundsSize;
        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;

        ComputeBuffer _buffer = new ComputeBuffer(numPoints, sizeof(float) * 4);

        densityGenerator.Generate(_buffer, numPointsPerAxis, boundsSize, worldBounds, Vector3.zero, offset, pointSpacing);

        Vector4[] data = new Vector4[numPointsPerAxis * numPointsPerAxis * numPointsPerAxis];
        _buffer.GetData(data);
        storedData = data.ToList();
    }

    public void ClearData()
    {
        storedData = new List<Vector4>();
        for (int y = 0; y < numPointsPerAxis; y++) {
            for (int x = 0; x < numPointsPerAxis; x++) {
                for (int z = 0; z < numPointsPerAxis; z++) {
                    storedData.Add(new Vector4(x, y, z, -1));
                }
            }
        }
    }


    const int threadGroupSize = 8;

    //[Header("General Settings"), ConditionalHide(nameof(storeData), false)]
    public DensityGenerator densityGenerator;
    //public bool storeData = false;


    public bool fixedMapSize;
    [ConditionalHide(nameof(fixedMapSize), true)]
    public Vector3Int numChunks = Vector3Int.one;
    [ConditionalHide(nameof(fixedMapSize), false)]
    public Transform viewer;
    [ConditionalHide(nameof(fixedMapSize), false)]
    public float viewDistance = 30;
    public int layerMask = 0;

    [Space()]
    public bool autoUpdateInEditor = true;
    public bool autoUpdateInGame = true;
    public ComputeShader shader;
    public Material mat;
    public bool generateColliders;
    [ConditionalHide(nameof(generateColliders), true)]
    public bool isColliderConvex = false;

    [Header("Voxel Settings")]
    public float isoLevel;
    public float boundsSize = 1;
    public Vector3 offset = Vector3.zero;

    [Range(2, 300)]
    public int numPointsPerAxis = 30;

    [Header("Gizmos")]
    public bool showBoundsGizmo = true;
    public Color boundsGizmoCol = Color.white;

    GameObject chunkHolder;
    public string chunkHolderName = "Chunks Holder";
    List<MeshChunk> chunks;
    Dictionary<Vector3Int, MeshChunk> existingChunks;
    Queue<MeshChunk> recycleableChunks;

    // Buffers
    ComputeBuffer triangleBuffer;
    ComputeBuffer pointsBuffer;
    ComputeBuffer triCountBuffer;

    bool settingsUpdated;

    [HideInInspector]
    public List<Vector4> storedData = new List<Vector4>();

    void Awake()
    {
        mainHolder = this;
        if (Application.isPlaying && !fixedMapSize) {
            InitVariableChunkStructures();

            var oldChunks = FindObjectsOfType<MeshChunk>();
            for (int i = oldChunks.Length - 1; i >= 0; i--) {
                Destroy(oldChunks[i].gameObject);
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
            InitChunks();
            UpdateAllChunks();

        }
        else {
            if (Application.isPlaying) {
                InitVisibleChunks();
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

    void InitVariableChunkStructures()
    {
        recycleableChunks = new Queue<MeshChunk>();
        chunks = new List<MeshChunk>();
        existingChunks = new Dictionary<Vector3Int, MeshChunk>();
    }

    void InitVisibleChunks()
    {
        if (chunks == null) {
            return;
        }
        CreateChunkHolder();

        Vector3 p = viewer.position;
        Vector3 ps = p / boundsSize;
        Vector3Int viewerCoord = new Vector3Int(Mathf.RoundToInt(ps.x), Mathf.RoundToInt(ps.y), Mathf.RoundToInt(ps.z));

        int maxChunksInView = Mathf.CeilToInt(viewDistance / boundsSize);
        float sqrViewDistance = viewDistance * viewDistance;

        // Go through all existing chunks and flag for recyling if outside of max view dst
        for (int i = chunks.Count - 1; i >= 0; i--) {
            MeshChunk chunk = chunks[i];
            Vector3 centre = CentreFromCoord(chunk.coord);
            Vector3 viewerOffset = p - centre;
            Vector3 o = new Vector3(Mathf.Abs(viewerOffset.x), Mathf.Abs(viewerOffset.y), Mathf.Abs(viewerOffset.z)) - Vector3.one * boundsSize / 2;
            float sqrDst = new Vector3(Mathf.Max(o.x, 0), Mathf.Max(o.y, 0), Mathf.Max(o.z, 0)).sqrMagnitude;
            if (sqrDst > sqrViewDistance) {
                existingChunks.Remove(chunk.coord);
                recycleableChunks.Enqueue(chunk);
                chunks.RemoveAt(i);
            }
        }

        for (int x = -maxChunksInView; x <= maxChunksInView; x++) {
            for (int y = -maxChunksInView; y <= maxChunksInView; y++) {
                for (int z = -maxChunksInView; z <= maxChunksInView; z++) {
                    Vector3Int coord = new Vector3Int(x, y, z) + viewerCoord;

                    if (existingChunks.ContainsKey(coord)) {
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
                            if (recycleableChunks.Count > 0) {


                                MeshChunk chunk = recycleableChunks.Dequeue();
                                //SaveSystem<List<float4>>.Save(chunk.SaveData, gameObject, "SAVE" + chunk.coord);

                                chunk.coord = coord;
                            //    chunk.SaveData = new List<float4>();
                                existingChunks.Add(coord, chunk);
                                chunks.Add(chunk);
                                UpdateChunkMesh(chunk);
                            }
                            else {
                                MeshChunk chunk = CreateChunk(coord);
                                chunk.coord = coord;
                             //   chunk.SaveData = new List<float4>();
                                chunk.SetUp(mat, generateColliders, isColliderConvex);
                                existingChunks.Add(coord, chunk);
                                chunks.Add(chunk);
                                UpdateChunkMesh(chunk);
                            }
                        }
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

    public string GetNameFromCoord(Vector3Int coord)
    {
        return "MeshChunk " + coord;
    }

    public void UpdateChunkMesh(MeshChunk chunk, bool updateFromEdit = false)
    {
        if (chunk == null) return;

        var currentQ = 0;
        Vector3Int coord = chunk.coord;
        /*
        if (qActivated) {
            inQ++;
            currentQ = int.Parse(inQ.ToString());
            activeQ.Add(currentQ);
            while (activeQ.Min() != currentQ && !updateFromEdit) {
                yield return null;
            }

            if (coord.y == 0) {
                yield return null;
                yield return null;
            }
        }*/

        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float)threadGroupSize);
        float pointSpacing = _pointSpacing; // boundsSize / (numPointsPerAxis - 1);

        chunk.name = GetNameFromCoord(coord);

        Vector3 centre = CentreFromCoord(coord);

        Vector3 worldBounds = new Vector3(numChunks.x, numChunks.y, numChunks.z) * boundsSize;

        /*
        if (storeData && storedData.Count == Mathf.Pow(numPointsPerAxis, 3)) {
            pointsBuffer.SetData(storedData);
        }
        else {
            densityGenerator.Generate(pointsBuffer, numPointsPerAxis, boundsSize, worldBounds, centre, offset, pointSpacing);
            Vector4[] data = new Vector4[numPointsPerAxis * numPointsPerAxis * numPointsPerAxis];
            pointsBuffer.GetData(data);
            storedData.RemoveRange(0, storedData.Count);
            storedData.AddRange(data);
        }*/
        // if (storeData) {
        //print("Fooo");



        if (updateFromEdit) {
           // pointsBuffer.SetData(chunk.SaveData);
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
            int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis; float4[] data = new float4[numPointsPerAxis * numPointsPerAxis * numPointsPerAxis];
            pointsBuffer.GetData(data);
           // chunk.SaveData = data.ToList();
            //SaveSystem<List<float4>>.Save(chunk.SaveData, gameObject, "SAVE" + coord);
            //}

        }





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

        Mesh mesh = chunk.mesh;
        mesh.Clear();

        var vertices = new Vector3[numTris * 3];
        var meshTriangles = new int[numTris * 3];

        for (int i = 0; i < numTris; i++) {
            for (int j = 0; j < 3; j++) {
                meshTriangles[i * 3 + j] = i * 3 + j;

                vertices[i * 3 + j] = tris[i][j];

                /*
                vertices[i * 3 + j].y -= Mathf.RoundToInt((vertices[i * 3 + j].y + 1) / 3);
                vertices[i * 3 + j].y -= Mathf.RoundToInt((vertices[i * 3 + j].y - 0.5f) / 2);
                vertices[i * 3 + j].x -= Mathf.RoundToInt((vertices[i * 3 + j].x + 1) / 3);
                vertices[i * 3 + j].x -= Mathf.RoundToInt((vertices[i * 3 + j].x - 0.5f) / 2);
                vertices[i * 3 + j].z -= Mathf.RoundToInt((vertices[i * 3 + j].z + 1) / 3);
                vertices[i * 3 + j].z -= Mathf.RoundToInt((vertices[i * 3 + j].z - 0.5f) / 2);
                
    */
            }
        }
        mesh.vertices = vertices;
        mesh.triangles = meshTriangles;
        mesh.RecalculateNormals();


        Vector3 bounds = chunk.mesh.bounds.size;
        bool validBounds = GetIfValidBounds(bounds);


        if (!validBounds) {
            print("NON VALID BOUNDS");
            HotReloadChunk(coord, chunk);
            return;
        }
        bool validMesh = GetIfValidMesh(chunk.meshCollider.sharedMesh);

        if (!validMesh) {
            print("NON VALID MESH");
            HotReloadChunk(coord, chunk);
            return;
        }

        if (validBounds && validMesh) {
            // force update
            chunk.meshCollider.convex = true;
            chunk.gameObject.layer = 10;
            StartCoroutine(WaitOneFrame(() => {
                try {
                    if (chunk != null) {
                        if (GetIfValidMesh(chunk.meshCollider.sharedMesh) && GetIfValidBounds(chunk.mesh.bounds.size)) {
                            chunk.meshCollider.convex = isColliderConvex;
                            chunk.gameObject.layer = 0;
                        }
                    }
                }
                catch (System.Exception) {

                }
            }));

            //print(">> " + chunk.meshCollider.sharedMesh.GetBaseVertex(0));
            /*
            chunk.meshCollider.sharedMesh = null;
            chunk.meshCollider.sharedMesh = mesh;
            // force update
            chunk.meshCollider.enabled = false;
            chunk.meshCollider.enabled = true;*/





            if (vertices.Length < 10 && !updateFromEdit) {
                // print(">> " + coord);
                StartCoroutine(WaitForCheck(chunk));
            }
            else {
                mesh.Optimize();
            }

        }

        if (qActivated) {

            //  yield return new WaitForSecondsRealtime(0.1f);
            activeQ.Remove(currentQ);
        }
    }

    bool GetIfValidBounds(Vector3 bounds)
    {
        return (bounds.x >= 0 && bounds.x <= numPointsPerAxis && bounds.y >= 0 && bounds.y <= numPointsPerAxis && bounds.z >= 0 && bounds.z <= numPointsPerAxis);
    }

    bool GetIfValidMesh(Mesh mesh)
    {
        for (int i = 0; i < Mathf.Min(mesh.vertices.Length, 10); i++) {
            if (float.IsNaN(mesh.vertices[i].x) || float.IsNaN(mesh.vertices[i].y) || float.IsNaN(mesh.vertices[i].z)) {
                return false;
            }
        }
        return true;
    }

    static int inQ = 0;
    static List<int> activeQ = new List<int>();

    const bool qActivated = false; // Q CHUCKS TO REMOVE GHOST CHUCKS, NOT 100% WORKING :(
    /*
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
    }*/

    public void RemoveChunk(MeshChunk old)
    {
        chunks.Remove(old);
        recycleableChunks.Enqueue(old);
        existingChunks.Remove(old.coord);
        Destroy(old.gameObject);
    }


    public void AddChuck(Vector3Int coord)
    {
        if (existingChunks.ContainsKey(coord)) {
            RemoveChunk(existingChunks[coord]);
        }
        MeshChunk _chunk = CreateChunk(coord);
        _chunk.coord = coord;
        //_chunk.SaveData = new List<float4>();
        _chunk.SetUp(mat, generateColliders, isColliderConvex);
        existingChunks.Add(coord, _chunk);
        chunks.Add(_chunk);

        UpdateChunkMesh(_chunk);

    }
    public void HotReloadChunk(Vector3Int coord, MeshChunk old)
    {
        RemoveChunk(old);
        // old.transform.position = new Vector3(0, 1, 0);
        AddChuck(coord);


        print("UPDATED");
    }

    IEnumerator WaitForCheck(MeshChunk chunk) // YOU SHOULD NOT DO THIS; ONLY USED BY GHOST CHUNKS, HAVE NOW FOUND BETTER WAY OF DOING IT, CAN DRAIN PREFORMENCE A LOT
    {
        yield return new WaitForSecondsRealtime(0.5f);

      //  chunk.mesh.RecalculateNormals();

        //print(">>" + GetIfValidMesh(chunk.meshCollider.sharedMesh) + "|" + GetIfValidBounds(chunk.meshCollider.bounds.size));
        // yield return null;
        //   var save = chunk.SaveData[0];
        //   chunk.SaveData[0] = new float4(save.x,save.y,save.z, Mathf.Min( save.w + 0.1f,1));
        //   UpdateChunkMesh(chunk, true);
        //  print(chunk.coord);

    }
    IEnumerator WaitOneFrame(System.Action a)
    {
        yield return null;
        a();
    }

    public void UpdateAllChunks()
    {
        // Create mesh for each chunk
        foreach (MeshChunk chunk in chunks) {
            UpdateChunkMesh(chunk);
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
            Vector3 totalBounds = (Vector3)numChunks * boundsSize;
            return -totalBounds / 2 + (Vector3)coord * boundsSize + Vector3.one * boundsSize / 2;
        }

        return new Vector3(coord.x, coord.y, coord.z) * boundsSize;
    }

    void CreateChunkHolder()
    {
        // Create/find mesh holder object for organizing chunks under in the hierarchy
        if (chunkHolder == null) {
            if (GameObject.Find(chunkHolderName)) {
                chunkHolder = GameObject.Find(chunkHolderName);
            }
            else {
                chunkHolder = new GameObject(chunkHolderName);
            }
            chunkHolder.layer = layerMask;
        }
    }

    // Create/get references to all chunks
    void InitChunks()
    {
        CreateChunkHolder();
        chunks = new List<MeshChunk>();
        List<MeshChunk> oldChunks = new List<MeshChunk>(FindObjectsOfType<MeshChunk>());

        // Go through all coords and create a chunk there if one doesn't already exist
        for (int x = 0; x < numChunks.x; x++) {
            for (int y = 0; y < numChunks.y; y++) {
                for (int z = 0; z < numChunks.z; z++) {
                    Vector3Int coord = new Vector3Int(x, y, z);
                    bool chunkAlreadyExists = false;

                    // If chunk already exists, add it to the chunks list, and remove from the old list.
                    for (int i = 0; i < oldChunks.Count; i++) {
                        if (oldChunks[i].coord == coord) {
                            chunks.Add(oldChunks[i]);
                            oldChunks.RemoveAt(i);
                            chunkAlreadyExists = true;
                            break;
                        }
                    }

                    // Create new chunk
                    if (!chunkAlreadyExists) {
                        var newChunk = CreateChunk(coord);
                        chunks.Add(newChunk);
                    }

                    chunks[chunks.Count - 1].SetUp(mat, generateColliders, isColliderConvex);
                }
            }
        }

        // Delete all unused chunks
        for (int i = 0; i < oldChunks.Count; i++) {
            oldChunks[i].DestroyOrDisable();
        }
    }

    MeshChunk CreateChunk(Vector3Int coord)
    {
        GameObject chunk = new GameObject(GetNameFromCoord(coord));//$"MeshChunk ({coord.x}, {coord.y}, {coord.z})");
        chunk.transform.parent = chunkHolder.transform;
        MeshChunk newChunk = chunk.AddComponent<MeshChunk>();
        newChunk.coord = coord;
        return newChunk;
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

            List<MeshChunk> chunks = (this.chunks == null) ? new List<MeshChunk>(FindObjectsOfType<MeshChunk>()) : this.chunks;
            foreach (var chunk in chunks) {
                Bounds bounds = new Bounds(CentreFromCoord(chunk.coord), Vector3.one * boundsSize);
                Gizmos.color = boundsGizmoCol;
                Gizmos.DrawWireCube(CentreFromCoord(chunk.coord), Vector3.one * boundsSize);
            }
        }
    }

}