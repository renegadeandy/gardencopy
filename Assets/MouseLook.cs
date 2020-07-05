using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
//using static MainMeshHolder;
using static MainChunkHolder;
using System;

public class MouseLook : MonoBehaviour
{

    public float moveSpeed = 10;
    public float friction = 0.9f;
    public float sensitivity = 50;
    public int radius = 10;

    private int groundLayerMask;
    public float mouseSensitivity = 300f;
    public Camera myEyes;
    public Transform playerBody;

    private float trowelDepth = -0.00025f;
    private float groundLevel = 0.5f;
    private float xRotation = 0f;
    // Start is called before the first frame update = test update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        groundLayerMask = LayerMask.NameToLayer("Ground");
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
        TerrainData td = null;
       
        if (Input.GetMouseButtonDown(0))
        {
            int layerMask = 1 << groundLayerMask;
            Ray ray = new Ray(transform.position, forward);
            if (Physics.Raycast(ray, out hit, 50f,layerMask))
            {
                Terrain piece = (Terrain)hit.collider.GetComponent<Terrain>();
                 td = piece.terrainData;
             

                //it looks like the orientation is wrong - top left goes bottom right!? maybe not -- as the X of the player decreases, the Z of the detected hit reduces instead of the X? Why?
                float relativeHitTerX = (hit.point.x - piece.transform.position.x) / td.size.x;
                float relativeHitTerZ = (hit.point.z - piece.transform.position.z) / td.size.z;

                float relativeTerCoordX = td.heightmapResolution * relativeHitTerX;
                float relativeTerCoordZ = td.heightmapResolution * relativeHitTerZ;
               
                int hitPointTerX = Mathf.FloorToInt(relativeTerCoordX);
                int hitPointTerZ = Mathf.FloorToInt(relativeTerCoordZ);

        
                float[,] heights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);

                float[,] newHeights = trowelDig(heights, hitPointTerZ, hitPointTerX);
                
                
                td.SetHeightsDelayLOD(0, 0, heights);
                
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (td != null)
            {
                td.SyncHeightmap();
             
            }
        }
    }

    //make this work neater when it hits boundaries of terrain (between terrain pieces and outer edges of terrain :) 
    private float[,] trowelDig(float[,] heights, int centerPointZ, int centerPointX)
    {
        //reduce area 3x3
        for(int i = centerPointX -2;i <= centerPointX+2; i++)
        {
            for(int p = centerPointZ-2; p<= centerPointZ + 2; p++)
            {

                if (i == centerPointX && p == centerPointZ)
                {
                    heights[centerPointZ, centerPointX] = heights[centerPointZ, centerPointX] + trowelDepth;
                }
                else
                {
                    heights[centerPointZ, centerPointX] = heights[centerPointZ, centerPointX] + (trowelDepth / 2);
                }
            }
         
        }

        
        //Debug.Log("Hit point height before adjusting is:" + heights[centerPointZ, centerPointX]);
        //if (Math.Abs(heights[centerPointZ, centerPointX] - 0.5f) < 0.005f)
       // {
        //    heights[centerPointZ, centerPointX] = heights[centerPointZ, centerPointX] + trowelDepth;
       //     Debug.Log("Terrain is close to ground level - setting to trowl depth:" + trowelDepth + " we are now at height:" + heights[centerPointZ, centerPointX]);
       // }
       // else
      //  {
       //     heights[centerPointZ, centerPointX] = heights[centerPointZ, centerPointX] + trowelDepth;
       //     Debug.Log("Terrain set to:" + heights[centerPointZ, centerPointX]);
      //  }
        return heights;
    }

}
