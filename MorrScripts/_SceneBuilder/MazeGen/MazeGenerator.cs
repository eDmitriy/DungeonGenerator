﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;


#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class MazeGenerator : MonoBehaviour, IDragHandler, IBeginDragHandler
{

    #region Vars

    public bool genOnStart = false;


    [Header("grid")] 
    public bool drawPathLinks = true;

    public Vector3 gridSize = new Vector3(50,50,50);
    public int stepDist = 20;

    public int pathNoiseCount = 10;

    Node startNode, endNode,  pathStartNode, pathEndNode;
    public Transform startPoint;

    [HideInInspector] public List<Node> pointsOfInterest = new List<Node> ( );


    [Header("Signatures")]
    public string pathToSinatures = "MazeGeneratorSignatures";
    [HideInInspector] public List<TileSignature> signatures = new List<TileSignature> ( );
    

    [HideInInspector]
    public List<Node> nodeList = new List<Node> ( );
    [HideInInspector]
    public List<PathNode> pathToTarget = new List<PathNode> ( );


    //draw grid brush
    [Header("Grid Brush")] 
    public bool drawingGridBrushNow = true;
    public int currHeightLayer = 0;
    private BoxCollider planeCollider;

    public Vector3 currGridBrushPointerPos = Vector3.zero;
    public Vector3 currGridBrushPointer_DragStartPos = Vector3.zero;
    public Node drawGridBrash_StartNode;
    public Node drawGridBrash_CurrNode;

    public LayerMask gridBrushLayerMask = -1;
    #endregion



    #region Mono

    // Use this for initialization
    private void Start()
    {
        if (planeCollider == null) planeCollider = GetComponent<BoxCollider>();

        if (genOnStart)
        {
            BuildPathGrid ( );
            ScanSigantures ( );
        }
    }


    private string pathToSinaturesLast = "";
    public void BuildPathGrid()
    {
        if (signatures.Count == 0 || pathToSinaturesLast != pathToSinatures)
        {
            signatures = Resources.LoadAll<TileSignature> ( pathToSinatures ).ToList ( );
            pathToSinaturesLast = pathToSinatures;
        }

        CleanSpawned(true);




        GenerateGrid ( );

        if ( startPoint != null )
        {
            Vector3 startPointClampedPos = ClampPositionToGrid ( startPoint.position );

            startNode = nodeList.FirstOrDefault ( v => Vector3.Distance ( v.worldPos/*.transform.position*/, startPointClampedPos ) < stepDist );
            if ( startNode != null ) pathStartNode = startNode;
        }

        SetStartEndPoints ( transform.position );
        pathToTarget.AddRange ( PathFindLoop ( ) );

        pointsOfInterest.Add ( pathToTarget.Last ( ).node );



        PathNoise ( );


        UpdateColorIndication ( );
    }

    public void ScanSigantures()
    {
        //ScanPathForSignature();
        //StartCoroutine ( ScanPathForSignature ( ) );
        ScanPathForSignature();
    }


    public void Update()
    {
        CalcCurrPointerPosition();

        DrawPathLinks();
    }

    public void DrawPathLinks()
    {
        if ( !drawPathLinks && pathToTarget != null && pathToTarget .Count>1) return;

        //EVERY NODE
/*        foreach (MapNode mapNode in nodeList)
        {
            Debug.DrawRay (
                mapNode.transform.position,
                Vector3.up * 25,
                Color.yellow
            );
        }*/


        foreach ( var pathNode in pathToTarget/*.Select(v=>v.node)*/ )
        {
/*            Debug.DrawRay(
                pathNode.node.worldPos ,
                    Quaternion.Euler(0, Random.Range(0,350), 0) * Vector3.one*50
            );*/
            if (pathNode == null || pathNode.node == null) continue;

            //NOT OCCUPIED TILES
/*            if ( !pathNode.node.occupiedByTile )
            {
                Debug.DrawRay (
                    pathNode.node.worldPos,
                    Vector3.up * 200,
                    Color.red
                );
            }*/
/*            Debug.DrawRay (
                pathNode.node.worldPos,
                Vector3.up*10, 
                Color.blue
            );*/
            foreach ( var link in pathNode.links )
            {
                if(link==null || link.node==null) continue;

                Debug.DrawLine (
                    pathNode.node.worldPos /*+ Vector3.one * Random.Range ( -10, 10 )*/,
                    link.node.worldPos /*+ Vector3.one * Random.Range ( -10, 10 )*/,
                    new Color(0,0,1,0.5f), 0.1f, false
                );
            }
        }
    }


    public void CleanSpawned(bool cleanLists)
    {
        //CLEAR PREV NODES
        foreach ( Transform child in transform.GetComponentsInChildren<Transform> ( ) )
        {
            if ( child != null && child != transform && child != startPoint ) DestroyImmediate ( child.gameObject );
        }




        if (cleanLists)
        {
            //Destroy unUsed nodes
/*            if ( nodeList != null && nodeList.Count > 0 )
            {
                for ( int i = 0; i < nodeList.Count; i++ )
                {
                    if ( nodeList[i] != null )
                    {
                        GameObject o = nodeList[i].gameObject;
                        if ( o != null ) Destroy/*Immediate#1# ( o );
                    }

                }
            }*/


            if (nodeList != null) nodeList.Clear();
            if (pathToTarget != null) pathToTarget.Clear();
            if (pointsOfInterest != null) pointsOfInterest.Clear();
        }
    }

    #endregion



    #region PathGen

    private void UpdateColorIndication ()
    {
        pathEndNode = pathToTarget.Last ( ).node;

        //Destroy unUsed
/*        List<MapNode> nodesToDelete = new List<MapNode> ( nodeList.Except ( pathToTarget.Select ( v => v.node ) ).Except ( pointsOfInterest ) );
        for (int i = 0; i < nodesToDelete.Count; i++)
        {
            DestroyImmediate ( nodesToDelete[i].gameObject );
        }
        nodeList = nodeList.Except(nodesToDelete).ToList();*/
/*        foreach ( MapNode mapnode in nodeList.Except ( pathToTarget.Select ( v => v.node ) ).Except ( pointsOfInterest ) )
        {
            Destroy ( mapnode.gameObject, 0.01f );
        }*/

        
        foreach ( var interestPoint in pointsOfInterest )
        {
            CreatePrimitiveAtMapNode ( interestPoint, Color.yellow, 0.1f );
        }
        

        if ( pathStartNode != null )
        {
            CreatePrimitiveAtMapNode ( pathStartNode, Color.white, 1.5f, PrimitiveType.Sphere );
        }

        if ( pathEndNode != null )
        {
            CreatePrimitiveAtMapNode ( pathEndNode, Color.red, 1.5f, PrimitiveType.Sphere );
        }
    }

    private void CreatePrimitiveAtMapNode ( Node node, Color color, float sizeMult = 1f,
        PrimitiveType type = PrimitiveType.Cube )
    {
        GameObject go = GameObject.CreatePrimitive ( type );
        if ( node != null /*&& node.transform !=null*/) go.transform.parent = transform;
        go.transform.position = node.worldPos;
        go.transform.localScale = Vector3.one * stepDist * sizeMult;
        //go.GetComponent<Renderer> ( ).material.SetColor ( "_Color", color );
    }



    private void GenerateGrid ()
    {
        //float halfGridSizeWorld = ( gridSize.x / 2 );
        int gridStepsCountX = (int)Mathf.Ceil ( gridSize.x / stepDist );
        int gridStepsCountY = (int)Mathf.Ceil ( gridSize.y / stepDist );
        int gridStepsCountZ = (int)Mathf.Ceil ( gridSize.z / stepDist );

        //Vector3 startPos = transform.position + Vector3.one * ( -halfGridSizeWorld + stepDist / 2f );
        Vector3 startPos = transform.position + ( -gridSize/2 + Vector3.one *stepDist / 2f );
        Vector3 spawnShift = Vector3.zero;


        nodeList = new List<Node> ( );
        pathToTarget = new List<PathNode> ( );
        pointsOfInterest = new List<Node> ( );



/*        GameObject go = new GameObject ( "MapNodes "/* + x + " " + y + " " + z#1# );
        go.transform.parent = transform;*/


        for ( int x = 0; x < gridStepsCountX; x++ )
        {
            for ( int z = 0; z < gridStepsCountZ; z++ )
            {
                for ( int y = 0; y < gridStepsCountY; y++ )
                {
                    spawnShift = new Vector3 ( x * stepDist, y * stepDist, z * stepDist ); //SHIFT POS

/*                    GameObject go = new GameObject ( "MapNode "+x+" "+y+" "+z );
                    go.transform.parent = transform;
                    go.transform.position = startPos + spawnShift;*/


                    Node node = new Node();// go.AddComponent<Node> ( );
                    node.worldPos = startPos + spawnShift;
                    node.vectorPos = new Vector3 ( x, y, z );
                    nodeList.Add ( node );

                    foreach ( var nodeConnection in GetAllNodeConnections ( node ) )
                    {
                        nodeConnection.CreateLink ( node );
                    }
                }
            }
        }
    }




    private void SetStartEndPoints ( Vector3 startPos )
    {
        if ( startNode != null && endNode != null ) return;


        Vector3 pointOfInterest = startPos;

        Node[] nodes =
            nodeList.Where ( v => Vector3.Distance ( v.worldPos/*.transform.position*/, pointOfInterest ) > gridSize.x / 2 /*Mathf.PI*/)
                .ToArray ( );
        if ( nodes.Length > 0 )
        {
            if ( endNode == null ) endNode = nodes[Random.Range ( 0, nodes.Length )];

            pointOfInterest = endNode.worldPos/*.transform.position*/;
            nodes =
                nodeList.Where ( v => Vector3.Distance ( v.worldPos/*.transform.position*/, pointOfInterest ) > gridSize.x / 2 /*Mathf.PI*/)
                    .ToArray ( );

            if ( nodes.Length > 0 )
            {
                if ( startNode == null )
                {
                    startNode = nodes[Random.Range ( 0, nodes.Length )];
                }
            }
        }

    }

    #endregion

    #region Build Various Pathes

    private void PathNoise ()
    {
        for ( int i = 0; i < pathNoiseCount; i++ )
        {
            startNode = pathToTarget.Last ( ).node;
            endNode = null;

            //build path from pathEnd to randomPath point
            if ( i > 0 && i % 3 == 0 )
            {
                var openNodesPair = GetPathOpenEnd();
                startNode = openNodesPair[0];
                endNode = openNodesPair[1];

            }

            
            //build straight horizontal path from random path point
            if ( i > 0 && i % 2 == 0 )
            {
                BuildLongStarightPathesOnCross ( );
                continue;
            }
            

            //Random point around path end point if end/start node not setted
            if ( startNode == null ) startNode = pathToTarget.Last ( ).node;
            SetStartEndPoints ( startNode.worldPos/*.transform.position*/ );


            //Build Path
            pathToTarget.AddRange ( PathFindLoop ( ).Except(pathToTarget) );
            pointsOfInterest.Add ( pathToTarget.Last ( ).node );

        }



        //Close OPEN ENDS
        int closedEndsCounter = 0;
        for (int i = 0; i < 200; i++)
        {
            Node[] openNodesPair = GetPathOpenEnd ( );
            if(openNodesPair[0]==null){
                //print(i);
                break; //Break if no open ends on Path
            }

            startNode = openNodesPair[0];
            endNode = openNodesPair[1];


            if (startNode != null && endNode != null)
            {
/*                for (int j = 0; j < 20; j++)
                {
                    Debug.DrawLine ( startNode.transform.position + Vector3.one *j, endNode.transform.position + Vector3.one * j, Color.blue, 20, false );
                }*/
                
                //Build Path
                closedEndsCounter++;
                pathToTarget.AddRange ( PathFindLoop ( ).Except ( pathToTarget ) );
            }

        }
        print("Closed Dead End count = "+closedEndsCounter);
    }


    private void BuildLongStarightPathesOnCross()
    {
        //MapNode dirNode = GetEmptyNodeConnection(startNode);//startNode.links.FirstOrDefault ( v => !pathToTarget.Select ( x => x.node ).Contains ( v ) );
        if ( startNode == null ) startNode = pathToTarget.Last ( ).node;
        startNode = GetRandomPointOnPathFarFromStartNode();

        //Random direction
        Vector3 dir = Vector3.zero;
        Vector3 rotEuler=Vector3.zero;
        Quaternion rot = new Quaternion ( );

        dir[Random.Range(0, 3)] = Random.Range(0, 100) > 50 ? 1 : -1;

        Node retNode = null;
        int maxSteps = (int)gridSize.x / stepDist;


        for (int w = 0; w < 4; w++)
        {
            //int dirModifier = (w == 0 ? 1 : -1);
            rotEuler = Vector3.zero;
            rotEuler[Random.Range ( 0, 3 )] = Random.Range ( 0, 4 ) * 90;
            rot = Quaternion.Euler(rotEuler);

            for ( int j = 0; j < maxSteps; j++ )
            {
                retNode = GetNodeByArrayId ( startNode.vectorPos + rot* dir * ( maxSteps - j )  );
                if ( retNode != null )
                {
                    endNode = retNode;

                    //Build Path
                    pathToTarget.AddRange ( PathFindLoop ( ).Except ( pathToTarget ) );
                    pointsOfInterest.Add ( pathToTarget.Last ( ).node );

                    break;
                }
            }

        }
        
    }

    private Node GetRandomPointOnPathFarFromStartNode ()
    {
        if (startNode == null) return null;


        List<PathNode> pathPointsFar = pathToTarget.Where (
                v => Vector3.Distance ( v.node.worldPos, startNode.worldPos/*.transform.position*/ ) > gridSize.x / 3 ).ToList ( );
        return pathPointsFar[Random.Range ( 0, pathPointsFar.Count )].node;
    }

    private Node[] GetPathOpenEnd ()
    {
        Node[] retNodes = new Node[2];

        List<PathNode> openPoints = pathToTarget.Where(v => v.links.Count == 1 && v.node!=null ).ToList();

        if (openPoints.Count < 1) return retNodes;//no open points


        retNodes[0] = openPoints[Random.Range ( 0, openPoints.Count )].node;


        openPoints = openPoints.Where(v=>v.node!=retNodes[0]).OrderByDescending ( v => GetPathAngle ( v, retNodes[0] ) ).ToList ( );
        retNodes[1] = openPoints.Select ( v => v.node ).FirstOrDefault ( );

        return retNodes;
    }

    float GetPathAngle ( PathNode node, Node currNode )
    {
        Vector3 pathDir = node.node.worldPos - currNode.worldPos/*.transform.position*/;
        Vector3 dirToCenter = transform.position - currNode.worldPos/*.transform.position*/;

        return Vector3.Angle(pathDir, dirToCenter);
    }

    #endregion






    #region SignatureCheck

    private void ScanPathForSignature()
    {
        if(pathToTarget.Count<2) return;


        //reset occupation flag for every node
        foreach (var node in nodeList)
        {
            node.occupiedByTile = false;
        }

        //clean signatures
        foreach ( var child in transform.GetComponentsInChildren<TileSignature> ( ) )
        {
            if ( child != null ) DestroyImmediate ( child.gameObject );
        }





        List<PathNode> nodeChain = new List<PathNode> ( );


        //yield return new WaitForSeconds ( 0.2f );

        signatures = signatures.OrderByDescending(v => v.signatureVector.Count).ToList();

        //FOR EACH SIGNATURE ITERATE WHOLE PATH
        foreach (TileSignature signature in signatures)
        {
            int signatureLength = signature.signatureVector.Count;


            for ( int i = 0; i + (signatureLength-1) < pathToTarget.Count; i++ )
            {
                //build node chain
                nodeChain = new List<PathNode> ( );
                for ( int j = 0; j < signatureLength; j++ )
                {                    
                    // if already OCCUPIED break chain and move iterator next
                    var currPathNode = pathToTarget[i+j].node;
                    if (currPathNode.occupiedByTile)
                    {
                        nodeChain.Clear ( );
                        break;
                    }


                    nodeChain.Add ( pathToTarget[i + j] );
                }



                if ( nodeChain.Count > 0 )
                {
                    //Scan forward
                    if (! ScanSignature(nodeChain, signature))
                    {
                        //Reverse
                        nodeChain.Reverse ( );
                        ScanSignature ( nodeChain, signature ); 
                    }
                }
                //yield return null;
            }
        }


        print("Not occupied nodes count = "+pathToTarget.Count(v=>!v.node.occupiedByTile) );

        //yield return null;
    }

    private bool ScanSignature ( List<PathNode> nodes, TileSignature signature)
    {
        //add every link of nodeChain to nodes list
        List<PathNode> linksList = new List<PathNode>();
        linksList = nodes.SelectMany ( v => v.links ).Except ( nodes ).ToList ( );


        //Calc signature for nodes chain
        List<Vector3> signaturePoints = new List<Vector3> ( nodes.Count );

        for ( int i = 0; i < nodes.Count; i++ )
        {
            signaturePoints.Add( nodes[i].node.vectorPos - nodes[0].node.vectorPos);
        }


        List<Vector3> pointsLinksVectors = new List<Vector3> ( linksList.Count );
        for ( int i = 0; i < linksList.Count; i++ )
        {
            pointsLinksVectors.Add ( linksList[i].node.vectorPos - nodes[0].node.vectorPos );
        }




        //Rotate sinature vectors to match 4 diff rotations in Y Axis
        Quaternion rot = Quaternion.Euler ( 0, 90, 0 );
        for (int q = 0; q < 4; q++)
        {
            rot = Quaternion.Euler ( 0, q*90, 0 );

            //ROTATE VECTORS
            for (int i = 0; i < signaturePoints.Count; i++)
            {
                signaturePoints[i] = rot*signaturePoints[i];
            }
            for ( int i = 0; i < pointsLinksVectors.Count; i++ )
            {
                pointsLinksVectors[i] = rot * pointsLinksVectors[i];
            }

            //CHECK
            if ( IsMatchSignature ( signaturePoints.ToList ( ), pointsLinksVectors, signature ) )
            {
                //Debug.Log("Is match signature "+nodes[0].node.vectorPos+" quat = "+rot);

                //Tile orientation
                Vector3 rotDir = Vector3.forward;
                if (signature.idPointsToZAxis > -1)
                {
                    rotDir = nodes[signature.idPointsToZAxis].node.worldPos - nodes[0].node.worldPos;
                    rotDir.y = 0;
                }
                else
                {
                    Vector3 signLink = signature.signatureLinks[Mathf.Abs(signature.idPointsToZAxis) - 1];
                    var nodeIndex = pointsLinksVectors.FindIndex ( v => Vector3.SqrMagnitude(v - signLink) < 0.1f);

                    rotDir = linksList[nodeIndex].node.worldPos - nodes[0].node.worldPos;
                    rotDir.y = 0;
                }



                //SPAWN
#if UNITY_EDITOR
                var go2 = PrefabUtility.InstantiatePrefab ( signature.transform,
                    transform.gameObject.scene
                ) as Transform;

                if (go2 != null)
                {
                    go2.position = nodes[0].node.worldPos;
                    go2.rotation = Quaternion.LookRotation(rotDir) * Quaternion.Euler(signature.rotCorrection);
                    go2.parent = transform;
                }

#else
                var go = Instantiate ( 
                    signature.transform, 
                    nodes[0].node.worldPos, 
                    Quaternion.LookRotation ( rotDir ) * Quaternion.Euler ( signature.rotCorrection ) 
                    );

                go.transform.parent = transform;
#endif



                //mark nodes as OCCUPIED
                foreach (PathNode node in nodes)
                {
                    node.node.occupiedByTile = true;
                }

                return true;
            }

        }

        return false;
    }

    bool IsMatchSignature(List<Vector3> points, List<Vector3> pointsLinks, TileSignature signature)
    {
        int counter = 0;
        int linksMatchCounter = 0;

        //SIGNATURE CHECK
        for ( int i = 0; i < signature.signatureVector.Count; i++ )
        {
            if (Vector3.SqrMagnitude(signature.signatureVector[i] - points[i]) < 0.1f) counter++;
        }

        if (counter != signature.signatureVector.Count) return false;



        //LINKS CHECK
        foreach (Vector3 link in signature.signatureLinks)
        {
            foreach (Vector3 pointsLink in pointsLinks)
            {
                if ( Vector3.SqrMagnitude ( link - pointsLink ) < 0.1f )
                {
                    linksMatchCounter++;
                }
            }
        }
        //Check if path links count match sinature
        if ( Mathf.Abs ( pointsLinks.Count - signature.signatureLinks.Count )>0 )
        {
            linksMatchCounter = 0;
        }



        return counter == signature.signatureVector.Count  
                    && linksMatchCounter == signature.signatureLinks.Count ;
    }

    #endregion



    #region GetNodes

    private List<Node> GetAllNodeConnections ( Node node )
    {
        List<Node> nodes = new List<Node> ( );

        Vector3 dir = Vector3.zero;
        Node retNode = null;


        for (int i = 0; i < 6; i++)
        {
            dir = Vector3.zero;

            dir[i/2] = i%2 == 0 ? 1 : -1;
            

            retNode = GetNodeByArrayId (node.vectorPos + dir );
            if ( retNode != null ) nodes.Add ( retNode );
        }

        return nodes;
    }

    public Node GetNodeByArrayId ( Vector3 vector )
    {
        for (int i = 0; i < nodeList.Count; i++)
        {
            Node v = nodeList[i];
            if ( (v.vectorPos - vector).sqrMagnitude<0.1 ) return v;
        }
        return null;
    }

    public Node GetNodeByWorldPos ( Vector3 pos )
    {
        return GetNodeByArrayId (
            ( transform.InverseTransformPoint ( pos ) + ( gridSize / 2 - new Vector3 ( stepDist / 2f, stepDist / 2f, stepDist / 2f ) ) )
            / stepDist
        );
    }


    bool IsNodeHaveManyLinksOnPath ( Node node )
    {
        return node.links.Any(link => pathToTarget.Count(v => v.node == link) > 1);
    }

    private Vector3 ClampPositionToGrid(Vector3 pos)
    {
        for (int i = 0; i < 3; i++)
        {
            pos[i] = (Mathf.Round(pos[i]/stepDist))*stepDist;
        }
        return pos;
    }

    Node GetEmptyNodeConnection ( Node node )
    {
        var pathMapNodes = pathToTarget.Select(x => x.node);

        return node.links.FirstOrDefault( v => !pathMapNodes.Contains(v) );
    }

    #endregion
    

    #region Pathfind



    private List<PathNode> openNodes = new List<PathNode> ( );
    private List<PathNode> closedNodes = new List<PathNode> ( );




    private List<PathNode> PathFindLoop ( )
    {
        //Init start values
        StartPathfind ( startNode, endNode );

        //Iterate
        while ( closestNode != null && closestNode.dist > /*stepDist*/0 )
        {
            FindNextNodeToReachTarget ( endNode );
            //yield return new WaitForSeconds ( 1 );
        }



        #region BuilLinks

        List<PathNode> pathToTargetTemp = new List<PathNode>(closedNodes);
        for (int i = 0; i < pathToTargetTemp.Count; i++)
        {
            PathNode nextNode = null;
            PathNode prevNode = null;
            PathNode currNode = pathToTargetTemp[i];

            if (pathToTargetTemp.Count > i + 1) nextNode = pathToTargetTemp[i + 1];
            if (i - 1 >= 0) prevNode = pathToTargetTemp[i - 1];


            if (nextNode != null) currNode.AddLink(nextNode);
            if (prevNode != null) currNode.AddLink(prevNode);
        }

        //Link to start
        var firstPathNode = pathToTarget.FirstOrDefault(v => v.node == startNode);
        if (firstPathNode != null) pathToTargetTemp.First().AddLink(firstPathNode);

        //Link to last path Node
        var endPathNode = pathToTarget.FirstOrDefault(v => v.node == endNode);
        if (endPathNode != null) pathToTargetTemp.Last().AddLink(endPathNode);

        #endregion




        return pathToTargetTemp;

        //yield return null;
    }


    private bool pathSearching;
    public void StartPathfind ( Node start, Node end )
    {
/*        if ( !pathSearching )
        {*/
            pathSearching = true;

            closedNodes = new List<PathNode> ( );
            openNodes = new List<PathNode> ( );

/*            PathNode startPath = new PathNode ( );
            startPath.node = start;
            startPath.CalcDist ( end, start );*/
            if (start.pathNode == null)
            {
                start.pathNode = new PathNode(){node = start};
            }
            start.pathNode.CalcDist ( end, start );

            closedNodes.Add ( start.pathNode );


            closestNode = start.pathNode;
            closestNode.CalcDist ( endNode, closestNode.node );
        //}

    }

    private PathNode closestNode = null;
    private void FindNextNodeToReachTarget ( Node end )
    {
        //Calc open nodes around closed
        foreach ( PathNode node in closedNodes/*.Except(noisedNodes).Except(pathToTarget)*/ )
        {
            var links = CalcNewPathNodes ( node.node.links.ToArray ( ), end, node.node );

            foreach ( PathNode pnLink in links )
            {
                var link = pnLink;
                if ( openNodes.Count ( v => v.node == link.node ) == 0 && closedNodes.Count ( v => v.node == link.node ) == 0 )
                {
                    openNodes.Add ( pnLink );
                }
            }
        }



        //select best next node to tick
        closestNode = openNodes.OrderBy ( v => v.cost ).FirstOrDefault ( );


        if (closestNode != null && closedNodes.Count ( v => v.node == closestNode.node ) == 0 )
        {
            closedNodes.Add ( closestNode );
        }


        var toRempvefromOpenList = openNodes.FirstOrDefault ( v => v.node == closestNode.node );
        if ( toRempvefromOpenList != null ) openNodes.Remove ( toRempvefromOpenList );




/*        foreach ( PathNode openNode in openNodes )
        {
            Debug.DrawRay ( openNode.node.worldPos + Vector3.right * 0.1f, Vector3.up * 3, Color.yellow, 2 );

        }
        foreach ( PathNode closedNode in closedNodes )
        {
            Debug.DrawRay ( closedNode.node.worldPos, Vector3.up * 6, Color.red, 3 );

        }*/
    }

    private PathNode[] CalcNewPathNodes ( Node[] nodes, Node target, Node prev )
    {
        List<PathNode> pathNodes = new List<PathNode> ( );

        foreach ( Node node in nodes )
        {
            if (node.pathNode == null)
            {
                node.pathNode = new PathNode(){node = node};
            }
            node.pathNode.CalcDist ( target, prev );

            pathNodes.Add ( node.pathNode );
        }

        return pathNodes.ToArray ( );
    }


    #endregion



    #region DrawGrid

    private int currHeightLayerPrev = 0;
    public void CalcCurrPointerPosition()
    {
        GridBrushInput();


        if ( !drawingGridBrushNow ) return;


        //Adjust collider
        if ( planeCollider == null ) planeCollider = GetComponent<BoxCollider> ( );


        if (currHeightLayer != currHeightLayerPrev)
        {
            currHeightLayerPrev = currHeightLayer;
            planeCollider.center = new Vector3 ( 0, currHeightLayer * stepDist - stepDist / 2, 0 );

            planeCollider.size = new Vector3 ( gridSize.x, 0.01f, gridSize.z );
        }



        //RAYCAST
        //var e = Event.current;
        RaycastHit hit = new RaycastHit();
        if ( Camera.main !=null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);//HandleUtility.GUIPointToWorldRay ( e.mousePosition );

            if ( Physics.Raycast ( ray, out hit, Mathf.Infinity, gridBrushLayerMask ) )
            {
                currGridBrushPointerPos = new Vector3 ( 
                    stepDist * (int)(hit.point.x / stepDist), 
                    stepDist * (int)(hit.point.y / stepDist), 
                    stepDist * (int)(hit.point.z / stepDist)
                    ) 
                    - Vector3.up*stepDist/2
                    //-Vector3.forward*stepDist
                    -Vector3.right*stepDist;
                //currGridBrushPointerPos = hit.point;

            } 
        }




        Color c = drawingGridBrushNow ? Color.red : Color.white;
        Debug.DrawLine ( currGridBrushPointerPos, currGridBrushPointerPos + Vector3.up * stepDist *4, c );

        Debug.DrawLine ( currGridBrushPointerPos, hit.point );


        if (drawingGridBrushNow && currGridBrushPointer_DragStartPos.sqrMagnitude > 0 )
        {
            Debug.DrawLine ( currGridBrushPointer_DragStartPos, currGridBrushPointerPos, c );

        }


    }

    void GridBrushInput()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            drawingGridBrushNow = !drawingGridBrushNow;
        }


        if ( Input.GetKey ( KeyCode.C ) && Input.GetKey ( KeyCode.L ) )
        {
            CleanSpawned(false);
        }
        if ( Input.GetKey ( KeyCode.G ) && Input.GetKey ( KeyCode.R ) )
        {
            BuildPathGrid();
        }
        if ( Input.GetKey ( KeyCode.S ) && Input.GetKey ( KeyCode.G ) )
        {
            ScanSigantures();
        }
    }

    public void DeleteBlock (  )
    {
        Node currNodeToDel = GetNodeByWorldPos ( currGridBrushPointerPos );
        if ( currNodeToDel != null )
        {
            if (currNodeToDel.pathNode != null) currNodeToDel.pathNode.RemoveAllLinks();

            var pathNode = pathToTarget.FirstOrDefault ( v => v.node == currNodeToDel );


            if ( pathNode != null && pathToTarget.Contains ( pathNode ) )
            {
                pathToTarget.Remove ( pathNode );
            }

        }
    }

    #region DragInput

    public void OnBeginDrag ( PointerEventData eventData )
    {
        if ( !drawingGridBrushNow ) return;


        if (eventData.button == PointerEventData.InputButton.Left)
        {
            //Debug.Log ( "Left click" );

            currGridBrushPointer_DragStartPos = currGridBrushPointerPos;
            //drawGridBrash_StartNode = GetNodeByWorldPos ( currGridBrushPointerPos );
        }


/*        else if ( eventData.button == PointerEventData.InputButton.Middle )
            Debug.Log ( "Middle click" );*/


/*        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            //Debug.Log ( "Right click" );
            DeleteBlock();
        }*/



    }
    public void OnDrag ( PointerEventData eventData )
    {
        if ( !drawingGridBrushNow ) return;


        if ( eventData.button == PointerEventData.InputButton.Left )
        {
            //Debug.Log ( "Left Drag" );

            drawGridBrash_CurrNode = GetNodeByWorldPos ( currGridBrushPointerPos );


            Node startNode = drawGridBrash_StartNode;
            Node currNode = drawGridBrash_CurrNode;

            if ( currNode != null && startNode != null
                 && currNode != startNode && currNode.links.Contains ( startNode )
                 && currNode.pathNode != null && startNode.pathNode != null )
            {
                if (currNode.pathNode.node == null) currNode.pathNode.node = currNode;
                if ( startNode.pathNode.node == null ) startNode.pathNode.node = startNode;


                currNode.pathNode.AddLink ( startNode.pathNode );

                //path
                if ( pathToTarget == null )
                {
                    pathToTarget = new List<PathNode> ( );
                }
                if ( !pathToTarget.Contains ( currNode.pathNode ) ) pathToTarget.Add ( currNode.pathNode );

                ScanPathForSignature();
                //start node = currNode
                //drawGridBrash_StartNode = currNode;
            }
            drawGridBrash_StartNode = currNode;


        }


/*        else if ( eventData.button == PointerEventData.InputButton.Middle )
            Debug.Log ( "Middle Drag" );*/


        else if ( eventData.button == PointerEventData.InputButton.Right )
        {
            //Debug.Log ( "Right Drag" );
            DeleteBlock ( );

            ScanPathForSignature();
        }
        
    }

    #endregion


    #endregion



}



