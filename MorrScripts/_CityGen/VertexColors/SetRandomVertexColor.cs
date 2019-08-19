using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using Sirenix.OdinInspector;
using UnityEngine;
//using UnityEngine.ProBuilder;

public class SetRandomVertexColor : MonoBehaviour
{
    public int atlasSize = 4096;
    public int tileSize = 128;
    public float building_UV_ScaleMult = 4;

    public int uvTileStartIndexX = 0;
    public int uvTileStartIndexY = 0;
    public int uvTileEndIndexX = 8;
    public int uvTileEndIndexY = 8;
    
    public Material roofMat;

/*    private float step = 1;
    private int tileCountOnLine = 1;*/




    [/*Button()*/ContextMenu( "SetColors_PB" )]
    void SetColors_PB()
    {
        SetColors_PB(transform/*, atlasSize, tileSize*/);
    }


    public void SetColors_PB(Transform transform/*, int atlasSize, int tileSize, int xStartIndex = 0, int yStartIndex = 0*/ )
    {
        /*float step = 1f / ((float)atlasSize / (float)tileSize);
        //var tileCountOnLine = atlasSize / tileSize;

        
        var pb_Objects_list = transform.GetComponentsInChildren<Transform>()
            .Where(v => v.name.Contains("building"))
            .Where(v=> v.GetComponent<ProBuilderMesh>()!=null)
            .Select(v => v.GetComponent<ProBuilderMesh>())
            .ToList();

        var newColor = GenerateColor(uvTileStartIndexX, uvTileStartIndexY, uvTileEndIndexX, uvTileEndIndexY, step);
        building_UV_ScaleMult = 1 / building_UV_ScaleMult;

        foreach (var pb in pb_Objects_list)
        {
            //scale uvs
            var uvs = new List<Vector4>();
            pb.GetUVs(0, uvs );

            for (var i = 0; i < uvs.Count; i++)
            {
                uvs [ i ] *= building_UV_ScaleMult;
            }
            pb.SetUVs(0, uvs);
            //pb.RefreshUV(pb.faces);
            
            
            
            var vertices = pb.GetVertices();

            foreach (var vert in vertices)
            {
                vert.color = newColor;
            }
            pb.SetVertices(vertices, true);
            
            
            
            #region SetMaterials

            Renderer renderer = pb.gameObject.GetComponent<Renderer>();
            
            if (renderer != null)
            {
                if (Application.isPlaying)
                {
                    renderer.materials = new Material [ ] { renderer.material/*, roofMat #1#};
                    renderer.material.SetFloat( "_AtlasStep", step );
                }
                else
                {
                    renderer.sharedMaterials = new Material [ ] { renderer.sharedMaterial/*, roofMat#1# };
                    renderer.sharedMaterial.SetFloat( "_AtlasStep", step );
                }

                //renderer.material.SetFloat("_AtlasStep", step);
            }

            #endregion
            
            pb.Refresh(RefreshMask.All);

        }*/

        

    }

    [ContextMenu("SetRandomColors_MF_FromCurrent")]
    public void SetRandomColors_MF_FromCurrent()
    {
        SetColors_MF(
            GetComponent<MeshFilter>(), 
            roofMat, atlasSize, tileSize, building_UV_ScaleMult, 
            uvTileStartIndexX, uvTileStartIndexY, uvTileEndIndexX, uvTileEndIndexY
            );
    }


