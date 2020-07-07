using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
//using static MainMeshHolder;
using static MainChunkHolder;
using System;
using System.Net.NetworkInformation;

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

                trowelDig(heights, hitPointTerZ, hitPointTerX,piece);
                
                
               
                
            }
        }
       
    }


    public void outputGrid(int centerPointZ, int centerPointX)
    {
        for (int x = centerPointX - 2; x <= centerPointX + 2; x++)
        {
            String output = "";
            for (int z = centerPointZ - 2; z <= centerPointZ + 2; z++)
            {
               output +="|" + z+","+x;
            }
            output += "|";
            Debug.Log(output);
        }
    }
    //make this work neater when it hits boundaries of terrain (between terrain pieces and outer edges of terrain :) 
    private void trowelDig(float[,] heights, int centerPointZ, int centerPointX,Terrain t)
    {
        this.outputGrid(centerPointZ, centerPointX);
        Debug.Log("Center point Z is:" + centerPointZ + " Center point X is:" + centerPointX);

       
        for(int x = centerPointX -2;x <= centerPointX+2; x++)
        {
            for (int z = centerPointZ - 2; z <= centerPointZ + 2; z++)
            {
               
                //if we are off this tile,get the neighboring piece and adjust that too.
                Boolean edgeSplitZ = false;
                Boolean edgeSplitX = false;
                Terrain chunkZ = null;
                Terrain chunkX = null;
                int newXOffset = 0;
                int newZOffset = 0;
                this.resetHoleCalcs();
                //find out if our hit location crosses a terrain boundary
                if (z > 64)
                {
                    chunkZ = this.getNeighbor(t, "top");
                    newZOffset = 0 + z - 65;
                    edgeSplitZ = true;

                }
                else if (z < 0)
                {
                    chunkZ = this.getNeighbor(t, "bottom");
                    newZOffset = 65 + z;
                    edgeSplitZ = true;
                }
                if (x > 64)
                {
                    chunkX = this.getNeighbor(t, "right");
                    newXOffset = 0 + x - 65;
                    edgeSplitX = true;
                }
                else if (x < 0)
                {

                    chunkX = this.getNeighbor(t, "left");
                    newXOffset = 65 + x;
                    edgeSplitX = true;
                }
                if(z > 0 && z<= 64 && x > 0 && x <= 64)
                {
                    if (z == centerPointX && x == centerPointZ)
                    {
                        heights[z, x] = this.getNewHeight("bottomPoint", heights[z, x]);
                    }
                    //other surrounding point
                    else
                    {
                        heights[z, x] = this.getNewHeight("slope", heights[z, x]);
                    }
                    t.terrainData.SetHeightsDelayLOD(0, 0, heights);
                    t.terrainData.SyncHeightmap();

                }
                //handle depth where it crosses a terrain boundary
                //if (edgeSplitZ || edgeSplitX)
                // {




                //Handle the case where just x is on the next terrain section
                if (edgeSplitX && !edgeSplitZ)
                    {
                        if (chunkX == null)
                        {
                            Debug.Log("Terrain X neighbor doesn't exist - continuing loop");
                            continue;
                        }
                        TerrainData td = chunkX.terrainData;
                        float[,] otherHeightsX = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
                        Debug.Log("Z offset is:" + z + " new X Offset is:" + newXOffset);
                        otherHeightsX[z, newXOffset] = this.getNewHeight("slope", otherHeightsX[z, newXOffset]);
                        td.SetHeightsDelayLOD(0, 0, otherHeightsX);
                        td.SyncHeightmap();
                    }
                    //Handle the case where just z is on the next terrain section
                    else if (edgeSplitZ && !edgeSplitX)
                    {
                        if (chunkZ == null)
                        {
                            Debug.Log("Terrain Z neighbor doesn't exist - continuing loop");
                            continue;
                        }
                        TerrainData td = chunkZ.terrainData;
                        float[,] otherHeightsZ = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
                        Debug.Log("new Z offset is:" + newZOffset + " and X Offset is:" + x);
                        otherHeightsZ[newZOffset, x] = this.getNewHeight("slope", otherHeightsZ[newZOffset, x]);
                        td.SetHeightsDelayLOD(0, 0, otherHeightsZ);
                        td.SyncHeightmap();
                    }
                    //check where both z and x are on the next chunk
                    else
                    {
                        if (chunkZ == null || chunkX == null)
                        {
                           // Debug.Log("Terrain Z or Terrain X neighbor doesn't exist - continuing loop");
                            continue;
                        }
                        TerrainData td = chunkZ.terrainData;
                        float[,] otherHeightsZ = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
                        otherHeightsZ[newZOffset, x] = this.getNewHeight("slope", otherHeightsZ[newZOffset, x]); 
                        td.SetHeightsDelayLOD(0, 0, otherHeightsZ);
                        TerrainData td2 = chunkX.terrainData;
                        float[,] otherHeightsX = td2.GetHeights(0, 0, td2.heightmapResolution, td2.heightmapResolution);
                        otherHeightsX[z, newXOffset] = this.getNewHeight("slope", otherHeightsX[z, newXOffset]);
                        td2.SetHeightsDelayLOD(0, 0, otherHeightsX);
                        td.SyncHeightmap();
                    }
              //  }
                //all part of the same terrain piece
               // else
               // {

                    //center point
                  
              //  }
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
       

        
    }

    private float slopeVal;
    private float bottomVal;
    private Boolean setSlope = false;
    private Boolean setBottom = false;
    private float getNewHeight(String area,float original)
    {

       
        if (area.Equals("slope"))
        {
            if (setSlope)
            {
                return slopeVal;
            }
            else
            {
                slopeVal = original + (trowelDepth / 2);
                setSlope = true;
                return slopeVal;
            }
               
        }
        else if (area.Equals("bottomPoint")){
            if (setBottom)
            {
                return bottomVal;
            }
            else
            {
                bottomVal = original + trowelDepth;
                setBottom = true;
                return bottomVal;
            }
        }
        return 0f;
    }

    public void resetHoleCalcs()
    {
        setSlope = false;
        setBottom = false;
    }

    public Terrain getNeighbor(Terrain t, String requestedSide)
    {
        if (requestedSide.Equals("top"))
        {
            if (t.topNeighbor == null)
            {
                return null;
            }
            else
            {
                return t.topNeighbor.GetComponent<Terrain>();
            }
        }else if (requestedSide.Equals("right"))
        {
            if( t.rightNeighbor == null)
            {
                return null;
            }else
            {
                return t.rightNeighbor.GetComponent<Terrain>();
            }
        }else if (requestedSide.Equals("bottom"))
        {
            if (t.bottomNeighbor == null)
            {
                return null;
            }
            else
            {
                return t.bottomNeighbor.GetComponent<Terrain>();
            }
        }else if (requestedSide.Equals("left"))
        {
            if( t.leftNeighbor== null)
            {
                return null;
            }else
            {
                return t.leftNeighbor.GetComponent<Terrain>();
            }
        }
        return null;
    }

}
