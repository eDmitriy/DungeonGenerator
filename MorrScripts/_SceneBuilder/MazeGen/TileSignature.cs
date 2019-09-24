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
        if (signatureLinks.Count > 0)
        {
            Debug.DrawLine(
                SignatureToWorldPoint(signatureLinks[0]),
                SignatureToWorldPoint(signatureVector[0])
            );
        }

        for (var i = 1; i < signatureVector.Count; i++)
        {
            var v3_1 = signatureVector[i-1];
            v3_1 = SignatureToWorldPoint(v3_1) ;
            var v3_2 = signatureVector[i];
            v3_2 = SignatureToWorldPoint(v3_2);

            Debug.DrawLine(v3_1, v3_2);
        }

        
        if (signatureLinks.Count > 0)
        {
            Debug.DrawLine(
                SignatureToWorldPoint(signatureLinks.Last()), 
                SignatureToWorldPoint(signatureVector.Last())
            );
        }
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
        

        List<Transform> cubesList = GetComponentsInChildren<Transform>(false).ToList();
        signatureVector = new List<Vector3>();
        signatureLinks = new List<Vector3>();
        pointsOfInterest = new List<Vector3>();
        
        foreach (var cTr in cubesList)
        {
            if (cTr.name.Contains("Cube"))
            {
                AddVectorToList(cTr.position, signatureVector);
            }
            if (cTr.name.Contains("LINK"))
            {
                AddVectorToList(cTr.position, signatureLinks);
            }
            if (cTr.name.Contains("INTEREST"))
            {
                AddVectorToList(cTr.position, pointsOfInterest);
            }
        }
    }



    [ContextMenu("SignatureVectorsFromBounds")]
    public void SignatureVectorsFromBounds()
    {
        SignatureVectorsFromChildCubes();
        
        
        Bounds bounds = new Bounds();
        bounds = bounds.CompoundBounds(gameObject);
        Vector3Int gridSize = new Vector3Int( (int)(bounds.size.x/gridStep), Mathf.CeilToInt(bounds.size.y/gridStep), (int)(bounds.size.z/gridStep) );
        Vector3 startShift = bounds.center - gridSize * (gridStep / 2);
        startShift.y = transform.position.y;
        
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int z = 0; z < gridSize.z; z++)
                {
                    var currPos = startShift + new Vector3(x, y, z) * gridStep;
                    
                    AddVectorToList(currPos, signatureVector);

                }
            }
        }

        //int indexOf_1st_link = signatureVector.IndexOf(Vector3.zero/*signatureLinks[0]*/);
        signatureVector.Insert(0, Vector3.zero);
    }
    
    
    private void AddVectorToList(/*Transform cTr*/Vector3 cTrpos, List<Vector3> list)
    {
        AddVectorToList(cTrpos/*cTr*/, list, transform, gridStep);
    }

    static void AddVectorToList(Vector3 cTrpos, List<Vector3> list, Transform transform, float gridStep)
    {
        var inverseTransformPoint = transform.InverseTransformPoint(cTrpos);
        for (int i = 0; i < 3; i++)
        {
            inverseTransformPoint[i] = Mathf.CeilToInt(inverseTransformPoint[i] / gridStep);
        }

        list.Add(inverseTransformPoint);
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