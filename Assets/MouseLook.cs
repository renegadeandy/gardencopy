using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System;
using System.Net.NetworkInformation;
using UnityEngine.Experimental.TerrainAPI;

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

    //new additions
    private Terrain terrain;
    private Vector2 terrainPoint, previousTerrainPoint;
    private Vector2 rectSize;
    private RenderTexture prevRenderTexture;
    private float height;
    public ComputeShader terrainPaintShader;
    private float smoothOffset = 0.02f;
    private Boolean baked = false;
    public Texture brushTexture;
    private List<TreeInstance> trees;
    private Transform brush;
    //end of new additions


    private float trowelDepth = -0.00025f;
    private float groundLevel = 0.5f;
    private float xRotation = 0f;
    // Start is called before the first frame update = test update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        groundLayerMask = LayerMask.NameToLayer("Ground");
        rectSize = new Vector2(4, 4);
        trees = new List<TreeInstance>();
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
            GetAreaToModify();
        }
       
    }


    void GetAreaToModify()
    {
        RaycastHit hit;
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, 1 << LayerMask.NameToLayer("Ground")))
        {
            Debug.Log("hello");
            if (!terrain)
                Debug.Log("gogo");
                terrain = hit.transform.GetComponent<Terrain>();
            terrainPoint = new Vector2((int)((hit.point.x - (rectSize.x * .5f)) - terrain.transform.position.x), (int)((hit.point.z - (rectSize.y * .5f)) - terrain.transform.position.z));

            if (terrainPoint != previousTerrainPoint && Vector2.Distance(terrainPoint, previousTerrainPoint) > 1f)
            {
                Rect prevRect = new Rect(previousTerrainPoint, rectSize);
                if (prevRect.height != 0 && prevRect.width != 0 && prevRenderTexture)
                    RestoreTerrain(prevRect);
                Rect rect = new Rect(terrainPoint, rectSize);
                ModifyTerrain(rect);
                previousTerrainPoint = terrainPoint;
                brush.position = new Vector3((int)hit.point.x, hit.point.y + 0.1f, (int)hit.point.z);
                
            }
        }
    }

    void ModifyTerrain(Rect selection)
    {
        
        //terrain = Terrain.activeTerrain;
        if (trees.Count > 0)
        {
            trees.AddRange(terrain.terrainData.treeInstances);
            terrain.terrainData.treeInstances = trees.ToArray();
            trees.Clear();
        }
        //Trees inside circle
        foreach (var tree in terrain.terrainData.treeInstances)
        {
            Vector2 p = new Vector2((tree.position.x * terrain.terrainData.size.x) + terrain.transform.position.x, (tree.position.z * terrain.terrainData.size.z) + terrain.transform.position.z);
            float d = Mathf.Sqrt(Mathf.Pow(p.x - transform.position.x, 2) + Mathf.Pow(p.y - transform.position.z, 2));
            if (d < rectSize.x * .5f)
                trees.Add(tree);
        }
        if (trees.Count > 0)
            terrain.terrainData.treeInstances = terrain.terrainData.treeInstances.Except(trees).ToArray();

        PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(terrain, selection);
        Debug.Log("Rect: " + selection + " contextRect: " + paintContext.pixelRect + "/" + paintContext.pixelSize);
        RenderTexture terrainRenderTexture = new RenderTexture(paintContext.sourceRenderTexture.width, paintContext.sourceRenderTexture.height, 0, RenderTextureFormat.R16);
        terrainRenderTexture.enableRandomWrite = true;
        Graphics.CopyTexture(paintContext.sourceRenderTexture, terrainRenderTexture);

        prevRenderTexture = new RenderTexture(paintContext.sourceRenderTexture.width, paintContext.sourceRenderTexture.height, 0, RenderTextureFormat.R16);
        Graphics.CopyTexture(paintContext.sourceRenderTexture, prevRenderTexture);

        float h0 = terrain.SampleHeight(new Vector3(selection.position.x + terrain.transform.position.x, 0, selection.position.y + terrain.transform.position.z));
        float h1 = terrain.SampleHeight(new Vector3(selection.position.x + terrain.transform.position.x, 0, selection.position.y + terrain.transform.position.z + rectSize.y));
        float h2 = terrain.SampleHeight(new Vector3(selection.position.x + terrain.transform.position.x + rectSize.x, 0, selection.position.y + terrain.transform.position.z + rectSize.y));
        float h3 = terrain.SampleHeight(new Vector3(selection.position.x + terrain.transform.position.x + rectSize.x, 0, selection.position.y + terrain.transform.position.z));

        height = (((h0 + h1 + h2 + h3) / 4f)/* + terrain.transform.position.y*/) / terrain.terrainData.size.y;

        terrainPaintShader.SetFloat("height", height * smoothOffset);
        terrainPaintShader.SetTexture(terrainPaintShader.FindKernel("CSMain"), "heightmap", terrainRenderTexture);
        terrainPaintShader.SetTexture(terrainPaintShader.FindKernel("CSMain"), "brush", brushTexture);

        terrainPaintShader.Dispatch(terrainPaintShader.FindKernel("CSMain"), (int)Mathf.Ceil(selection.width / 32), (int)Mathf.Ceil(selection.height / 32), 1);

        Graphics.CopyTexture(terrainRenderTexture, paintContext.destinationRenderTexture);

        TerrainPaintUtility.EndPaintHeightmap(paintContext, "Terrain");

        terrain.terrainData.SyncHeightmap();

    }

    void RestoreTerrain(Rect selection)
    {
        if (baked)
        {
            baked = false;
            return;
        }
        PaintContext prevPaintContext = TerrainPaintUtility.BeginPaintHeightmap(terrain, selection);

        Graphics.CopyTexture(prevRenderTexture, prevPaintContext.destinationRenderTexture);

        TerrainPaintUtility.EndPaintHeightmap(prevPaintContext, "Terrain");

        terrain.terrainData.SyncHeightmap();
    }

}