#region Data

[Serializable]
public class PathNode
{
    #region Vars

    public Node node;

    public float step = 0;
    public float dist = 0;
    public float cost = 0;

    public List<PathNode> links = new List<PathNode>();
    //public List<PathNode> pathLinks = new List<PathNode> ( );



    #endregion


    public void CalcDist ( Node target, Node currStepNode )
    {
        dist = Vector3.Distance(node.worldPos, target.worldPos/*.transform.position*/);
        step = Vector3.Distance(node.worldPos, currStepNode.worldPos/*.transform.position*/);

        cost = dist + step;
    }


    #region Links

    public void AddLink(PathNode nodeToAdd)
    {
        if (nodeToAdd != null && nodeToAdd.node!=node )
        {
            if(links!=null && !links.Contains(nodeToAdd)) links.Add(nodeToAdd);
            //nodeToAdd.AddLink(this);
            if(nodeToAdd.links!=null && !nodeToAdd.links.Contains(this))nodeToAdd.links.Add(this);
        }
    }
    public bool RemoveLink ( PathNode nodeToRemove )
    {
        if ( /*nodeToRemove != null && nodeToRemove.node != node && links.Contains ( nodeToRemove )*/true )
        {
            if (links.Contains(nodeToRemove))
            {
                links.Remove ( nodeToRemove );
            }
            if (nodeToRemove.links.Contains(this))
            {
                nodeToRemove.links.Remove(this);
            }
            //nodeToRemove.RemoveLink ( this );

            return true;
        }
        return false;
    }

