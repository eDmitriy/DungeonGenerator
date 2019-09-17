using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[SelectionBase]
public class TileSignature : MonoBehaviour
{
    [Tooltip("Postive values = signature points. Negative = node links")]
    public int idPointsToZAxis = 2;
    public int preinsertCutRange = 1;
    public int gridStep = 10;
    public bool isVerticalConnector = false;
    public bool drawSignature = false;


    [Tooltip("First and last items must be near to signatureLinks")]
    public List<Vector3> signatureVector = new List<Vector3>(){new Vector3(0,0,0)};

    public List<Vector3> signatureLinks = new List<Vector3>(); 

    [Header("Rotation")]
    public Vector3 rotCorrection = Vector3.zero;

    [Header("Correction")]
    public List<Vector3> signatureVector_setNotPreinserted = new List<Vector3>();
    public List<Vector3> pointsOfInterest = new List<Vector3>();



    private void OnDrawGizmosSelected()
    {
      
        
        if(drawSignature==false) return;

        //Gizmos.color = Color.blue;
        Debug.DrawLine(
            SignatureToWorldPoint(signatureLinks[0]), 
            SignatureToWorldPoint(signatureVector[0])
            );

        for (var i = 1; i < signatureVector.Count; i++)
        {
            var v3_1 = signatureVector[i-1];
            v3_1 = SignatureToWorldPoint(v3_1) ;
            var v3_2 = signatureVector[i];
            v3_2 = SignatureToWorldPoint(v3_2);

            Debug.DrawLine(v3_1, v3_2);
        }
        
        Debug.DrawLine(
            SignatureToWorldPoint(signatureLinks.Last()), 
            SignatureToWorldPoint(signatureVector.Last())
        );
    }

    private Vector3 SignatureToWorldPoint(Vector3 v3)
    {
        return transform.TransformPoint(v3 * gridStep);
    }


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