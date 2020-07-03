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


    public float mouseSensitivity = 300f;
    public Camera myEyes;
    public Transform playerBody;

    public Terrain terrain;
    private TerrainData td;
    private float xRotation = 0f;
    // Start is called before the first frame update = test update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        td = terrain.terrainData;
    }



    // Update is called once per frame
    void Update()
    {
        

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
        Debug.DrawRay(transform.position, forward * 1000, Color.blue);


        

        RaycastHit hit;
        if (Input.GetMouseButton(0))
        {
            Ray ray = new Ray(transform.position, forward);
            if (Physics.Raycast(ray, out hit, 50f))
            {
                float relativeHitTerX = (hit.point.x - terrain.transform.position.x) / td.size.x;
                float relativeHitTerZ = (hit.point.z - terrain.transform.position.z) / td.size.z;

                float relativeTerCoordX = td.heightmapResolution * relativeHitTerX;
                float relativeTerCoordZ = td.heightmapResolution * relativeHitTerZ;

                int hitPointTerX = Mathf.FloorToInt(relativeTerCoordX);
                int hitPointTerZ = Mathf.FloorToInt(relativeTerCoordZ);

                //can we find just the area around the ray instead of updating the 513 x 513 resolution height map?!
                float[,] heights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
                Debug.Log("Resolution is:" + td.heightmapResolution+" array is:"+heights.Length);
                heights[hitPointTerZ, hitPointTerX] = heights[hitPointTerZ, hitPointTerX] * 0.90f;
                td.SetHeightsDelayLOD(0, 0, heights);
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            td.SyncHeightmap();
        }
    }

}
