using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AI;
using UnityEngine.SceneManagement;


#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor;
#endif



public class NavMeshLinks_AutoPlacer : MonoBehaviour
{
    #region Variables

    public Transform linkPrefab;

    //public Transform facingTarget;
    public float tileWidth = 5f;
    public int maxEdgesCount = 1000;


    [Header("OffMeshLinks")]
    public float maxJumpHeight = 3f;
    public LayerMask raycastLayerMask = -1;


    [Header ( "EdgeNormal" )]
    public bool invertFacingNormal = false;
    public bool dontAllignYAxis = false;

    List<Vector3> spawnedLinksPositionsList = new List< Vector3 >();
    Mesh currMesh;
    List<Edge> edges = new List<Edge> ( );

    private float agentRadius = 2;

    #endregion






    #region GridGen

    public void Generate ()
    {
        if(linkPrefab==null) return;
        agentRadius = NavMesh.GetSettingsByIndex( 0 ).agentRadius;


        edges.Clear ( );
        spawnedLinksPositionsList.Clear();

        CalcEdges ( );
        PlaceTiles ( );



#if UNITY_EDITOR
        //EditorUtility.SetDirty ( spawnedTransf );
        if(!Application.isPlaying)EditorSceneManager.MarkSceneDirty ( gameObject.scene );
#endif

    }



    private void PlaceTiles ()
    {
        if ( edges.Count == 0 ) return;


/*        List< NavMeshLink_TBS > navMeshLinkList = GetComponentsInChildren< NavMeshLink_TBS >().ToList();
        while( navMeshLinkList.Count>0 )
        {
            GameObject o = navMeshLinkList[ 0 ].gameObject;
            if( o != null ) DestroyImmediate( o );
            navMeshLinkList.RemoveAt( 0 );
        }



        foreach ( Edge edge in edges )
        {

            int tilesCountWidth = (int)Mathf.Clamp ( edge.length / tileWidth, 0, 10000 );
            float heightShift = 0;


            for ( int columnN = 0; columnN < tilesCountWidth; columnN++ ) //every edge length segment
            {
                Vector3 placePos = Vector3.Lerp (
                                    edge.start,
                                    edge.end,
                                    (float)columnN / (float)tilesCountWidth //position on edge
                                    + 0.5f / (float)tilesCountWidth //shift for half tile width
                                ) + edge.facingNormal * Vector3.up * heightShift;

                
                //spawn
                CheckPlacePos( placePos , edge.facingNormal);

                        
            }

        }*/
    }




    protected virtual void CheckPlacePos(Vector3 pos, Quaternion normal)
    {
        Vector3 startPos = pos + normal * Vector3.forward * agentRadius * 2;
        Vector3 endPos = startPos - Vector3.up * maxJumpHeight * 1.1f;

        //Debug.DrawLine ( pos + Vector3.right * 0.2f, endPos, Color.white, 2 );


        NavMeshHit navMeshHit;
        RaycastHit raycastHit = new RaycastHit();
        if ( Physics.Linecast ( startPos, endPos, out raycastHit, raycastLayerMask.value, QueryTriggerInteraction.Ignore) )
        {
            if(NavMesh.SamplePosition( raycastHit.point, out navMeshHit, 0.5f, NavMesh.AllAreas ))
            {
                //Debug.DrawLine( pos, navMeshHit.position, Color.black, 15 );

                if( Vector3.Distance( pos, navMeshHit.position ) > 1.1f )
                {
                    //SPAWN NAVMESH LINKS
                    Transform spawnedTransf = Instantiate (
                        linkPrefab.transform,
                        pos - normal * Vector3.forward * 0.02f,
                        normal
                    ) as Transform;

/*                    var nmLink = spawnedTransf.GetComponent<NavMeshLink_TBS> ( );
                    nmLink.startPoint = Vector3.zero;
                    nmLink.endPoint = nmLink.transform.InverseTransformPoint ( navMeshHit.position );
                    nmLink.UpdateLink();

                    nmLink.SetMatchTargetTransforms();

                    spawnedTransf.SetParent ( transform );*/


                }

            }
        }


    }



    #endregion





    #region EdgeGen


    float triggerAngle = 0.999f;

    public int meshTrianglesCounter = 0;
    
