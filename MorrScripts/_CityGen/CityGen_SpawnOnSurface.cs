using UnityEngine;
using System.Collections;

public class CityGen_SpawnOnSurface : MonoBehaviour
{
    private MeshFilter meshFilter;
    private Mesh currMesh;
    private Vector3[] vertices;
    private Vector3[] triangles;
    Vector3[] trisVerts = new Vector3[3];
    private Vector3 randomPos;


    public float spawnCount = 20;
    public bool faceNormals = false;


	// Use this for initialization
	void Start ()
	{
	    meshFilter = GetComponent<MeshFilter>();
	    currMesh = meshFilter.mesh;

        for (int i = 0; i < currMesh.triangles.Length - 1; i += 3)
        {
            trisVerts[0] = transform.TransformPoint(currMesh.vertices[currMesh.triangles[i]]);
            trisVerts[1] = transform.TransformPoint(currMesh.vertices[currMesh.triangles[i+1]]);
            trisVerts[2] = transform.TransformPoint(currMesh.vertices[currMesh.triangles[i+2]]);

            for (int j = 0; j < spawnCount; j++)
            {
                randomPos = trisVerts[0];
                randomPos = Vector3.Lerp(randomPos, trisVerts[1], Random.Range(0f, 1f));
                randomPos = Vector3.Lerp(randomPos, trisVerts[2], Random.Range(0f, 1f));

                Vector3 normal = Vector3.Cross(trisVerts[0] - trisVerts[1], trisVerts[1] - trisVerts[2]);

                /*Debug.DrawRay(
                    randomPos, 
                    //Vector3.up
                    //currMesh.normals[i / 3] 
                    normal
                    * 10, 
                    Color.red, 
                    10
                    );*/

                
            }


        }

	}
	



}
