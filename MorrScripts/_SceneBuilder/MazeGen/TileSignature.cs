using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


public class TileSignature : MonoBehaviour
{
    [Tooltip("Postive values = signature points. Negative = node links")]
    public int idPointsToZAxis = 2;
    public int preinsertCutRange = 1;
    public int gridStep = 10;


/*    public bool calcVectors = false;
    public float stepDist = 50;

    public Transform[] signaturePoints = new Transform[0];*/

    
    public List<Vector3> signatureVector = new List<Vector3>(){new Vector3(0,0,0)};
    //public List<Vector3> signatureVector_additionalPoints = new List<Vector3>();

    public List<Vector3> signatureLinks = new List<Vector3>(); 

    [Header("Rotation")]
    public Vector3 rotCorrection = Vector3.zero;

    [Header("Correction")]
    public List<Vector3> signatureVector_setNotPreinserted = new List<Vector3>();
    public List<Vector3> pointsOfInterest = new List<Vector3>();





/*	// Use this for initialization
	void Start () {

	    if (calcVectors)
	    {
            signatureVector = new List<Vector3> ( );

            foreach ( Transform signaturePoint in signaturePoints )
            {
                signatureVector.Add ( transform.InverseTransformPoint ( signaturePoint.position ) / stepDist );
            }
	    }

	}*/


}



#region CustomEditorButton
/*#if UNITY_EDITOR

[CanEditMultipleObjects]
[CustomEditor ( typeof ( TileSignature ) )]
public class TileSignature_Editor : Editor
{

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ( );

        if (GUILayout.Button("GenerateColliders"))
        {
            foreach (Object t in targets)
            {
                TileSignature tileSignature = (TileSignature) t;

                foreach (MeshRenderer meshRenderer in tileSignature.GetComponentsInChildren<MeshRenderer>())
                {
                    if (meshRenderer.gameObject.GetComponent<MeshCollider>() == null)
                    {
                        meshRenderer.gameObject.AddComponent<MeshCollider>();
                    }
                }
            }
        }

    }



}
#endif*/

#endregion