    public void RemoveAllLinks()
    {
        while (links.Count>0)
        {
/*            if (!)
            {
                break;
            }*/
            RemoveLink(links[0]);
        }
        //links.Clear();
    }

    /*        public void AddPathLink ( PathNode nodeToAdd )
            {
                if ( nodeToAdd != null && !pathLinks.Contains ( nodeToAdd ) )
                {
                    pathLinks.Add ( nodeToAdd );
                    nodeToAdd.AddLink ( this );
                }
            }*/


    #endregion

}


public class Node
{
    #region Vars


    public Vector3 vectorPos;
    public Vector3 worldPos;

    [HideInInspector] public List<Node> links = new List<Node>();
    public static List<Node> allMapNodesList = new List<Node>();


    public bool occupiedByTile = false;
    public PathNode pathNode;
    

    #endregion


    public void CreateLink(Node node)
    {
        if(!links.Contains(node)) links.Add(node);
        if(!node.links.Contains(this))node.links.Add(this);
    }
}

#endregion




#region CustomEditorButton
#if UNITY_EDITOR

//[CanEditMultipleObjects]
[CustomEditor ( typeof ( MazeGenerator ) )]
public class MazeGenerator_Editor : Editor
{

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ( );


        var generator = (MazeGenerator) target;




        if ( GUILayout.Button ( "Generate nodes" ) )
        {
            generator.BuildPathGrid();
        }
        if ( GUILayout.Button ( "Past signatures" ) )
        {
            generator.ScanSigantures();
        }

