using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static MainChunkHolder;

public class MeshChunk : MonoBehaviour
{
    //[HideInInspector] public bool IsSaveDataClear { get { return SaveData.Count < 29*29*29; } }
    [HideInInspector] public List<float4> SaveData = new List<float4>();
    public Vector3Int coord;

    [HideInInspector]
    public Mesh mesh;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    public MeshCollider meshCollider;
    bool generateCollider;

    public void DestroyOrDisable()
    {
        if (Application.isPlaying) {
            mesh.Clear();
            gameObject.SetActive(false);
        }
        else {
            DestroyImmediate(gameObject, false);
        }
    }

    public void UpdateColliders()
    {
        if (generateCollider) {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
            // force update
            meshCollider.enabled = false;
            meshCollider.enabled = true;
        }
    }

    public void UpdateChunk(bool updateFromEdit = false)
    {
        mainHolder.UpdateMeshChunkMesh(this,updateFromEdit);
    }

    // Add components/get references in case lost (references can be lost when working in the editor)
    public void SetUp(Material mat, bool generateCollider, bool isColliderConvex = false)
    {
        this.generateCollider = generateCollider;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        if (meshFilter == null) {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        if (meshRenderer == null) {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        if (meshCollider == null && generateCollider) {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }
        if (meshCollider != null && !generateCollider) {
            DestroyImmediate(meshCollider);
        }

        mesh = meshFilter.sharedMesh;
        if (mesh == null) {
            mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.sharedMesh = mesh;
        }

        if (generateCollider) {
            if (meshCollider.sharedMesh == null) {
                meshCollider.sharedMesh = mesh;
            }
            // force update
            meshCollider.enabled = false;
            meshCollider.enabled = true;
        }



        meshRenderer.material = mat;
    }
}