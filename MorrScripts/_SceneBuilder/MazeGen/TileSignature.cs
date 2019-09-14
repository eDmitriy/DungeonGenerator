using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public bool isVerticalConnector = false;


/*    public bool calcVectors = false;
    public float stepDist = 50;

    public Transform[] signaturePoints = new Transform[0];*/

    [Tooltip("First and last items must be near to signatureLinks")]
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

    [ContextMenu("Signatures From Child Cubes")]
    public void SignatureVectorsFromChildCubes()
    {
        MazeGenerator mazeGenerator = FindObjectOfType<MazeGenerator>();
        if (mazeGenerator != null) gridStep = mazeGenerator.stepDist;
        

        List<Transform> cubesList = GetComponentsInChildren<Transform>()./*Where(v=>v.name.Contains("Cube")).*/ToList();
        signatureVector = new List<Vector3>();
        signatureLinks = new List<Vector3>();
        pointsOfInterest = new List<Vector3>();
        
        foreach (var cTr in cubesList)
        {
            if (cTr.name.Contains("Cube"))
            {
                AddVectorToList(cTr, signatureVector);
            }
            if (cTr.name.Contains("LINK"))
            {
                AddVectorToList(cTr, signatureLinks);
            }
            if (cTr.name.Contains("INTEREST"))
            {
                AddVectorToList(cTr, pointsOfInterest);
            }
        }
    }

    
    
    private void AddVectorToList(Transform cTr, List<Vector3> list)
    {
        AddVectorToList(cTr, list, transform, gridStep);
    }

    static void AddVectorToList(Transform cTr, List<Vector3> list, Transform transform, float gridStep)
    {
        var inverseTransformPoint = transform.InverseTransformPoint(cTr.position);
        for (int i = 0; i < 3; i++)
        {
            inverseTransformPoint[i] = Mathf.RoundToInt(inverseTransformPoint[i] / gridStep);
        }

        /*signatureVector*/list.Add(inverseTransformPoint);
    }
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