        if ( GUILayout.Button ( "Clear" ) )
        {
            generator.CleanSpawned(true);
        }
    }

/*    void OnSceneGUI()
    {
        return;

        var generator = (MazeGenerator)target;


        Vector3 pos = generator.currGridBrushPointerPos;

        if (generator.drawingGridBrushNow)
        {
            generator.Update ( );//.CalcCurrPointerPosition ( );
            SceneView.RepaintAll ( );
            
            OverrideMouseButtons ( generator, pos );
        }



        var e = Event.current;
        switch ( e.type )
        {
            case EventType.keyDown:
            {
                if ( e.keyCode == ( KeyCode.B ) )
                {
                    generator.drawingGridBrushNow = !generator.drawingGridBrushNow;
                }
                break;
            }
        }



        Handles.color = generator.drawingGridBrushNow ? Color.red : Color.white;
        Handles.DrawWireCube ( pos, Vector3.one* generator.stepDist );
        Handles.DrawLine(pos, pos+Vector3.up*generator.stepDist/2);


        if ( generator.currGridBrushPointer_DragStartPos.sqrMagnitude>0 )
        {
            Handles.DrawLine ( generator.currGridBrushPointer_DragStartPos, pos );
        }

    }*/


    void OverrideMouseButtons(MazeGenerator generator, Vector3 pos)
    {
        int controlId = GUIUtility.GetControlID ( FocusType.Passive );

        if ( Event.current.button != 2 )
        {
            switch ( Event.current.GetTypeForControl ( controlId ) )
            {
                case EventType.MouseDown:
                    GUIUtility.hotControl = controlId;

                    //Ваша логика использования события MouseDown

                    //Левая кнопка мыши
                    if ( Event.current.button == 0 )
                    {
                        Debug.Log ( "SceneGUIClick left" );
                        generator.currGridBrushPointer_DragStartPos = pos;

                        generator.drawGridBrash_StartNode = generator.GetNodeByWorldPos(pos);
                    }

                    //Правая кнопка мыши
                    if ( Event.current.button == 1 )
                    {
                        Debug.Log ( "SceneGUIClick Right" );

                        generator.DeleteBlock ( );
                    }



                    //Используем событие
                    Event.current.Use ( );

                    break;

                case EventType.MouseDrag:
                    GUIUtility.hotControl = controlId;

                    //Левая кнопка мыши
                    if ( Event.current.button == 0 )
                    {
                        generator.drawGridBrash_CurrNode = generator.GetNodeByWorldPos ( pos );


                        Node startNode = generator.drawGridBrash_StartNode;
                        Node currNode = generator.drawGridBrash_CurrNode;

                        if ( currNode != null && startNode != null
                             && currNode != startNode && currNode.links.Contains ( startNode )
                             && currNode.pathNode != null && startNode.pathNode != null )
                        {
                            //connect to start node
                            //currNode.CreateLink(startNode);
                            currNode.pathNode.AddLink ( startNode.pathNode );
                            //currNode.occupiedByTile = true;

                            //path
                            if ( generator.pathToTarget == null )
                            {
                                generator.pathToTarget = new List<PathNode> ( );
                            }
                            generator.pathToTarget.Add ( currNode.pathNode );

                            //start node = currNode
                            generator.drawGridBrash_StartNode = currNode;
                        }
                    }

                    if (Event.current.button == 1)
                    {
                        generator.DeleteBlock ( );
                    }

                    Event.current.Use ( );


                    break;

                case EventType.MouseUp:
                    //Возвращаем другим control доступ к событиям мыши
                    generator.currGridBrushPointer_DragStartPos = Vector3.zero;

                    GUIUtility.hotControl = 0;
                    Event.current.Use ( );

                    break;

            }
        }
    }



}
#endif

#endregion