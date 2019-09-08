using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ProceduralToolkit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;


#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class MazeGenerator : MonoBehaviour, IDragHandler, IBeginDragHandler
{

    #region Vars

    public bool genOnStart = false;

    
    [Serializable]
    public class DrawPathLinksSettings
    {
       public bool draw = true;
       public bool onlyUpDown = false;
       [Range(-1, 200)]
       public int floor = -1;
       public int floorRange = 10;
       public float floorRangePow = 5;
       public int maxFloor = 1;

       
       public float linkShift = 0.1f;

       public bool animateFloors = false;
       public float floorAnimationCounter = 0;
       public float floorAnimationCounter_triggerValue = 0;
       
       public Color defaultLinksColor = new Color(1,1,0, 0.1f);
       public Color floorColor = Color.cyan;

       public Slider floorSlider;
       
       public int Floor
       {
           get { return floor; }
           set
           {
               floor = value;
               
               if (floorSlider != null)
               {
                   floorSlider.maxValue = MaxFloor;
                   floorSlider.value = floor;
               }
           }
       }

       public int MaxFloor
       {
           get { return maxFloor; }
           set
           {
               maxFloor = value;
               Floor = floor;
           }
       }
    }
    [Header("grid")] 
    public DrawPathLinksSettings drawPathLinksSettings = new DrawPathLinksSettings();

    public Vector3 gridSize = new Vector3(50,50,50);
    public int stepDist = 20;

    [Header("Path Noise")] 
    public int pathNoiseCount = 10;


    #region Path Grow

    [Serializable]
    public class PathGrowSettings
    {
        public int pathGrowBranchesCount = 40;
        public int pathGrowMaxCount = 30;
        //public int pathGrow_upDownLinkRemoveChance = 90;
        public int pathGrow_sameDirectionMult = 5;
        public int pathGrow_verticalMult = 20;

        public float nextNodeSelection_onePlaneChance = 90;
        public float nextNodeSelection_useVectorSortingChance = 50;

        public float inserSignatureChance = 20;
        
    }
    [Header("Path Grow")] 
    public PathGrowSettings pathGrowSettings = new PathGrowSettings();

    #endregion


    //public Transform startNodeTr, endNodeTr;
    Node startNode, endNode,  pathStartNode, pathEndNode;
    public Transform startPoint;

    [HideInInspector] 
    public List<Node> pointsOfInterest = new List<Node> ( );


    [Header("Signatures")]
    public string pathToSignatures = "MazeGeneratorSignatures";
    [HideInInspector] public List<TileSignature> signatures = new List<TileSignature> ( );


    //[HideInInspector] 
    //private List<Node> nodeList { get; set; } = new List<Node>();
    private Dictionary<Vector3Int, Node> nodeDict = new Dictionary<Vector3Int, Node>();

    [HideInInspector]
    public List<PathNode> pathToTarget = new List<PathNode> ( );


    //draw grid brush
    [Header("Grid Brush")] 
    public bool drawingGridBrushNow = true;
    public int currHeightLayer = 0;
    private BoxCollider planeCollider;

    private Vector3 currGridBrushPointerPos = Vector3.zero;
    private Vector3 currGridBrushPointer_DragStartPos = Vector3.zero;
    private Node drawGridBrash_StartNode;
    private Node drawGridBrash_CurrNode;

    public LayerMask gridBrushLayerMask = -1;



    #region SignatureInsertion

    [Serializable]
    public class SignatureInsertion
    {
        public TileSignature[] signatureToInsert = new TileSignature[0];
        public int maxCount = 5;
    }
    [Header("PreInsert Big Signatures")]
    public SignatureInsertion signatureInsertion = new SignatureInsertion();

    #endregion
    
    #endregion



    #region Mono

    // Use this for initialization
    private void Start()
    {
        if (planeCollider == null) planeCollider = GetComponent<BoxCollider>();

        if (genOnStart)
        {
            BuildPathPointsForSignatureCheck ( );
            ScanSigantures ( );
        }
        var drS = drawPathLinksSettings;
        drS.MaxFloor = (int)(gridSize.y / stepDist);
        SetFloor(0);
    }


    public void Update()
    {
        //CalcCurrPointerPosition();

        DrawPathLinks();

        
        
        if (Input.GetKeyDown(KeyCode.G) && Input.GetKeyDown(KeyCode.R))
        {
            BuildPathPointsForSignatureCheck();
        }
    }

    
    private void OnGUI()
    {
        GUILayout.BeginVertical();
        
        GUILayout.Label(currGridBrushPointerPos.ToString());
        
        GUILayout.EndVertical();
    }



    public void SetFloor(float val)
    {
        drawPathLinksSettings.floor = (int)val;
        //DrawPathLinks();
    }
    
    public void DrawPathLinks()
    {
        var drS = drawPathLinksSettings;
//        drS.MaxFloor = (int)(gridSize.y / stepDist);
        
        if ( !drS.draw && pathToTarget != null && pathToTarget .Count>1) return;

        if (drS.animateFloors)
        {
            drS.floorAnimationCounter++;
            if (drS.floorAnimationCounter >= drS.floorAnimationCounter_triggerValue)
            {
                drS.floorAnimationCounter = 0;
                drS.Floor++;
                if (drS.Floor > drS.MaxFloor)
                {
                    drS.Floor = 0;
                }
            }
        }


/*        foreach (var n in nodeDict.Values)
        {
            if (n.hasPreinserted)
            {
                Color color = Color.blue;

                Debug.DrawRay (
                    n.worldPos,
                    Vector3.up*3, 
                    color
                );
            }
        }*/
        
        
        
        foreach ( var pathNode in pathToTarget)
        {
/*            Debug.DrawRay(
                pathNode.node.worldPos ,
                    Quaternion.Euler(0, Random.Range(0,350), 0) * Vector3.one*50
            );*/
            if (pathNode == null || pathNode.node == null) continue;
            

            Color color = pathNode.node.hasPreinserted ? Color.blue : new Color(1,1,0,0.5f);

/*            //if (drS.drawPathLinks_onlyUpDown==false)
            {
                Debug.DrawRay (
                    pathNode.node.worldPos,
                    Vector3.up*3, 
                    color
                );
            }*/


            float shift = drS.linkShift;//0.1f;
            foreach ( var link in pathNode.links )
            {
                if(link==null || link.node==null) continue;

                color = drS.defaultLinksColor;//new Color(1,1,0,0.05f);

                
                if(drS.onlyUpDown && Vector3.Angle( link.node.worldPos - pathNode.node.worldPos, Vector3.up) > 11 ) continue;
                int floorDiff = (drS.Floor - (int)pathNode.node.vectorPos.y);
                float drawShift_floorMult = 1;
                if(drS.Floor>-1 && floorDiff>-1 && floorDiff < drS.floorRange)
                {
                    float lerp = Mathf.Lerp(0f, 1f, ((float)drS.floorRange-floorDiff)/(float)drS.floorRange);
                    lerp = Mathf.Pow(lerp, drS.floorRangePow);
                    color = Color.Lerp(color, drS.floorColor, lerp); //new Color(1,1,0.5f,/*0.99f*/ lerp);
                    drawShift_floorMult = 5;
                }

                if (drS.Floor < 0)
                {
                    color.a = 0.5f;
                }
                
                

                Debug.DrawLine (
                    pathNode.node.worldPos + Vector3.one * Random.Range ( -shift*drawShift_floorMult, shift*drawShift_floorMult ),
                    link.node.worldPos + Vector3.one * Random.Range ( -shift*drawShift_floorMult, shift*drawShift_floorMult ),
                    color, 0.1f, false
                );
            }
        }
    }



    
    #endregion






    #region MazeGen

    private string pathToSignaturesLast = "";
    [ContextMenu("Generate path nodes")]
    public void BuildPathPointsForSignatureCheck()
    {
        DateTime startTime = DateTime.Now;
        
/*
        
        if (/*signatures.Count == 0 || pathToSignaturesLast != pathToSignatures#1#loadReses && drawingGridBrushNow==false)
        {
            signatures = Resources.LoadAll<TileSignature> ( pathToSignatures ).ToList ( );
            pathToSignaturesLast = pathToSignatures;
        }*/

        CleanSpawned(true);

        GenerateGrid ( );

/*        if ( startPoint != null )
        {
            Vector3 startPointClampedPos = ClampPositionToGrid ( startPoint.position );

            startNode = nodeList.FirstOrDefault ( v => Vector3.Distance ( v.worldPos/*.transform.position#1#, startPointClampedPos ) < stepDist );
            if ( startNode != null ) pathStartNode = startNode;
        }*/


        #region SIGNATURE INSERTION

        var hG = gridSize.z / stepDist / 2f;
        var insertSignatureStartShift = Vector3.zero;//new Vector3( 0,0, Random.Range( -hG,  hG ));
        InsertSignature(signatureInsertion.signatureToInsert.GetRandom(), insertSignatureStartShift );

        if (pointsOfInterest.Count > 0) endNode = pointsOfInterest[0];

        #endregion


/*        SetStartEndPoints ( transform.position );
        var pathFindLoopResult = PathFindLoop ( );
        if (pathFindLoopResult != null) pathToTarget.AddRange(pathFindLoopResult);*/

        //pointsOfInterest.Add ( pathToTarget.Last ( ).node );


        GrowPath();
        PathNoise ( );
        CloseOpenPathEnds();
        ConnectSeparatedPathParts();
  

        DrawPathLinks();
        Debug.Log("GenerationFinished. Time: " + (DateTime.Now - startTime).TotalSeconds);
    }

    
    private void ConnectSeparatedPathParts()
    {
        foreach (var pp in pathToTarget)
        {
            var duplicatePathNodes = pathToTarget.Where(v => v.node == pp.node && v != pp).ToList();

            foreach (var duplicatePathNode in duplicatePathNodes)
            {
                if (duplicatePathNode != null)
                {
                    foreach (var ppLink in pp.links)
                    {
                        //if (ppLink.node.hasPreinserted == false) continue;

                        if (duplicatePathNode.links.Any(v => v.node == ppLink.node)) continue;
                        if (Vector3.Distance(ppLink.node.vectorPos, duplicatePathNode.node.vectorPos) > /*stepDist * */1.1f) continue;

                        duplicatePathNode.links.Add(ppLink);
                    }

                    foreach (var dpLink in duplicatePathNode.links)
                    {
                        //if (dpLink.node.hasPreinserted == false) continue;

                        if (duplicatePathNode.links.Any(v => v.node == dpLink.node)) continue;
                        if (Vector3.Distance(dpLink.node.vectorPos, duplicatePathNode.node.vectorPos) > /*stepDist * */1.1f) continue;

                        pp.links.Add(dpLink);
                    }
                }
            }

            
            pp.links = pp.links.Where(v =>
                {
                    if (Vector3.Distance(pp.node.vectorPos, v.node.vectorPos) < /*stepDist **/ 1.1f)
                    {
                        return true;
                    }
                    else
                    {
                        if (v.node.hasPreinserted == true) return true;
                    }

                    return false;
                })
                .ToList();
        }
    }

    private bool InsertSignature(TileSignature signatureToInsert, Vector3 insertSignatureStartShift)
    {
        PathNode prevNode = null;
        
        if (signatureToInsert != null)
        {
            List<Vector3> signatureVectorWithLinks = new List<Vector3>(signatureToInsert.signatureVector);
            signatureVectorWithLinks.Insert(0, signatureToInsert.signatureLinks[0]);
            signatureVectorWithLinks.Insert(signatureVectorWithLinks.Count, signatureToInsert.signatureLinks[1]);

            
            var randRotV = (float)Random.Range(0, 4) * 90f;
            var randRotQ = Quaternion.Euler(0, randRotV, 0);

            for (var i = 0; i < signatureVectorWithLinks.Count; i++)
            {
                var v3 = signatureVectorWithLinks[i];
                v3 = randRotQ * v3;
                Vector3 wPos = transform.position + insertSignatureStartShift + v3 * stepDist; //transform.position + (insertSignatureStartShift + v3) * stepDist;

                signatureVectorWithLinks[i] = wPos/*v3*/;
            }

            List<Node> nodeList = new List<Node>();
            for (var i = 0; i < signatureVectorWithLinks.Count; i++)
            {
                Vector3 v3 = signatureVectorWithLinks[i];
                //Debug.DrawRay(v3, Vector3.up*10, Color.magenta, 10);

                Node node = GetNodeByWorldPos( /*wPos*/ v3);
                if (node == null || node.hasPreinserted  || (i>2 && pathToTarget.Any(v=> v.node==node)) )
                {
                    print("TryInsertSignature_NodeAlreadyPreinserted");
                    return false;
                }
                
                nodeList.Add(node);
            }


            for (var i = 0; i < nodeList.Count; i++)
            {
                //Debug.DrawRay(wPos, Vector3.up * 50, Color.magenta, 20);
                Node node = nodeList[i];
                
                //POINTS OF INTERESTS from links
                if (i == 0 || i == nodeList.Count - 1)
                {
                    //pointsOfInterest.Add( node);
                }
                else
                {
                    node.hasPreinserted = true;
                }

                
                PathNode pathNode = new PathNode() {node = node, step = stepDist};
                if (prevNode != null) pathNode.AddLink(prevNode);

                prevNode = pathNode;

                pathToTarget.Add(pathNode);
            }

            
            
            
            //additional points => set preinserted
/*            for (int i = 0; i < signatureToInsert.signatureVector_additionalPoints.Count; i++)
            {
                Vector3 v3 = signatureToInsert.signatureVector_additionalPoints[i];
                v3 = randRotQ * v3;

                Vector3 wPos = transform.position + (insertSignatureStartShift + v3) * stepDist;

                //Debug.DrawRay(wPos, Vector3.up * 50, Color.magenta, 20);
                Node node = GetNodeByWorldPos(wPos);
                if (node != null) node.hasPreinserted = true;
            }*/

            return true;

        }

        return false;
    }

    
    
    [ContextMenu("ScanSignatures")]
    public void ScanSigantures()
    {
        //ScanPathForSignature();
        //StartCoroutine ( ScanPathForSignature ( ) );
        ScanPathForSignature();
    }

    #endregion
    
    
    
    #region PathGen_Utils

    private void UpdateColorIndication ()
    {
        if(pathToTarget==null || pathToTarget.Count==0) return;
        
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

        
/*        foreach ( var interestPoint in pointsOfInterest )
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
        }*/
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


        //nodeList = new List<Node> ( );
        nodeDict = new Dictionary<Vector3Int, Node>();
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


                    Node node = new Node
                    {
                        worldPos = startPos + spawnShift,
                        vectorPos = new Vector3(x, y, z)
                    }; 
                    //nodeList.Add ( node );
                    nodeDict.Add( new Vector3Int(x,y,z), node );

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

        Node[] nodes = nodeDict.Values//nodeList
            .Where ( v =>v.hasPreinserted==false && Vector3.Distance ( v.worldPos/*.transform.position*/, pointOfInterest ) > gridSize.x / 2 /*Mathf.PI*/)
                .ToArray ( );
        if ( nodes.Length > 0 )
        {
            if ( endNode == null ) endNode = nodes[Random.Range ( 0, nodes.Length )];

            pointOfInterest = endNode.worldPos/*.transform.position*/;
            nodes = nodeDict.Values//nodeList
                .Where ( v => Vector3.Distance ( v.worldPos/*.transform.position*/, pointOfInterest ) > gridSize.x / 2 /*Mathf.PI*/)
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

    
    
    
    [ContextMenu("Clean")]
    public void CleanSpawned()
    {
        CleanSpawned(true);
    }

    public void CleanSpawned(bool cleanLists)
    {
        //CLEAR PREV NODES
        foreach ( Transform child in transform.GetComponentsInChildren<Transform> (true ) )
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


            //if (nodeList != null) nodeList.Clear();
            if (nodeDict != null) nodeDict.Clear();
            pathToTarget = new List<PathNode>();
            if (pointsOfInterest != null) pointsOfInterest.Clear();

            if (openNodes != null) openNodes.Clear();
            if (closedNodes != null) closedNodes.Clear();
        }
    }




    #region World pos node test

    [Header("World pos node test")]
    public Transform wordPOsGizmoTr;
    
    [Serializable]
    public class WorldPosNodeDebug
    {
        public Vector3 wPOsNodePos;
        public List<Vector3> wPOsNodeLinks = new List<Vector3>();
    }
    public List<WorldPosNodeDebug> worldPosNodeDebugList = new List<WorldPosNodeDebug>();
    public void GetNodeByWorldPosGizmo()
    {
        worldPosNodeDebugList.Clear();
        var nodeByWorldPosList = pathToTarget
            .Where(v => Vector3.Distance(v.node.worldPos, wordPOsGizmoTr.position)<stepDist)
            .ToList();

        worldPosNodeDebugList = nodeByWorldPosList.Select(v =>
        {
            Debug.DrawRay(v.node.worldPos, Vector3.up*20, Color.magenta, 5);

            return new WorldPosNodeDebug()
                {wPOsNodePos = v.node.worldPos, wPOsNodeLinks = v.links.Select(n => n.node.worldPos).ToList()};
        }).ToList();

    }

    #endregion
    
    
    #endregion

    
    #region Build Various Pathes


    void GrowPath()
    {
        var gs = pathGrowSettings;
        List<int> branchesStartFloors = new List<int>();
        List<Vector3> verticalLinksVectorsPosList = new List<Vector3>();
        
        int lastFloor = 0;
        int floorLockCounter = 0;

        for (int i = 0; i < gs.pathGrowBranchesCount; i++)
        {
            #region Selecting pathNode to GROW

            PathNode pathNode = null;
            var pathNodes = pathToTarget.Where(v => v.node.hasPreinserted == false).ToList();

            
            if(pathNodes.Count==0)
            {
                pathNode = pathToTarget[pathToTarget.Count-1];
            }
            else
            {
/*                if (branchesStartFloors.Count>0)
                {
                    int floor = 0;

                    if (floorLockCounter==0)
                    {
                        var ssT = branchesStartFloors
                            .GroupBy(v => v)
                            .Select(v => new Vector2Int(v.Key, v.Count()))
                            //.Where(v=>v[1] < 10)
                            .ToList();
                        if (ssT.Count>0)
                        {
                            floor = ssT
                                .OrderBy(v => v[1]) //TODO не выбирать этажи, которые имеют заполненных соседей 
                                .First()[0];
                        }
                    }
                    else
                    {
                        floorLockCounter--;
                        floor = lastFloor;
                    }
                    
                    
                    
                    var pathNodesFloorTemp = pathNodes.Where(v => (int) v.node.vectorPos.y == floor).ToList();
                    if (pathNodesFloorTemp.Count > 0)
                    {
                        pathNodes = pathNodesFloorTemp;
                        lastFloor = floor;
                        
                        if (floorLockCounter==0 && Random.Range(0, 100) < 30)
                        {
                            floorLockCounter = Random.Range(3, 20);
                        }
                    }
                }*/

/*                var pathNodesFiltered = pathNodes
                    .Where(b =>
                    verticalLinksVectorsPosList.All(v=>
                    {
                            var lPos = b.node.vectorPos;
                            return Vector3.Distance(v, lPos) > 5; //иначе разрешать точки за пределами радиуса
                        })
                    )
                    .ToList();
                if (pathNodesFiltered.Count > 0)
                {
                    pathNodes = pathNodesFiltered;
                }*/
                pathNode = pathNodes.GetRandom();
            }
            
            List<Node> branchNodes = new List<Node>( /*pathToTarget.Select(p=>p.node).ToList()*/ );
            bool useVectorSorting = Random.Range(0f, 100f) < gs.nextNodeSelection_useVectorSortingChance;
            bool onePlane = Random.Range(0f, 100f) < gs.nextNodeSelection_onePlaneChance ;
            bool verticalLinks = Random.Range(0f, 100f) < 20 ;

            
            if (i < 5)
            {
                onePlane = true;
                verticalLinks = false;
            }
            if (i> 5 && i < 15){
                onePlane = false; //создавать на старте больше вертикальных веток
                verticalLinks = true;
                useVectorSorting = true;
            }

            #endregion
            
            
            //GROWING fron selected path node
            for (int j = 0; j < gs.pathGrowMaxCount; j++)
            {
                print("BranchTick");

                if (pathNode != null)
                {
                    var links = pathNode.node.links.Where(v=> v.hasPreinserted==false && branchNodes.Contains(v) == false).ToList();
                    if(links.Count==0) break;

                    
                    //INSERT SIGNATURE
                    if (/*nextNode*/pathNode.node.hasPreinserted==false && Random.Range(0f, 100f) < gs.inserSignatureChance )
                    {
                        print("TryInsertSignature");
                        if (InsertSignature(
                            signatureInsertion.signatureToInsert.GetRandom(), /*nextNode*/
                            pathNode.node.worldPos)
                        )
                        {
                            print("SignatureInserted");
                            continue;
                        }
                    }
                  
                    #region Remove UpDownLinks

                    //if (/*useVectorSorting && */onePlane )
                    {
                        if (links.Count > 1 )
                        {
                            var link = links.FirstOrDefault(v=> (v.vectorPos.y - pathNode.node.vectorPos.y) > 0.1f);
                            if (link != null && (onePlane || ExistingLinksIsNearToThis(verticalLinksVectorsPosList, link.vectorPos) ) )
                            {
                                links.Remove(link);
                            }
                        }
                        if (links.Count > 1 )
                        {
                            var link = links.FirstOrDefault(v=> (v.vectorPos.y - pathNode.node.vectorPos.y) < -0.1f);
                            if (link != null && (onePlane || ExistingLinksIsNearToThis(verticalLinksVectorsPosList, link.vectorPos)) )
                            {
                                links.Remove(link);
                            }
                        }
                    }

                    #endregion
                    
                    #region link with same direction

                    if ( useVectorSorting && /*!onePlane && */links.Count > 0 && pathNode.links.Count>0)
                    {
                        Vector3 pathNodeVector = (pathNode.node.worldPos - pathNode.links[0].node.worldPos).normalized;
                        
                        var forwardLink = links.FirstOrDefault(v=> Vector3.Angle(pathNodeVector, (v.worldPos - pathNode.node.worldPos).normalized) < 30);
                        if (forwardLink != null)
                        {
                            int upDownMult = 1;
                            if (verticalLinks && Mathf.Abs(Vector3.Dot(pathNodeVector, Vector3.up)) > 0.9f)
                            {
                                upDownMult = gs.pathGrow_verticalMult;
                            }
                            
                            for (int k = 0; k < gs.pathGrow_sameDirectionMult * upDownMult; k++)
                            {
                                links.Add(forwardLink);
                            }
                        }
                    }

                    #endregion
                    
                    
                    var nextNode = links.GetRandom();


                    
                    var nextPathNode = new PathNode(){node = nextNode/*, IsVertical =*/ };
                    
                    pathToTarget.Add(nextPathNode);
                    pathNode.AddLink(nextPathNode);
                    branchNodes.Add( nextNode);
                    
                    
                    bool isVertical = Mathf.Abs(pathNode.node.vectorPos.y - nextNode.vectorPos.y) > 0.1f;
                    if (isVertical)
                    {
                        verticalLinksVectorsPosList.Add(pathNode.node.vectorPos);
                        verticalLinksVectorsPosList.Add(nextNode.vectorPos);
                    }
                    
                    branchesStartFloors.Add( (int)nextNode.vectorPos.y);
                }

                pathNode = pathToTarget[pathToTarget.Count - 1];
            }
        }

    }

    private static bool ExistingLinksIsNearToThis(List<Vector3> verticalLinksVectorsPosList, Vector3 lPos)
    {
        //Vector3 lPos = link.vectorPos;
        return verticalLinksVectorsPosList.Any(v=>
        {
            //разрешать точки, находящиеся на/под целевой
            if ((int) v.x == (int) lPos.x && (int) v.z == (int) lPos.z) return false;

            //return true;
            return Vector3.Distance(v, lPos) < 5; //иначе разрешать точки за пределами радиуса
        }) == false;
    }


    private void PathNoise ()
    {
        for ( int i = 0; i < pathNoiseCount; i++ )
        {
            PathNoise_GetStartNodeFromPathToTraget();
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
            if ( startNode == null )
            {
                PathNoise_GetStartNodeFromPathToTraget();
            }
            SetStartEndPoints ( startNode.worldPos/*.transform.position*/ );


            //Build Path
            var pathFindLoopResult = PathFindLoop ( );
            if (pathFindLoopResult != null)
            {
                pathToTarget.AddRange(pathFindLoopResult.Except(pathToTarget));
                pointsOfInterest.Add(pathToTarget.Last().node);
            }

        }
    }
    
    private void CloseOpenPathEnds()
    {
        int closedEndsCounter = 0;
        for (int i = 0; i < 200; i++)
        {
            Node[] openNodesPair = GetPathOpenEnd();
            if (openNodesPair[0] == null)
            {
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
                if(startNode.hasPreinserted || endNode.hasPreinserted) continue;

                //Build Path
                closedEndsCounter++;
                var pathFindLoopResult = PathFindLoop();
                if (pathFindLoopResult != null)
                {
                    pathToTarget.AddRange(pathFindLoopResult/*.Except(pathToTarget)*/ );
                }
            }
        }

        print("Closed Dead End count = " + closedEndsCounter);
    }

    
    
    private void PathNoise_GetStartNodeFromPathToTraget()
    {
        if (pathToTarget.Count > 0)
        {
            startNode = pathToTarget.Last().node;
        }
        else
        {
/*            startNode = GetNodeByArrayId(
                new Vector3(0, 0, 0)
            ); //GetNodeByWorldPos( ClampPositionToGrid(transform.position) );*/

            startNode = /*nodeList*/nodeDict.Values.ToList().GetRandom();
        }
    }

    private void BuildLongStarightPathesOnCross()
    {
        //MapNode dirNode = GetEmptyNodeConnection(startNode);//startNode.links.FirstOrDefault ( v => !pathToTarget.Select ( x => x.node ).Contains ( v ) );
        if (startNode == null)
        {
            //startNode = pathToTarget.Last ( ).node;
            PathNoise_GetStartNodeFromPathToTraget();
        }
        startNode = GetRandomPointOnPathFarFromStartNode();
        if(startNode==null) return;
        

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
                var wPos = startNode.vectorPos + rot* dir * ( maxSteps - j );
                Vector3Int wPosInt = new Vector3Int( (int)wPos.x, (int)wPos.y, (int)wPos.z);
                retNode = GetNodeByArrayId ( wPosInt  );
                if ( retNode != null )
                {
                    endNode = retNode;

                    //Build Path
                    var pathFindLoopResult = PathFindLoop ( );
                    if (pathFindLoopResult != null)
                    {
                        pathToTarget.AddRange(pathFindLoopResult.Except(pathToTarget));
                        pointsOfInterest.Add(pathToTarget.Last().node);
                    }

                    break;
                }
            }

        }
        
    }

    private Node GetRandomPointOnPathFarFromStartNode ()
    {
        if (startNode == null) return null;


        List<PathNode> pathPointsFar = pathToTarget.Where (v =>
            v.node.hasPreinserted==false 
            &&  Vector3.Distance ( v.node.worldPos, startNode.worldPos/*.transform.position*/ ) > gridSize.x / 3 ).ToList ( );
        if (pathPointsFar.Count == 0) return null;
        
        return pathPointsFar[Random.Range ( 0, pathPointsFar.Count )].node;
    }

    private Node[] GetPathOpenEnd ()
    {
        Node[] retNodes = new Node[2];

        List<PathNode> openPoints = pathToTarget.Where(v =>v.node.hasPreinserted==false &&  v.links.Count == 1 && v.node!=null ).ToList();

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
        if( pathToTarget.Count<2) return;

        DateTime startTime = DateTime.Now;

        //reset occupation flag for every node
/*        foreach (var node in nodeList)
        {
            node.occupiedByTile = false;
        }*/

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


        print("Not occupied nodes count = "+pathToTarget.Count(v=> v!=null && !v.node.occupiedByTile) );
        Debug.Log("Signatures checked and pasted for " + (DateTime.Now - startTime).TotalSeconds);
        //yield return null;
    }


    //public float signaturesScaleMult = 1;
    [Header("Scan Signatures")]
    public bool loadReses = true;

    private bool ScanSignature ( List<PathNode> nodes, TileSignature signature)
    {
                
        if (/*signatures.Count == 0 || pathToSignaturesLast != pathToSignatures*/loadReses && drawingGridBrushNow==false)
        {
            signatures = Resources.LoadAll<TileSignature> ( pathToSignatures ).ToList ( );
            pathToSignaturesLast = pathToSignatures;
        }
        
        
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

                #region Tile Orientation

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

                #endregion



                #region SPAWN

#if UNITY_EDITOR
                var go2 = PrefabUtility.InstantiatePrefab ( signature.transform,
                    transform.gameObject.scene
                ) as Transform;

                if (go2 != null)
                {
                    go2.position = nodes[0].node.worldPos;
                    go2.rotation = Quaternion.LookRotation(rotDir) * Quaternion.Euler(signature.rotCorrection);
                    go2.parent = transform;
                    //go2.localScale = Vector3.one * signaturesScaleMult;
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

                #endregion

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
            
            
            var wPos = node.vectorPos + dir ;
            Vector3Int wPosInt = new Vector3Int( (int)wPos.x, (int)wPos.y, (int)wPos.z);
            retNode = GetNodeByArrayId ( wPosInt  );
            
            //retNode = GetNodeByArrayId (node.vectorPos + dir );
            if ( retNode != null ) nodes.Add ( retNode );
        }

        return nodes;
    }

    public Node GetNodeByArrayId ( Vector3Int vector )
    {
/*        for (int i = 0; i < nodeList.Count; i++)
        {
            Node v = nodeList[i];
            //if ( (v.vectorPos - vector).sqrMagnitude<0.1 ) return v;
            if (Vector3.Distance(v.vectorPos, vector) < 0.1f) return v;
        }*/
        if (nodeDict.ContainsKey(vector)) return nodeDict[vector];
        
        return null;
    }

    public Node GetNodeByWorldPos ( Vector3 pos, bool clamp = false )
    {
        var gridCenterCorrection = ( gridSize / 2 - new Vector3 ( stepDist / 2f, stepDist / 2f, stepDist / 2f ) );
        var localPos = transform.InverseTransformPoint ( pos );
        pos = ( localPos + gridCenterCorrection )/ stepDist;
        
        if (clamp)
        {
            pos = ClampPositionToGrid(pos);
        }
        
        return GetNodeByArrayId ( /*pos*/ new Vector3Int( (int)pos.x, (int)pos.y, (int)pos.z) );
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


    #region Recursive pathfind

/*    public class RecursivePathFindResult
    {
        public Node node;
        public Node endNode;
        public List<Node> path = new List<Node>();
        public int cost = 0;

        public int currDepth = 0;
        public int maxDepth = 10;


        public enum State
        {
            working,
            finished,
            failed
        };

        public State currState = State.working;

    }
/*    public class RecursivePathBranch
    {
        public Node lastNode;
        public List<Node> path = new List<Node>();
    }#1#

    public RecursivePathFindResult PathFind_Iterator(RecursivePathFindResult rpf )
    {
        pathToTarget = new List<PathNode>();
        openPF_Branches = new List<RecursivePathFindResult>();
        openPF_Branches.Add(rpf);
        PathFind_R(rpf);

        for (int i = 0; i < 100; i++)
        {
            var branchesCount = openPF_Branches.Count;
            for (var index = 0; index < branchesCount; index++)
            {
                var r = openPF_Branches[index];
                if(r.currState != RecursivePathFindResult.State.working) continue;
                
                PathFind_R(r);
            }
        }

        return openPF_Branches
            .Where(v => v.currState == RecursivePathFindResult.State.finished)
            .OrderBy(v => v.path.Count).FirstOrDefault();
    }

    List<RecursivePathFindResult> openPF_Branches = new List<RecursivePathFindResult>();
    //List<RecursivePathFindResult> removedBranches = new List<RecursivePathFindResult>();

    
    void PathFind_R( RecursivePathFindResult rpf )
    {
        //if(currDepth>= maxDepth) return new RecursivePathFindResult(){path = path};

        if (rpf.currDepth < rpf.maxDepth)
        {
            for (var i = 0; i < rpf.node.links.Count; i++)
            {
                var link = rpf.node.links[i];
                
                
                //if(rpf.path.Any(v=> v == link)) continue;
                for (int j = 0; j < rpf.path.Count; j++)
                {
                    if(rpf.path[j] == link) continue;
                }
                
                if (/*Vector3.Distance#1#(link.worldPos - endNode.worldPos).sqrMagnitude < 1f) //SUCCESS!!!!
                {
                    rpf.path.Add(link);
                    rpf.currState = RecursivePathFindResult.State.finished;
                    return;
                    //return rpf;
                }

                
                
                RecursivePathFindResult r = new RecursivePathFindResult();
                r.node = link;
                r.endNode = rpf.endNode;
                r.currDepth = rpf.currDepth+1;
                r.maxDepth = rpf.maxDepth;
                
                r.path = new List<Node>( rpf.path);
                r.path.Add(link);
                
                openPF_Branches.Add( r);
                //return PathFind_R(r);
            }
        }
        else
        {
            //rpf.currState = RecursivePathFindResult.State.failed;
            openPF_Branches.Remove(rpf);
            //removedBranches.Add(rpf);
        }
        openPF_Branches.Remove(rpf);
        //removedBranches.Add(rpf);

        //return rpf; //FINAL RESULT
    }*/
    

    #endregion
    
    
    

    private List<PathNode> openNodes = new List<PathNode> ( );
    private List<PathNode> closedNodes = new List<PathNode> ( );




    private List<PathNode> PathFindLoop ( )
    {
        if (startNode.hasPreinserted || endNode.hasPreinserted)
        {
            return new List<PathNode>(){ new PathNode(){node = /*startNode*/GetNodeByArrayId(Vector3Int.zero)}};
        }  
        
        //Init start values
        StartPathfind ( startNode, endNode );



        //Iterate
        //while ( closestNode != null && closestNode.dist > 0 )
        Vector3Int gridSizeStep = new Vector3Int( 
            Mathf.CeilToInt(gridSize.x/stepDist), 
            Mathf.CeilToInt(gridSize.y/stepDist), 
            Mathf.CeilToInt(gridSize.z/stepDist)
            );
        var maxClosedNodesCount = gridSizeStep.x * gridSizeStep.y * gridSizeStep.z;
        maxClosedNodesCount = (int)Mathf.Sqrt(maxClosedNodesCount) * 4;
        
        FindNextNodeToReachTarget(endNode);

        for (int i = 0; i < /*10000*/maxClosedNodesCount; i++)
        {
            if (closestNode != null && closestNode.dist > 1 && closedNodes.Count< 500)
            {
                FindNextNodeToReachTarget(endNode);
            }
            else
            {
                if(i>100) Debug.Log("PathFindLoop break " + i);
                //pathToTargetTemp = new List<PathNode>(){ new PathNode(){node = startNode}};
                break;
            }

            if (i == maxClosedNodesCount - 1) //если поиск пути не завершен -> вернуть пустой путь, иначе сетка запонится мусором из поиска
            {
                Debug.Log("PatFind Failed " + maxClosedNodesCount );
                return new List<PathNode>(){ new PathNode(){node = /*startNode*/GetNodeByArrayId(Vector3Int.zero)}};  
            }
            //yield return new WaitForSeconds ( 1 );
        }



        #region BuilLinks
        List<PathNode> pathToTargetTemp = new List<PathNode>(closedNodes);
        BuildPathLinks(pathToTargetTemp);

/*        //Link to start
        var firstPathNode = pathToTarget.FirstOrDefault(v => v.node == startNode);
        if (firstPathNode != null) pathToTargetTemp.First().AddLink(firstPathNode);

        //Link to last path Node
        var endPathNode = pathToTarget.FirstOrDefault(v => v.node == endNode);
        if (endPathNode != null) pathToTargetTemp.Last().AddLink(endPathNode);*/

        #endregion




        return pathToTargetTemp;

        //yield return null;
    }

    private static void BuildPathLinks(List<PathNode> pathToTargetTemp)
    {
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
    }


    //private bool pathSearching;
    public void StartPathfind ( Node start, Node end )
    {
/*        if ( !pathSearching )
        {*/
            //pathSearching = true;

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
            if(node.node.hasPreinserted) continue;
            
            
            var links = CalcNewPathNodes ( node.node.links.ToArray ( ), end, node.node );

            foreach ( PathNode pnLink in links )
            {
                var link = pnLink;
                if(link.node.hasPreinserted) continue;
                
                if ( openNodes.Count ( v => v.node == link.node ) == 0 && closedNodes.Count ( v => v.node == link.node ) == 0 )
                {
                    openNodes.Add ( pnLink );
                }
            }
        }
        



        //select best next node to tick
        closestNode = openNodes.Where(v=> v.node.hasPreinserted==false).OrderBy ( v => v.cost  ).FirstOrDefault ( );


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



    #region EditGrid_Drawing

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
            BuildPathPointsForSignatureCheck();
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

//[Serializable]
public class PathNode
{
    #region Vars

    public Node node;

    public float step = 0;
    public float dist = 0;
    public float cost = 0;

    public List<PathNode> links = new List<PathNode>();
    //public List<PathNode> pathLinks = new List<PathNode> ( );

    public bool IsVertical { get; set; }


    #endregion


    public void CalcDist ( Node target, Node currStepNode )
    {
        if (target==null || currStepNode==null)
        {
            cost = 1;
            return;
        }
    
        dist = Vector3.Distance(node.worldPos, target.worldPos);
        step = Vector3.Distance(node.worldPos, currStepNode.worldPos);

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


//[Serializable]
public class Node
{
    #region Vars


    public Vector3 vectorPos;
    public Vector3 worldPos;

    //[HideInInspector] 
    public List<Node> links = new List<Node>();
    public static List<Node> allMapNodesList = new List<Node>();


    public bool occupiedByTile = false;
    public bool hasPreinserted = false;
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
            generator.BuildPathPointsForSignatureCheck();
        }
        if ( GUILayout.Button ( "Past signatures" ) )
        {
            generator.ScanSigantures();
        }

        if ( GUILayout.Button ( "Clear" ) )
        {
            generator.CleanSpawned(true);
        }
        
        if ( GUILayout.Button ( "GetNodeByWorldPos" ) )
        {
            generator.GetNodeByWorldPosGizmo();
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


/*
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
*/



}
#endif

#endregion