    public static void SetColors_MF( MeshFilter meshFilter, Material roofMat, int atlasSize, int tileSize, 
        float buildingsUVScaleMult, int xStartIndex = 0, int yStartIndex = 0, int xEndIndex = 8, int yEndIndex = 8 )
    {
        float step = 1f / ((float)atlasSize / (float)tileSize);
        var tileCountOnLine = atlasSize / tileSize;
        Mesh mesh;
        if (Application.isPlaying)
        {
            mesh = meshFilter.mesh;
        }
        else
        {
            mesh = meshFilter.sharedMesh;
        }

       

        Color [ ] colors = mesh.colors;
        //Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;
        Vector2[] uvs = mesh.uv;

        var newColor = GenerateColor( xStartIndex, yStartIndex, xEndIndex, yEndIndex,/*tileCountOnLine,*/ step);
        //newColor.b = Random.Range(0f, 1f);

        for( var i = 0; i < colors.Length; i++ )
        {
            colors [ i ] = newColor;
        }
        mesh.colors = colors;
        mesh.MarkDynamic();
        


        //ROOF TRIS
        List<int> roofTris = new List<int>();
        float triggerAngle = 90f;
        for (var i = 0; i < triangles.Length; i+=3)
        {
            //int trInd = triangles[i];
            if (Vector3.Angle(normals[ triangles [ i ] ], Vector3.up) < triggerAngle
                || Vector3.Angle( normals [ triangles [ i + 1 ] ], Vector3.up ) < triggerAngle
                || Vector3.Angle( normals [ triangles [ i + 2 ] ], Vector3.up ) < triggerAngle
                )
            {
                roofTris.Add( triangles [ i ] );
                roofTris.Add( triangles [ i+1 ] );
                roofTris.Add( triangles [ i + 2 ] );
            }
        }


        //scale uvs
        buildingsUVScaleMult = 1 / buildingsUVScaleMult;

        for (var i = 0; i < uvs.Length; i++)
        {
            uvs [ i ] *= buildingsUVScaleMult;
        }


/*        foreach (int roofTri in roofTris)
        {
            vertices[roofTri] += Vector3.up*5;
        }
        mesh.vertices = vertices;*/

        mesh.uv = uvs;
        
        //ROOF TRIS
        mesh.subMeshCount = 2;
        mesh.SetTriangles( roofTris, 1, true );
        //mesh.SetTriangles( triangles.Except(roofTris).ToArray(), 0, true );


        #region Swt mesh and materials

        if (Application.isPlaying)
        {
            meshFilter.mesh = mesh;
        }
        else
        {
            meshFilter.sharedMesh = mesh;
        }


        var renderer = meshFilter.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            if (Application.isPlaying)
            {
                renderer.materials = new Material [ ] { renderer.material/*, roofMat*/ };
                renderer.material.SetFloat( "_AtlasStep", step );
            }
            else
            {
                renderer.sharedMaterials = new Material [ ] { renderer.sharedMaterial, roofMat };
                renderer.sharedMaterial.SetFloat( "_AtlasStep", step );
            }
            
            
            //renderer.additionalVertexStreams = mesh;

            //renderer.material.SetFloat("_AtlasStep", step);
        }

        #endregion
    }





    static Color GenerateColor(int xStartIndex, int yStartIndex, int xEndIndex, int yEndIndex,  /*int tileCountOnLine,*/ float step )
    {
        return new Color( 
            GetRandomTileFloat( xStartIndex, /*tileCountOnLine*/xEndIndex, step ), 
            GetRandomTileFloat( yStartIndex, /*tileCountOnLine*/yEndIndex, step ), 
            0f, 1f 
            );
    }


    static float GetRandomTileFloat(int startIndex, int tileCountOnLine, float step )
    {
        return Random.Range( startIndex, tileCountOnLine) * step;
    }





    [/*Button()*/ContextMenu( "SetRoofMaterial" )]
    void SetRoofMaterial()
    {
/*        float step = 1f / ( (float)atlasSize / (float)tileSize );
        var tileCountOnLine = atlasSize / tileSize;


        var pb_Objects_list = transform.GetComponentsInChildren<Transform>()
            .Where(v => v.name.Contains("building"))
            .Where(v=> v.GetComponent<pb_Object>()!=null)
            .Select(v => v.GetComponent<pb_Object>())
            .ToList();


        foreach( pb_Object pb in pb_Objects_list )
        {
            pb.ToMesh();

            var normals = pb.GetNormals();
            foreach( pb_Face face in pb.faces )
            {
                #region Only faces that looks at sky eg RoofFaces

                List<Vector3> normalsSelectionPerFace = new List<Vector3>();
                foreach (int index in face.indices )
                {
                    normalsSelectionPerFace.Add( normals[index] );
                }

                Vector3 averageVector = Vector3.zero;
                foreach (Vector3 vector in normalsSelectionPerFace)
                {
                    averageVector += vector;
                }
                averageVector /= normalsSelectionPerFace.Count;

                if(Vector3.Angle(Vector3.up, averageVector) > 45) continue;

                #endregion


                if( roofMat != null ) face.material = roofMat;
            }

            pb.Refresh();
        }*/

    }



}
