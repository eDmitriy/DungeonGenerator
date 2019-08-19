//----------------------------------------------
// MeshBatcher
// Mark Hogan
// www.popupasylum.co.uk
//----------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MeshBatcher : MonoBehaviour
{
    private static Transform currTransform;

    class MeshStrip
    {
        public List<int> strip = new List<int> ( );
    }

    static List<GameObject> batchedResults = new List<GameObject> ( );

    public bool batchOnStart = false;

    /// <summary>
    /// Batchs the index of the lightmap.
    /// Meshes are divided into mesh strips (one strip per material)
    /// Meshes are given a lightmap index;
    /// Meshes with the same lightmap index and strips that use the same material can have thier strips batched.
    /// Lightmapped meshes have a second (full 01) uv set and renderers contain the offset/scale of it in the lightmap
    /// UV2s must therefore be adjusted to account for the fact that we can only have one offset
    /// </summary>
    /// <param name='lightmapIndex'>
    /// Lightmap index.
    /// </param>
    static GameObject BatchLightmapIndex ( int lightmapIndex )
    {
        List<MeshRenderer> meshRenderers = currTransform.GetComponentsInChildren<MeshRenderer> ( ).ToList();
        
        List<Material> materials = UniqueMaterialsForRenderers(meshRenderers.ToArray());

        MeshFilter[] meshFilters = currTransform.GetComponentsInChildren<MeshFilter> ( );
        meshFilters = RemoveDisabledRenderersAndNoneLightmap(meshFilters, lightmapIndex);
               
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<Vector2> uv1 = new List<Vector2>();
        List<Color> col = new List<Color>();
       
        List<MeshStrip> strips = new List<MeshStrip>();
        for (int i = 0; i<materials.Count; i++){
            strips.Add(new MeshStrip());
        }
               
        foreach (MeshFilter mf in meshFilters)
        {          
            int vertCount = verts.Count;
           
            // if the mesh filter has no mesh ignore it
            if (!mf || !mf.sharedMesh){
                break;
            }
           
            //if the result of adding this mesh would produce a mesh with a vertex count thats too high, create the current mesh and begin a new one
            if (mf.sharedMesh.vertexCount + vertCount > 65000)
            {
                MakeMeshChild("MeshLightmapIndex_" + lightmapIndex + "_" + vertCount,
                    verts.ToArray(),
                    norms.ToArray(),
                    uv.ToArray(),
                    uv1.ToArray(),
                    col.ToArray(),
                    strips.ToArray(),
                    materials.ToArray(),
                    lightmapIndex);
               
                verts.Clear();
                norms.Clear();
                uv.Clear();
                uv1.Clear();
                col.Clear();
               
                foreach (MeshStrip ms in strips)
                {
                    ms.strip.Clear();
                }
               
                vertCount = 0;
            }
           
            //ADDING VERTICIES
            Vector3[] newVerts = new Vector3[mf.sharedMesh.vertices.Length];
            var sharedMFVertices = mf.sharedMesh.vertices;
            for ( int i = 0; i </*mf.sharedMesh.vertices*/sharedMFVertices.Length; i++ )
            {
                newVerts[i] = currTransform.InverseTransformPoint ( mf.transform.TransformPoint ( /*mf.sharedMesh.vertices*/sharedMFVertices[i] ) );
            }
            verts.AddRange(newVerts);
           
            //ADDING STANDARD NORMALS
            norms.AddRange(mf.sharedMesh.normals);
           
            //ADDING STANDARD UVS
            uv.AddRange(mf.sharedMesh.uv);
           
            //CONVERTING INDIVIDUAL LIGHTMAP UVS TO THIS RENDERERS LIGHTMAP COORDS
            if (lightmapIndex>=0 && lightmapIndex<255){          
                Vector2[] lightmapUVs = mf.sharedMesh.uv2;
                Vector4 lightmapTilingOffset = mf.GetComponent<Renderer>()/*.renderer*/.lightmapScaleOffset;               
                Vector2 uvscale = new Vector2( lightmapTilingOffset.x, lightmapTilingOffset.y );           
                Vector2 uvoffset = new Vector2( lightmapTilingOffset.z, lightmapTilingOffset.w );
                for ( int j = 0; j < lightmapUVs.Length; j++ ) {           
                      lightmapUVs[j] = uvoffset + new Vector2( uvscale.x * lightmapUVs[j].x, uvscale.y * lightmapUVs[j].y );           
                }            
                uv1.AddRange(lightmapUVs);
            }
           
            //ACCOUNTING FOR MESHES WITHOUT VERTEX COLORS !!!!!!!!!!!!!!!!!!!!!!!!!!
            if (mf.sharedMesh.colors.Length == 0){
                Color[] replacementColors = new Color[mf.sharedMesh.vertexCount];
                for (int i = 0; i < mf.sharedMesh.vertexCount; i++){replacementColors[i] = Color.white;}
                col.AddRange(replacementColors);
            }
            else{
                col.AddRange(mf.sharedMesh.colors);
            }
           
            //ASSEMBLING TRIANGLE STRIPS
            for (int subMeshIndex = 0; subMeshIndex < mf.sharedMesh.subMeshCount && subMeshIndex < mf.GetComponent<Renderer>()/*.renderer*/.sharedMaterials.Length; subMeshIndex++)
            {
                int[] mfStrip = mf.sharedMesh.GetTriangles(subMeshIndex);
                if ( /*MeshIsFlipped(mf)*/ true){ //STRNGE REVERSED MESH
                   // mfStrip = ReverseTriangleWinding(mfStrip);
                }
                for(int i = 0; i < mfStrip.Length; i++)
                {
                    mfStrip[i] = mfStrip[i] + vertCount;
                }
                int stripIndex = materials.IndexOf ( mf.GetComponent<Renderer> ( )/*.renderer*/.sharedMaterials[subMeshIndex] );
                strips[stripIndex].strip.AddRange(mfStrip);
            }

            //NormalSolver.RecalculateNormals (mf.sharedMesh, 89 );
        }

        //REERSE NORMALS !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
/*        for (int i = 0; i < norms.Count; i++)
        {
            norms[i] *= -1;
        }*/

        var retGO = MakeMeshChild("MeshLightmapIndex_" + lightmapIndex + "_"  + verts.Count,
            verts.ToArray(),
            norms.ToArray(),
            uv.ToArray(),
            uv1.ToArray(),
            col.ToArray(),
            strips.ToArray(),
            materials.ToArray(),
            lightmapIndex);
       
        foreach (MeshFilter r in meshFilters){
            DestroyImmediate(r.GetComponent<Renderer>());
            DestroyImmediate(r);
        }

        return retGO;
    }

    static GameObject MakeMeshChild ( string name, Vector3[] verts, Vector3[] norms, Vector2[] uv0s, Vector2[] uv1s, Color[] cols, MeshStrip[] subMeshes, Material[] materials, int lightmapIndex )
    {
        //MAKE NEW MESH
        Mesh newMesh = new Mesh ( );
        newMesh.name = name;

        //APPLYING VERTICIES< NORMALS< UVS< UV2S< COLORS
        newMesh.vertices = verts;
        newMesh.normals = norms;
        //newMesh.uv = uv0s;
        if( uv0s.Length == verts.Length )
        {
            newMesh.uv = uv0s;
        }
        else
        {
            Vector2[] uv0s_new = new Vector2[verts.Length];
            for (int i = 0; i < uv0s.Length; i++)
            {
                uv0s_new[i] = uv0s[i];
            }

            newMesh.uv = uv0s_new;
        }
        if ( uv1s.Length == verts.Length )
        {
            newMesh.uv2 = uv1s;
        }
        newMesh.colors = cols; //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        //APPLYING SUBMESH TRIANGLE STRIPS
        newMesh.subMeshCount = materials.Length;
        for ( int i = 0; i < subMeshes.Length; i++ )
        {
            newMesh.SetTriangles ( subMeshes[i].strip.ToArray ( ), i );
        }

        //NormalSolver.RecalculateNormals ( newMesh, 89 );
        newMesh.RecalculateNormals();

        GameObject childMesh = new GameObject ( name );
        Transform cT = childMesh.transform;
        cT.parent = currTransform;
        cT.localPosition = Vector3.zero;
        cT.localScale = Vector3.one;
        cT.localRotation = Quaternion.identity;

        MeshFilter mfr = childMesh.AddComponent<MeshFilter> ( );
        mfr.sharedMesh = newMesh;

        MeshRenderer mr = childMesh.AddComponent<MeshRenderer> ( );
        mr.sharedMaterials = materials;
        mr.lightmapIndex = lightmapIndex;

        childMesh.layer = currTransform.gameObject.layer;

        batchedResults.Add ( childMesh );

        return childMesh;
    }

    int[] ReverseTriangleWinding ( int[] triangleStrip )
    {
        int[] copy = new int[triangleStrip.Length];
        for ( int i = 0; i < triangleStrip.Length; i += 3 )
        {
            copy[i] = triangleStrip[i];
            copy[i + 1] = triangleStrip[i + 2];
            copy[i + 2] = triangleStrip[i + 1];
        }

        return copy;
    }

    bool MeshIsFlipped ( MeshFilter m )
    {
        Vector3 mScale = m.transform.lossyScale;
        return ( mScale.x * mScale.y * mScale.z < 0 );
    }

    static MeshFilter[] RemoveDisabledRenderersAndNoneLightmap ( MeshFilter[] mfs, int removeMeshesWithLightmapIndexesThatDontMatchThisValue )
    {
        List<MeshFilter> filterList = new List<MeshFilter> ( );
        filterList.AddRange ( mfs );

        List<MeshFilter> dupFilterList = new List<MeshFilter> ( );
        dupFilterList.AddRange ( mfs );

        for ( int i = 0; i < mfs.Length; i++ )
        {
            Renderer rend = dupFilterList[i].GetComponent<Renderer> ( );
            
            if (rend==null || 
                (rend!=null && rend.enabled == false || rend.lightmapIndex != removeMeshesWithLightmapIndexesThatDontMatchThisValue) 
                )
            {
                filterList.Remove ( dupFilterList[i] );
            }
        }

        return filterList.ToArray ( );
    }

    static List<Material> UniqueMaterialsForRenderers ( MeshRenderer[] meshRenderers )
    {
        List<Material> materials = new List<Material>();
       
        foreach (MeshRenderer mr in meshRenderers){
            foreach (Material mat in mr.sharedMaterials){
                if (mat!=null &&  !materials.Contains(mat)){
                    materials.Add(mat);
                }
            }
        }
       
        return materials;
    }

    /// <summary>
    /// Gets the lightmap indicies of mesh renderers in children
    /// </summary>
    /// <returns>
    /// The lightmap indicies.
    /// </returns>
    static int[] GetLightmapIndicies ()
    {
        MeshRenderer[] mrs = currTransform.GetComponentsInChildren<MeshRenderer> ( );

        List<int> lightmapIndexes = new List<int> ( );

        foreach ( MeshRenderer mr in mrs )
        {
            if ( !lightmapIndexes.Contains ( mr.lightmapIndex ) )
            {
                lightmapIndexes.Add ( mr.lightmapIndex );
            }
        }

        return lightmapIndexes.ToArray ( );
    }

    
    
    
    public void Start ()
    {
        if ( batchOnStart /*&& ( batchedResults == null || batchedResults.Count == 0 ) */)
        {
            Batch (transform );
        }
    }

    [ContextMenu("BAtch")]
    void BatchTransform()
    {
        Batch (transform );
    }
    

    public static GameObject Batch (Transform batchParent)
    {
        currTransform = batchParent;

        //if (DuplicateBatch()){return;}
 
        foreach (int index in GetLightmapIndicies())
        {
            if (index >= -1 && index <= 255)
            {
                return BatchLightmapIndex(index);
            }
        }

        return null;
    }

/*    public static bool DuplicateBatch ()
    {
        foreach (MeshBatcher mb in FindObjectsOfType(typeof(MeshBatcher)) as MeshBatcher[]) {
            if ( mb.name == currTransform.name && mb.batchedResults.Count > 0 )
            {
                foreach (GameObject r in mb.batchedResults){
                    GameObject doop = Instantiate ( r, currTransform.position, currTransform.rotation ) as GameObject;
                    doop.transform.parent = currTransform;
                }
                return true;
            }
        }
       
        return false;
    }*/
}