    private void CalcEdges ()
    {
        var tr = NavMesh.CalculateTriangulation ( );


        currMesh = new Mesh()
        {
            vertices = tr.vertices,
            triangles = tr.indices
        };


        for ( int i = 0; i < currMesh.triangles.Length - 1; i += 3 )
        {
            //CALC FROM MESH OPEN EDGES vertices
            meshTrianglesCounter = i;
            if(i>maxEdgesCount) break;


            TrisToEdge ( currMesh, i, i + 1 );
            TrisToEdge ( currMesh, i + 1, i + 2 );
            TrisToEdge ( currMesh, i + 2, i );
        }



        foreach ( Edge edge in edges )
        {
            //EDGE LENGTH
            edge.length = Vector3.Distance (
                edge.start/* - Vector3.up * edge.start.y*/,
                edge.end/* - Vector3.up * edge.end.y*/
                );

            //AVERAGE WIDTH of UP and DOWN EDGES if upper edge setted
/*            if ( edge.startUp.sqrMagnitude > 0 )
            {
                edge.length = Mathf.SmoothStep (
                    Vector3.Distance ( edge.start, edge.end ),
                    Vector3.Distance ( edge.startUp, edge.endUp ),
                    0.5f );
                edge.endOrig = edge.end;
                edge.end = edge.start + ( edge.end - edge.start ).normalized * edge.length;
            }*/




            //FACING NORMAL 
            if ( !edge.facingNormalCalculated )
            {
                edge.facingNormal = Quaternion.LookRotation ( Vector3.Cross ( edge.end - edge.start, Vector3.up ) );


                if ( edge.startUp.sqrMagnitude > 0 )
                {
                    var vect = Vector3.Lerp ( edge.endUp, edge.startUp, 0.5f ) - Vector3.Lerp ( edge.end, edge.start, 0.5f );
                    edge.facingNormal = Quaternion.LookRotation ( Vector3.Cross ( edge.end - edge.start, vect ) );


                    //FIX FOR NORMALs POINTING DIRECT TO UP/DOWN
                    if ( Mathf.Abs ( Vector3.Dot ( Vector3.up, ( edge.facingNormal * Vector3.forward ).normalized ) ) > triggerAngle )
                    {
                        edge.startUp += new Vector3 ( 0, 0.1f, 0 );
                        vect = Vector3.Lerp ( edge.endUp, edge.startUp, 0.5f ) - Vector3.Lerp ( edge.end, edge.start, 0.5f );
                        edge.facingNormal = Quaternion.LookRotation ( Vector3.Cross ( edge.end - edge.start, vect ) );
                    }


                }

                if ( dontAllignYAxis )
                {
                    edge.facingNormal = Quaternion.LookRotation (
                        edge.facingNormal * Vector3.forward,
                        Quaternion.LookRotation ( edge.end - edge.start ) * Vector3.up
                    );
                }

                //edge.facingNormal = Quaternion.LookRotation(Vector3.up);


                edge.facingNormalCalculated = true;
            }
            if ( invertFacingNormal ) edge.facingNormal = Quaternion.Euler ( Vector3.up * 180 ) * edge.facingNormal;




        }



/*        float longest = float.MinValue;
        float angle;
        for ( int i = 0; i < edges.Count; i++ )
        {
            Vector3 facingTargetPos = facingTarget != null ? facingTarget.position : transform.position + transform.up * -10f;

            /*            Debug.DrawRay(edges[i].start, edges[i].facingNormal*Vector3.forward, Color.red, 10f);
                        Debug.DrawRay(edges[i].start, facingTargetPos - Vector3.Lerp(edges[i].start, edges[i].end, 0.5f), Color.blue, 10f);#1#


            angle = Vector3.Angle (
                edges[i].facingNormal * Vector3.forward,
                facingTargetPos - Vector3.Lerp ( edges[i].start, edges[i].end, 0.5f )
                );
            edges[i].facingType = angle < 89f ? CityGenTyleFacingType.frontSide : CityGenTyleFacingType.backSide;

/*            if ( (int)( edges[i].length / tileWidth ) < tileCountToSwitchToFronSide )
            {
                edges[i].facingType = CityGenTyleFacingType.sideWall;
            }#1#
        }*/
    }



    private void TrisToEdge ( Mesh currMesh, int n1, int n2 )
    {
        Vector3 val1 = /*transform.TransformPoint */( currMesh.vertices[currMesh.triangles[n1]] );
        Vector3 val2 = /*transform.TransformPoint*/ ( currMesh.vertices[currMesh.triangles[n2]] );

        Edge newEdge = new Edge ( val1, val2 );

        //remove duplicate edges
        for (var i = 0; i < edges.Count; i++)
        {
            Edge edge = edges[i];
            if ((edge.start == val1 & edge.end == val2)
                || (edge.start == val2 & edge.end == val1)
            )
            {
                //print("Edges duplicate " + newEdge.start + " " + newEdge.end);
                edges.Remove(edge);
                return;
            }
        }


        edges.Add ( newEdge );
    }



    #endregion

}








#if UNITY_EDITOR

[CustomEditor ( typeof ( NavMeshLinks_AutoPlacer ) )]
[CanEditMultipleObjects]
public class NavMeshLinks_AutoPlacer_Editor : Editor
{

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ( );

        //CityGen_EdgesToTiles[] generators = (CityGen_EdgesToTiles[]) targets;

        if ( GUILayout.Button ( "Generate" ) )
        {
            foreach ( var targ in targets )
            {
                ( (NavMeshLinks_AutoPlacer)targ ).Generate ( );
            }
        }


    }
}

#endif