using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
//using static MainMeshHolder;
using static MainChunkHolder;


public class MouseLook : MonoBehaviour
{

    public float moveSpeed = 10;
    public float friction = 0.9f;
    public float sensitivity = 50;
    public int radius = 10;

    bool onPoint = false;
    Vector3Int placeAt = Vector3Int.zero;
  

   
    GameObject cam;
    MeshChunk meshChunk;
    List<MeshChunk> borderChunks = new List<MeshChunk>();


    public float mouseSensitivity = 300f;
    public Camera myEyes;
    public Transform playerBody;

    private float xRotation = 0f;
    // Start is called before the first frame update = test update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }


    int ConvertVec3ToInt(Vector3Int v, int _base = -1)
    {
        if (_base == -1) { _base = mainHolder.numPointsPerAxis; }
        return v.x + v.y * _base + v.z * _base * _base; // * 30 + v.y * 30 + v.z;
    }

    // Update is called once per frame
    void Update()
    {
        // ------------------- CURSOR -------------------


        // ------------------- LOOK AROUND -------------------

        if (Cursor.lockState == CursorLockMode.Locked) {

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            //Handles looking right and left on x axis
            playerBody.Rotate(Vector3.up * mouseX);

            //Handles looking up and down, clamps to 90 degrees up and down looking
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90, 90);
            transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
        }



     
       

        // ------------------- RAYCAST -------------------

        Vector3 forward = transform.forward;
        onPoint = false;
        RaycastHit hit;
       
        Debug.DrawRay(transform.position, forward * 1000, Color.blue);
        List<Vector3Int> reloadChunks = new List<Vector3Int>();
        List<Vector3Int> addChunks = new List<Vector3Int>();
        int size = mainHolder.numPointsPerAxis;

        List<Vector3Int> offsets = new List<Vector3Int>();
        // Does the ray intersect any objects excluding the player layer
        if (Physics.Raycast(transform.position, forward, out hit, Mathf.Infinity)) {
            Vector3 placePos = hit.point;
            //onPoint = true;
            placeAt = new Vector3Int(Mathf.RoundToInt(placePos.x), Mathf.RoundToInt(placePos.y), Mathf.RoundToInt(placePos.z));

            if ((meshChunk = hit.collider.gameObject.GetComponent<MeshChunk>()) != null) {
                onPoint = true;

                borderChunks.Clear();
                for (int i = 0; i < 27; i++) {
              //      borderChunks.Add(null);
                }
                for (int x = 0; x < 3; x++) {
                    for (int y = 0; y < 3; y++) {
                        for (int z = 0; z < 3; z++) {
                            Vector3Int offset = meshChunk.coord + new Vector3Int(x - 1, y - 1, z - 1);
                            offsets.Add(offset);
                            GameObject go = GameObject.Find(mainHolder.GetNameFromCoord(offset));
                            if (go != null) {
                                int place = ConvertVec3ToInt(new Vector3Int(x, y, z), 3);
                                //  print(place + "|" + offset);
                       //         borderChunks[place] = go.GetComponent<MeshChunk>();
                            }
                            else {

                                if (!addChunks.Contains(offset)) {
                           //        addChunks.Add(offset);
                                }
                            }
                        }
                    }
                }
                //Border(size);

                //borderChucks[ConvertVec3ToInt(Vector3Int.one,3)] = meshChunk;
                //print(borderChucks.Where(t => t != null).ToList().Count);
            }
        }

        // ------------------- BUTTONS -------------------


        if (Input.GetKey(KeyCode.Mouse0) || Input.GetKey(KeyCode.Mouse1)) {
            if (onPoint)
            {
                Vector3 offset = mainHolder.CentreFromCoord(meshChunk.coord);

                Vector3Int rPlaceAt = placeAt - new Vector3Int((int)offset.x, (int)offset.y, (int)offset.z) + new Vector3Int(size / 2, size / 2, size / 2);
                //  print("DATA: " + mg.storedData[ConvertVec3ToInt(placeAt)] + "|" + rPlaceAt);
                float remove = Input.GetKey(KeyCode.Mouse1) ? -1 : 1;
                List<int> updates = new List<int>();
                if (addChunks.Count == 0)
                {
                    for (int y = -radius; y < radius; y++)
                    {
                        for (int x = -radius; x < radius; x++)
                        {
                            for (int z = -radius; z < radius; z++)
                            {
                                Vector3 newPos = new Vector3(x, y, z);// - new Vector3(radius, radius, radius) * 0.5f;
                                                                      // print(newPos.sqrMagnitude);
                                if (Mathf.Abs(newPos.sqrMagnitude) < radius)
                                {

                                    Vector3Int newPlaceAt = rPlaceAt + new Vector3Int(x, y, z);
                                    Vector3Int chunkOffset = Vector3Int.one + new Vector3Int(0, 0, 0);
                                    List<Vector3Int> shareBorders = new List<Vector3Int>();

                                    // print(newPlaceAt);
                                    //print(newPlaceAt);
                                    if (newPlaceAt.x > size - 1) { chunkOffset.x++; }
                                    if (newPlaceAt.x < 0) { chunkOffset.x--; }
                                    if (newPlaceAt.y > size) { chunkOffset.y++; }
                                    if (newPlaceAt.y < 0) { chunkOffset.y--; }
                                    if (newPlaceAt.z > size) { chunkOffset.z++; }
                                    if (newPlaceAt.z < 1) { chunkOffset.z--; }

                                    newPlaceAt += new Vector3Int(0, -size, 0);
                                    newPlaceAt += (chunkOffset - Vector3Int.one) * -size;
                                    // newPlaceAt -= chunkOffset;
                                    int _chunkOffset = ConvertVec3ToInt(chunkOffset, 3);



                                    // try {
                                    //print(_chunkOffset);
                                    //print(_chunkOffset + "|" + chunkOffset);
                                    // var _meshChunk = borderChucks[_chunkOffset];

                                    float4 save = (borderChunks[_chunkOffset].SaveData[ConvertVec3ToInt(newPlaceAt)]);
                                    save.w += remove * 0.1f;
                                    //save.w *= remove;
                                    save.w = Vmath.MinMaxValue(save.w, -1, 1);
                                    //_meshChunk.SaveData[] = save; // new Vector4(newPlaceAt.x, newPlaceAt.y, newPlaceAt.z, Random.Range(-15,15));
                                    borderChunks[_chunkOffset].SaveData[ConvertVec3ToInt(newPlaceAt)] = save;
                                    if (!updates.Contains(_chunkOffset))
                                    {
                                        updates.Add(_chunkOffset);
                                    }
                                    //  print(save.x + "|" + save.y + "|" + save.z + "|" + save.w);
                                    //  }

                                    // catch (System.Exception) {
                                    //  if (!reloadChunks.Contains(offsets[_chunkOffset])) {
                                    //      reloadChunks.Add(offsets[_chunkOffset]);
                                    //  }
                                    //}




                                    // if (borderChucks[_chunkOffset] != null) {
                                    //     MeshHolder.mainHolder.HotReloadChunk(borderChucks[_chunkOffset].coord, borderChucks[_chunkOffset]);
                                    // }
                                    // else {
                                    //     if (!reloadChunks.Contains(offsets[_chunkOffset])) {
                                    //         reloadChunks.Add(offsets[_chunkOffset]);
                                    //     }
                                    //     //  MeshHolder.mainHolder.HotReloadChunk(offsets[_chunkOffset], null);
                                    //     print("error2");
                                    // }

                                }

                            }
                        }
                    }
                }

                if (reloadChunks.Count > 0)
                {
                    var meshes = GameObject.FindObjectsOfType<MeshChunk>().Where(t => reloadChunks.Contains(t.coord)).Select(t => t).ToList();
                    List<Vector3Int> doneChunks = new List<Vector3Int>();
                    for (int i = 0; i < meshes.Count; i++)
                    {
                        //string _name = MeshHolder.mainHolder.GetNameFromCoord(reloadChunks[i]);
                        if (doneChunks.Contains(meshes[i].coord))
                        {
                            print("REMOVED" + meshes[i].coord);
                            mainHolder.RemoveChunk(meshes[i]); // TO PREVENT DUPLICATES
                        }
                        else
                        {
                            doneChunks.Add(meshes[i].coord);
                            print("HOT RELOADED" + meshes[i].coord);
                            mainHolder.HotReloadChunk(meshes[i].coord, meshes[i]);

                        }
                    }
                }
                if (addChunks.Count > 0)
                {
                    for (int i = 0; i < addChunks.Count; i++)
                    {
                        GameObject g;
                        if ((g = GameObject.Find(mainHolder.GetNameFromCoord(addChunks[i]))) == null)
                        {
                            mainHolder.AddChuck(addChunks[i]);
                        }
                        else
                        {
                            print("HOT RELOAD");
                            mainHolder.HotReloadChunk(addChunks[i], g.GetComponent<MeshChunk>());
                        }
                    }

                }
                //    
                Border(size);



                // mg.storedData[ConvertVec3ToInt(placeAt)] = new Vector4(rPlaceAt.x, rPlaceAt.y, rPlaceAt.z, 15f);
                for (int i = 0; i < updates.Count; i++)
                {
                    borderChunks[updates[i]].UpdateChunk(updateFromEdit: true);
                }
            }
            else
            {
                Debug.Log("NOT ON POINT");
            }


        }
    }

    void Border(int size)
    {
        //Border chunks
        if (borderChunks[ConvertVec3ToInt(new Vector3Int(2, 1, 1), 3)] != null) {
            for (int z = 0; z < size; z++) {
                for (int y = 0; y < size; y++) {
                    Vector3Int chunkOffset = new Vector3Int(2, 1, 1);
                    float4 save = (meshChunk.SaveData[ConvertVec3ToInt(new Vector3Int(size - 1, y, z))]);
                    try {
                        borderChunks[ConvertVec3ToInt(chunkOffset, 3)].SaveData[ConvertVec3ToInt(new Vector3Int(0, y, z))] = save;
                    }
                    catch (System.Exception) {
                    }
                }
            }
        }
        if (borderChunks[ConvertVec3ToInt(new Vector3Int(0, 1, 1), 3)] != null) {
            for (int z = 0; z < size; z++) {
                for (int y = 0; y < size; y++) {
                    Vector3Int chunkOffset = new Vector3Int(0, 1, 1);
                    float4 save = (meshChunk.SaveData[ConvertVec3ToInt(new Vector3Int(0, y, z))]);
                    try {
                        borderChunks[ConvertVec3ToInt(chunkOffset, 3)].SaveData[ConvertVec3ToInt(new Vector3Int(size - 1, y, z))] = save;
                    }
                    catch (System.Exception) {
                    }
                }
            }
        }

        if (borderChunks[ConvertVec3ToInt(new Vector3Int(1, 1, 2), 3)] != null) {
            for (int x = 0; x < size; x++) {
                for (int y = 0; y < size; y++) {
                    Vector3Int chunkOffset = new Vector3Int(1, 1, 2);
                    float4 save = (meshChunk.SaveData[ConvertVec3ToInt(new Vector3Int(x, y, size - 1))]);
                    try {
                        borderChunks[ConvertVec3ToInt(chunkOffset, 3)].SaveData[ConvertVec3ToInt(new Vector3Int(x, y, 0))] = save;
                    }
                    catch (System.Exception) {
                    }
                }
            }
        }
        if (borderChunks[ConvertVec3ToInt(new Vector3Int(1, 1, 0), 3)] != null) {
            for (int x = 0; x < size; x++) {
                for (int y = 0; y < size; y++) {
                    Vector3Int chunkOffset = new Vector3Int(1, 1, 0);
                    float4 save = (meshChunk.SaveData[ConvertVec3ToInt(new Vector3Int(x, y, 0))]);
                    try {
                        borderChunks[ConvertVec3ToInt(chunkOffset, 3)].SaveData[ConvertVec3ToInt(new Vector3Int(x, y, size - 1))] = save;
                    }
                    catch (System.Exception) {
                    }
                }
            }
        }

        if (borderChunks[ConvertVec3ToInt(new Vector3Int(1, 2, 1), 3)] != null) {
            for (int x = 0; x < size; x++) {
                for (int z = 0; z < size; z++) {
                    Vector3Int chunkOffset = new Vector3Int(1, 2, 1);
                    float4 save = (meshChunk.SaveData[ConvertVec3ToInt(new Vector3Int(x, size - 1, z))]);
                    try {
                        borderChunks[ConvertVec3ToInt(chunkOffset, 3)].SaveData[ConvertVec3ToInt(new Vector3Int(x, 0, z))] = save;
                    }
                    catch (System.Exception) {
                    }
                }
            }
        }
        if (borderChunks[ConvertVec3ToInt(new Vector3Int(1, 0, 1), 3)] != null) {
            for (int x = 0; x < size; x++) {
                for (int z = 0; z < size; z++) {
                    Vector3Int chunkOffset = new Vector3Int(1, 0, 1);
                    float4 save = (meshChunk.SaveData[ConvertVec3ToInt(new Vector3Int(x, 0, z))]);
                    try {
                        borderChunks[ConvertVec3ToInt(chunkOffset, 3)].SaveData[ConvertVec3ToInt(new Vector3Int(x, size - 1, z))] = save;
                    }
                    catch (System.Exception) {
                    }
                }
            }
        }
    }
    List<float4> GetEmtyFloat4(Vector3Int coord)
    {
        int numPointsPerAxis = mainHolder.numPointsPerAxis;
        float4 add = new float4(coord.x * numPointsPerAxis, coord.y * numPointsPerAxis, coord.z * numPointsPerAxis, 0);
        var storedData = new List<float4>();
        for (int y = 0; y < numPointsPerAxis; y++) {
            for (int x = 0; x < numPointsPerAxis; x++) {
                for (int z = 0; z < numPointsPerAxis; z++) {
                    storedData.Add(new float4(x, y, z, -1) + add);
                }
            }
        }
        return storedData;
    }
}
