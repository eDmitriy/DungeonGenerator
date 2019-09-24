using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProceduralToolkit;
using Sirenix.Utilities.Editor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Random = UnityEngine.Random;


#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class MazeGenerator : MonoBehaviour
{
    #region Vars

    public bool genOnStart = false;
    public bool connectOpenLinks = true;

    
    [Serializable]
    public class DrawPathLinksSettings
    {
       public bool draw = true;
       public bool drawPreinserted = false;
       public bool drawOccupied = false;
       public bool drawModifierVolumes = true;

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
       public Color modifierVolumesColor = Color.red;
       public Color modifierVolumesColor_exclude = Color.red;

       
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
    [HideInInspector]
    public Bounds gridBounds;
    public int stepDist = 20;

    [Header("Path Noise")] 
    public int pathNoiseCount = 10;


    #region Path Grow

    [Serializable]
    public class PathGrowSettings
    {
        public int branchesCount = 40;
        public int branchMaxPtsCount = 30;
        //public int pathGrow_upDownLinkRemoveChance = 90;
        public int pathGrow_sameDirectionMult = 5;
        public int pathGrow_verticalMult = 20;

        public float nextNodeSelection_onePlaneChance = 90;
        public float nextNodeSelection_useVectorSortingChance = 50;

        public float inserSignatureChance = 20;

        public int secondaryBranchLength = 10;

    }
    [Header("Path Grow")] 
    public PathGrowSettings pathGrowSettings = new PathGrowSettings();

    #endregion


    //public Transform startNodeTr, endNodeTr;
    Node startNode, endNode,  pathStartNode, pathEndNode;
    //public Transform startPoint;

    private List<Node> GrowPoints { get; set; } = new List<Node>();


    [Header("Signatures")]
    public bool loadReses = true;
    public string pathToSignatures = "MazeGeneratorSignatures";
    [HideInInspector] public List<TileSignature> signatures = new List<TileSignature> ( );


    
    private Dictionary<Vector3Int, Node> nodeDict = new Dictionary<Vector3Int, Node>();


    #region PathToTarget

    private List<PathNode> pathToTarget = new List<PathNode> ( );
    public List<PathNode> GetPathToTarget()
    {
        return pathToTarget;
    }

    #endregion
    


    #region SignatureInsertion


    [Serializable]
    public class SignatureInsertion
    {
        [Serializable]
        public class SignatureWithRatio
        {
            public TileSignature signature;
            public int ratio = 1;
        }
        [Serializable]
        public class SignaturePalette
        {
            public TileSignature signature;
            public string tag = "default";
        }
        public static class SignaturePaletteDrawer
        {
            public static string currDrawingTag = "default";
        }
        
        public List<TileSignature> SignatureToInsert { get; private set; }
        public SignatureWithRatio[] signatureToInsert_withRatio = new SignatureWithRatio[0];
        public SignaturePalette[] signaturesPalette = new SignaturePalette[0];


        public TileSignature startTile;
        
        //public int maxCount = 5;

        public void Init()
        {
            SignatureToInsert = new List<TileSignature>();

            foreach (var signatureWithRatio in signatureToInsert_withRatio)
            {
                for (int i = 0; i < signatureWithRatio.ratio; i++)
                {
                    SignatureToInsert.Add( signatureWithRatio.signature);
                }
            }
        }
    }
    [Header("PreInsert Big Signatures")]
    public SignatureInsertion signatureInsertion = new SignatureInsertion();

    #endregion


    #region SCENE_BUILDER

    //SCENE_BUILDER
    [Serializable]
    public class SceneBuilderTiles
    {
        public SceneBuilder sceneBuilder;


        public void Generate(SceneBuilder_Tile sbTile, MazeGenerator mg)
        {
            sceneBuilder.LastMazeGenerator = mg;
            if (sceneBuilder.TilesList.Count == 0)
            {
                GenerateInitialData(sbTile);
            }
            /*sceneBuilder.*/Generate(sbTile);
        }
        public void Generate( SceneBuilder_Tile sbTile)
        {
            SceneBuilder sb = sceneBuilder;
            if (sb != null)
            {
                //sb.currPlacedTilesCount = 0;
                sb.startTile = sbTile;
                sb.Generate(false); 
            }
        }
        
        public static bool CheckAndGrabSelection(out SceneBuilder_Tile sbTile, bool checkIfSpawned = true)
        {
            sbTile = null;
        
            if (Selection.objects.Length <= 0) return false;
            var so = Selection.objects[0] as GameObject;
            if (so == null) return false;
        
            sbTile = so.GetComponent<SceneBuilder_Tile>();
            if (sbTile == null) return false;
        
            bool sceneCheck = true;
            if (checkIfSpawned) sceneCheck = sbTile.gameObject.scene.IsValid();
        
            return (sbTile != null && sceneCheck);
        }


        public void GenerateInitialData(SceneBuilder_Tile sbTile)
        {
            if (sceneBuilder != null)
            {
                sceneBuilder.startTile = sbTile;
                sceneBuilder.InitDataForGeneration();
            }
        }
        
    }
    
    [Header("SCENE BUILDER")]
    public SceneBuilderTiles sceneBuilder = new SceneBuilderTiles();

    #endregion
    
    
    #endregion


    #region Mono

    
    #region OnEnableDisable



    void OnEnable()
    {
        #region editor

#if UNITY_EDITOR
        // Remove delegate listener if it has previously
        // been assigned.
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
        // Add (or re-add) the delegate.
        SceneView.onSceneGUIDelegate += this.OnSceneGUI;
        //EditorApplication.update += Update;

#endif

        #endregion
    }



    void OnDisable ()
    {
        #region Editor

#if UNITY_EDITOR
        //EditorApplication.update -= Update;
        //EditorApplication.playModeStateChanged -= PlayModeChanged;

        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;

#endif

        #endregion
    }

    #endregion
    
    
    // Use this for initialization
    private void Start()
    {
        //if (planeCollider == null) planeCollider = GetComponent<BoxCollider>();
        if(Application.isPlaying) return;

        if (genOnStart)
        {
            BuildPathPointsForSignatureCheck ( );
            ScanSignatures ( );
        }
        var drS = drawPathLinksSettings;
        drS.MaxFloor = (int)(gridSize.y / stepDist);
        SetFloor(-1);
    }


    public void Update()
    {
        gridBounds = new Bounds(transform.position, gridSize);
        
        if(Application.isPlaying) return;
        
        DrawPathLinks();
    }



    private void OnDrawGizmos()
    {
        if(drawPathLinksSettings.drawModifierVolumes==false) return;
        
        Gizmos.DrawWireCube(transform.position, gridSize);
        Gizmos.DrawWireCube(transform.position, new Vector3(gridSize.x, 0.1f, gridSize.z));
        //Gizmos.DrawWireCube(transform.position, Vector3.one*stepDist);



        #region Points Selector

        Gizmos.color = Color.red;
        Node n = null;
        if (NodeSelector.pathNodeSelectionList.Count >0)
        {
            n = NodeSelector.pathNodeSelectionList[0];
            if (n != null) Gizmos.DrawWireCube(n.worldPos, Vector3.one * stepDist);
        }

        if (NodeSelector.pathNodeSelectionList.Count > 1)
        {
            n = NodeSelector.pathNodeSelectionList[1];
            if (n != null) Gizmos.DrawCube(n.worldPos, Vector3.one * stepDist);
        }

        #endregion
    }

    


    
    public void OnSceneGUI(SceneView sceneView)
    {
        Handles.BeginGUI ( );

        
        NodeSelector.NodeSelectionViaMouse( pathToTarget, this );

        

        
        
        #region Sliders

        GUILayout.BeginArea ( new Rect(5,5, 150, 300));
        GUILayout.BeginVertical();

        GUILayout.Box("SignSpawn% " + pathGrowSettings.inserSignatureChance );
        pathGrowSettings.inserSignatureChance = GUILayout.HorizontalSlider(pathGrowSettings.inserSignatureChance,  0, 100);
        
        GUILayout.Box("BranchMaxPtsCount " + pathGrowSettings.branchMaxPtsCount );
        pathGrowSettings.branchMaxPtsCount = (int)GUILayout.HorizontalSlider(pathGrowSettings.branchMaxPtsCount,  1, 200);

        GUILayout.Box("MaxBranches " + pathGrowSettings.branchesCount );
        pathGrowSettings.branchesCount = (int)GUILayout.HorizontalSlider(pathGrowSettings.branchesCount,  1, 200);

        GUILayout.EndVertical();
        GUILayout.EndArea();
        
        
        
        GUILayout.BeginArea ( new Rect(165,5, 150, 300));
        GUILayout.BeginVertical();

/*        GUILayout.Box("SignSpawn% " + pathGrowSettings.inserSignatureChance );
        pathGrowSettings.inserSignatureChance = GUILayout.HorizontalSlider(pathGrowSettings.inserSignatureChance,  0, 100);
        
        GUILayout.Box("BranchMaxPtsCount " + pathGrowSettings.pathGrowMaxCount );
        pathGrowSettings.pathGrowMaxCount = (int)GUILayout.HorizontalSlider(pathGrowSettings.pathGrowMaxCount,  1, 100);*/

        
        string nodeSelectorpointsHeightsStr = " --- ";
        if (NodeSelector.pathNodeSelectionList.Count > 0)
        {
            int p1Floor = Mathf.CeilToInt(NodeSelector.pathNodeSelectionList[0].worldPos.y) / stepDist;
            nodeSelectorpointsHeightsStr += "P1: " + p1Floor + "  |Selector: " + NodeSelector.currFloorY;
        }
        GUILayout.Box("Floor " + NodeSelector.Floor(this)/*drawPathLinksSettings.Floor */+  nodeSelectorpointsHeightsStr);
        drawPathLinksSettings.Floor  = (int)GUILayout.HorizontalSlider(drawPathLinksSettings.Floor ,  -1, drawPathLinksSettings.maxFloor);

        GUILayout.EndVertical();
        GUILayout.EndArea();

        #endregion


        #region Gen Buttons

        GUILayout.BeginArea ( new Rect(5,150, 60, 300));
        GUILayout.BeginVertical();
        
        #region GEnerationButtons

        if ( pathToTarget.Count ==0 &&  GUILayout.Button("Start\nGener"))
        {
            BuildPathPointsForSignatureCheck();
        }

        if (pathToTarget.Count > 0)
        {
            GUI.backgroundColor = Color.green;
        } 
        if (pathToTarget.Count>0 && GUILayout.Button("Continue\nGener"/*"⇅"*/))
        {
            BuildPathPointsForSignatureCheck(false);
        }
        GUI.backgroundColor = Color.white; 


        if (GUILayout.Button("FixPath"))
        {
            ConnectSeparatedPathParts();
        }
        if (GUILayout.Button("ClsOpen"))
        {
            CloseOpenPathEnds();
        }
        #endregion
        

        

        GUILayout.Space(10);
        
        if (GUILayout.Button("Sign"))
        {
            ConnectSeparatedPathParts();
            ScanSignatures();
        }
        if (GUILayout.Button("Clear"))
        {
            CleanSpawned();
            sceneBuilder.GenerateInitialData(null);
            NodeSelector.RepaintSceneView();
            DrawPathLinks();
        }  
        if (GUILayout.Button("DelMesh"))
        {
            DestroySpawnedMeshes();
        }  
        
        GUILayout.Space(10);


        if (InsertSignatureFromEditorSelection_Check(out var selectedSign) )
        {
            #region snap selected tile

            if (Event.current.shift && NodeSelector.pathNodeSelectionList.Count>1)
            {
                Transform lastSpawnedSignatureTr = selectedSign.transform;
                Vector3 pos = lastSpawnedSignatureTr.position;
        
                lastSpawnedSignatureTr.position = new Vector3( 
                                                      Mathf.RoundToInt(pos.x/stepDist)*stepDist,
                                                      NodeSelector.pathNodeSelectionList[1].worldPos.y - stepDist/2,
                                                      Mathf.RoundToInt(pos.z/stepDist)*stepDist
                                                  ) + Vector3.one*stepDist/2;
            }

            #endregion
            

            if (  GUILayout.Button("Insert \nSelected"))
            {
                InsertSignature_FromSceneObject(selectedSign);
                
                Selection.objects = new UnityEngine.Object[0];
            }  
        }
        
        
        GUILayout.Space(10);

        if (SceneBuilderTiles.CheckAndGrabSelection(out var sbTile, true) )
        {
            if ( GUILayout.Button("Scene\nBuilder") )
            {
/*                sceneBuilder.sceneBuilder.LastMazeGenerator = this;
                if (sceneBuilder.sceneBuilder.TilesList.Count == 0)
                {
                    sceneBuilder.GenerateInitialData(sbTile);
                }
                sceneBuilder.Generate(sbTile);*/
                sceneBuilder.Generate(sbTile, this);
                Selection.objects = new UnityEngine.Object[0];
            }  
        }


        
        GUILayout.EndVertical();
        GUILayout.EndArea();

        #endregion



        #region Signatures Palette
        GUILayout.BeginArea ( new Rect(Camera.current.pixelRect.width - 200,110, 200, 1000));
        GUILayout.BeginVertical();



        #region Signatures palette

        if (InsertSignatureFromEditorSelection_Check(out selectedSign, false) && NodeSelector.pathNodeSelectionList.Count>1)
        {
            if (GUILayout.Button("InsrtSignFromEdtrSelect"))
            {
                InsertSignature(selectedSign, NodeSelector.pathNodeSelectionList[1].worldPos, 
                    false,true, false);
                DrawPathLinks();
            }
            if (GUILayout.Button("InsrtPrefabFromEditorSelect"))
            {
                TileSignature spwnedTileSignature = PrefabUtility.InstantiatePrefab(selectedSign, transform) as TileSignature;
                if (spwnedTileSignature != null)
                {
                    spwnedTileSignature.transform.position = NodeSelector.pathNodeSelectionList[1].worldPos;
                    Selection.objects = new UnityEngine.Object[] {spwnedTileSignature.gameObject};
                }
            }
            GUILayout.Space(10);
        }



        
        var signaturesPaletteTagList = signatureInsertion.signaturesPalette.Select(v=>v.tag).Distinct().ToList();
        
        foreach (var sTag in signaturesPaletteTagList)
        {
            GUI.color = Color.yellow;
            GUILayout.BeginHorizontal();
            GUILayout.Label("_____");
            if (GUILayout.Button(sTag))
            {
                SignatureInsertion.SignaturePaletteDrawer.currDrawingTag = SignatureInsertion.SignaturePaletteDrawer.currDrawingTag == sTag ?  " " : sTag;
            }
            GUILayout.EndHorizontal();
            if(SignatureInsertion.SignaturePaletteDrawer.currDrawingTag != sTag) continue;
            
            
            GUI.color = Color.white;
            foreach (var sign in signatureInsertion.signaturesPalette.Where(v=>v.tag==sTag))
            {
                if (GUILayout.Button(sign.signature.name))
                {
                    if (NodeSelector.pathNodeSelectionList.Count > 1)
                    {
                        InsertSignature(sign.signature, NodeSelector.pathNodeSelectionList[1].worldPos, 
                            false,true, false);
                        DrawPathLinks();
                    }
                }
            }
        }
        GUI.color = Color.white;

        #endregion


        
        GUILayout.EndVertical();
        GUILayout.EndArea();

        #endregion
        
        

        Handles.EndGUI ( );
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
        
        if ( !drS.draw || pathToTarget == null || pathToTarget.Count==0) return;

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


        if (drS.drawPreinserted || drS.drawOccupied)
        {
            foreach (var n in nodeDict.Values)
            {
                if (n.hasPreinserted || n.occupiedByTile)
                {
                    Color color = n.occupiedByTile && drS.drawOccupied ? Color.red : Color.blue;

                    Debug.DrawRay (
                        n.worldPos,
                        Vector3.up*3, 
                        color
                    );
                }
            }
        }

        
        foreach ( var pathNode in pathToTarget)
        {
/*            Debug.DrawRay(
                pathNode.node.worldPos ,
                    Quaternion.Euler(0, Random.Range(0,350), 0) * Vector3.one*50
            );*/
            if (pathNode == null || pathNode.node == null) continue;
            

            Color color = pathNode.node.hasPreinserted ? Color.blue : new Color(1,1,0,0.5f);


            #region CAmeraUpVector

            float shift = drS.linkShift;//0.1f;
            Vector3 camUp = Vector3.one;
            if (Camera.current != null)
            {
                camUp = Camera.current.transform.up;
            }

            #endregion
            
            
            foreach ( var link in pathNode.links )
            {
                if(link==null || link.node==null) continue;

                color = drS.defaultLinksColor;//new Color(1,1,0,0.05f);

                
                if(drS.onlyUpDown && Vector3.Angle( link.node.worldPos - pathNode.node.worldPos, Vector3.up) > 11 ) continue;
                int floorDiff = (drS.Floor - (int)pathNode.node.vectorPos.y);
                float drawShift_floorMult = 1;
                if(drS.Floor>-1 && floorDiff>-1 && floorDiff < drS.floorRange)
                {
/*                    float lerp = Mathf.Lerp(0f, 1f, ((float)drS.floorRange-floorDiff)/(float)drS.floorRange);
                    lerp = Mathf.Pow(lerp, drS.floorRangePow);
                    color = Color.Lerp(color, drS.floorColor, lerp); //new Color(1,1,0.5f,/*0.99f#1# lerp);*/
                    drawShift_floorMult = 5;
                    color = drS.floorColor;
                }

                if (drS.Floor < 0)
                {
                    color.a = 0.5f;
                }


                if (pathNode.links.Count<2 /*&& pathToTarget.Any(v=> v!= pathNode && v.node==pathNode.node)==false*/ )
                {
                    color = Color.red;
                }
  
                Debug.DrawLine (
                    pathNode.node.worldPos + camUp * Random.Range ( -shift*drawShift_floorMult, shift*drawShift_floorMult ),
                    link.node.worldPos + camUp * Random.Range ( -shift*drawShift_floorMult, shift*drawShift_floorMult ),
                    color, 0, false
                );
            }
        }
    }



    
    #endregion




    #region MazeGen

    public void BuildPathPointsForSignatureCheck( )
    {
        BuildPathPointsForSignatureCheck(Event.current.shift == false || pathToTarget.Count == 0 ||
                                         nodeDict.Values.Count == 0);
    }
    public void BuildPathPointsForSignatureCheck(bool initialGen)
    {
        DateTime startTime = DateTime.Now;

       
        if ( initialGen  )
        {
           
            DateTime gridGenStartTime = DateTime.Now;

            signatureInsertion.Init();
        
            CleanSpawned(true);
            GenerateGrid ( );

            sceneBuilder.GenerateInitialData(null);

            Debug.Log("GridGen time: " + (DateTime.Now - gridGenStartTime).TotalSeconds );
            #region SIGNATURE INSERTION

            //var hG = gridSize.z / stepDist / 2f;
            var insertSignatureStartShift = Vector3.zero;//new Vector3( 0,0, Random.Range( -hG,  hG ));
            InsertSignature(signatureInsertion.startTile, insertSignatureStartShift, false, true, false );

            if (GrowPoints.Count > 0) endNode = GrowPoints[0];


            TileSignature[] tileSignatures = FindObjectsOfType<TileSignature>();
            foreach (var ts in tileSignatures)
            {
                InsertSignature_FromSceneObject(ts);
            }

            #endregion
        }


/*        SetStartEndPoints ( transform.position );
        var pathFindLoopResult = PathFindLoop ( );
        if (pathFindLoopResult != null) pathToTarget.AddRange(pathFindLoopResult);*/


        GrowBranches();
        PathNoise ( );
        if(connectOpenLinks) CloseOpenPathEnds();
        
        //ConnectSeparatedPathParts();


        DrawPathLinks();
        Debug.Log("GenerationFinished. Time: " + (DateTime.Now - startTime).TotalSeconds);
    }

    
    
    private void ConnectSeparatedPathParts()
    {
        var linksTotalList = pathToTarget
            .SelectMany(v=> v.links)
            .Where(v=> pathToTarget.Contains(v) == false )
            .ToList();
        foreach (var lPN in linksTotalList)
        {
            var pathNodes = pathToTarget.Where(v=> v.links.Any(b=>b.node == lPN.node)).ToList();

            pathToTarget.Add(new PathNode(){node = lPN.node, links = pathNodes});
        }
        
        

        for (var i = 0; i < pathToTarget.Count; i++)
        {
            var pp = pathToTarget[i];
            var duplicatePathNodes = pathToTarget.Where(v => v.node == pp.node && v != pp).ToList();


            foreach (var duplicatePathNode in duplicatePathNodes)
            {
                if (duplicatePathNode == null) continue;

                
                foreach (var ppLink in pp.links)
                {
                    //if (ppLink.node.hasPreinserted == false) continue;

                    if (duplicatePathNode.links.Any(v => v.node == ppLink.node)) continue;
                    if (Vector3.Distance(ppLink.node.vectorPos, duplicatePathNode.node.vectorPos) > 1.1f) continue;

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
            

            //only links with distance of 1
            pp.links = pp.links.Where(v =>
                {
                    if (Vector3.Distance(pp.node.vectorPos, v.node.vectorPos) < 1.1f)
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




    #region InsertSignatures

    private bool InsertSignature(TileSignature signatureToInsert, Vector3 insertSignatureStartShift,
        bool checkModifierVolume, bool applyStartLinkShift,
        bool setPathNodesOccupied)
    {
        return InsertSignature(signatureToInsert, insertSignatureStartShift, checkModifierVolume, applyStartLinkShift,
            setPathNodesOccupied, new Vector3(10, 1, 1));
    }
    
    private bool InsertSignature(TileSignature signatureToInsert, Vector3 insertSignatureStartShift, bool checkModifierVolume, bool applyStartLinkShift, 
        bool setPathNodesOccupied, Vector3 customRot)
    {
        PathNode prevNode = null;
        
        var sceneBuilderTile = signatureToInsert.GetComponent<SceneBuilder_Tile>();
        if (setPathNodesOccupied) sceneBuilderTile = null;
        if (sceneBuilderTile != null)
        {
            setPathNodesOccupied = true;
        }

        
        if (signatureToInsert != null)
        {
            List<int> randRotIntsList = new List<int>(){0,1,2,3};
            randRotIntsList.Shuffle();

            for (int r = 0; r < 4; r++)
            {
                #region prepare data

                List<Vector3> signatureVectorWithLinks = new List<Vector3>(signatureToInsert.signatureVector);
                signatureVectorWithLinks.Insert(0, signatureToInsert.signatureLinks[0]);
                if(signatureToInsert.signatureLinks.Count>1) signatureVectorWithLinks.Insert(signatureVectorWithLinks.Count, signatureToInsert.signatureLinks.Last());
                
                
                float randRotV = /*(float)Random.Range(0, 4)*/randRotIntsList[r] * 90f;
                var randRotQ = Quaternion.Euler(0, randRotV, 0);
                if (customRot.x<10) randRotQ = Quaternion.Euler(customRot);
                
                Vector3 startLinkShift = Vector3.zero;
                if(applyStartLinkShift) startLinkShift = randRotQ * -signatureToInsert.signatureLinks[0];
                
                List<int> notPreinsertedNodeIndexesList = new List<int>();

                #endregion
                
    
                //prepare signatures
                for (var i = 0; i < signatureVectorWithLinks.Count; i++)
                {
                    var v3 = signatureVectorWithLinks[i];
                    if (signatureToInsert.signatureVector_setNotPreinserted.Any(v => Vector3.Distance(v, v3) < 1))
                    {
                        notPreinsertedNodeIndexesList.Add(i);
                    }
                    
                    v3 = randRotQ * v3;
                    Vector3 wPos = transform.position + insertSignatureStartShift + ((v3 + startLinkShift) * stepDist); 
    
                    signatureVectorWithLinks[i] = wPos/*v3*/;
                }
    
                List<Node> nodeList = new List<Node>();

                
                //build node list and check
                bool mustTryNext = false;
                for (var i = 0; i < signatureVectorWithLinks.Count; i++)
                {
                    Vector3 v3 = signatureVectorWithLinks[i];
                    //Debug.DrawRay(v3, Vector3.up*10, Color.magenta, 10);
    
                    Node node = GetNodeByWorldPos( /*wPos*/ v3);
                    
                    
                    bool modifierVolumeCheckResult = false;
                    if(checkModifierVolume && node!=null) modifierVolumeCheckResult = !MazeGenerator_BoundModifier.IsIncludeBoundsContains(node.worldPos);
                    
                    if (node == null || node.hasPreinserted  || (i>2 && pathToTarget.Any(v=> v.node==node)) /*|| modifierVolumeCheckResult*/ )
                    {
                        //print("TryInsertSignature_NodeAlreadyPreinserted");
                        //return false;
                        mustTryNext = true;
                        
                        //if (node != null) Debug.DrawRay(node.worldPos, Vector3.up * 20, Color.red, 10);
                        
                        break;
                    }
                    
                    nodeList.Add(node);
                }
                if(mustTryNext) continue;
    
    
                //build path from signature points
                int preinsertCutRange = signatureToInsert.preinsertCutRange;
                PathNode pathNode = null;
                for (var i = 0; i < nodeList.Count; i++)
                {
                    //Debug.DrawRay(wPos, Vector3.up * 50, Color.magenta, 20);
                    Node node = nodeList[i];
                    
                    //POINTS OF INTERESTS from links
                    if (i >= preinsertCutRange && i <= nodeList.Count - (preinsertCutRange+1) && !notPreinsertedNodeIndexesList.Contains(i) )
                    {
                        node.hasPreinserted = true;
                        if (setPathNodesOccupied) node.occupiedByTile = true;
                    }
    
    
                    pathNode = new PathNode() {node = node, step = stepDist};
                    if (prevNode != null) pathNode.AddLink(prevNode);
    
                    prevNode = pathNode;
    
                    pathToTarget.Add(pathNode);
                }



                #region Add grow points for every signature link/interest point

                foreach (Vector3 v3 in signatureToInsert.pointsOfInterest)
                {
                    Vector3 wPos = transform.position + insertSignatureStartShift + ((randRotQ * v3 + startLinkShift) * stepDist);
    
                    Node interestNode = GetNodeByWorldPos(wPos);
                    if (interestNode != null) GrowPoints.Add(interestNode);
                }

                
                if (signatureToInsert.signatureLinks.Count > 1) //dont grow from signature end if this signature has no exits
                {
                    GrowPoints.Add(nodeList.Last());
                }

                #endregion
                //Debug.DrawRay(transform.position + insertSignatureStartShift, Vector3.up*10, Color.yellow, 20);

                //set node selection to signature exit link, so we can add signatures chain
                if (pathNode != null) NodeSelector.AddNodeToList(pathNode);



                #region SceneBuilder_Tile

                if (sceneBuilderTile != null)
                {
                    //SPawn tile Prefab
                    SceneBuilder_Tile spawnedSBTile = PrefabUtility.InstantiatePrefab(sceneBuilderTile, transform) as SceneBuilder_Tile;
                    if (spawnedSBTile != null)
                    {
                        spawnedSBTile.transform.position = nodeList[1].worldPos;
                        spawnedSBTile.transform.rotation = randRotQ;
                        
                        //run scene builder with start point at spawned prefab
/*                        List<TileConnection> sceneBuilderOpenConnections = new List<TileConnection>(sceneBuilder.sceneBuilder.openConnections);
                        sceneBuilder.sceneBuilder.openConnections.Clear();*/

                        foreach (Transform childTr in sceneBuilder.sceneBuilder.transform)
                        {
                            childTr.SetParent(transform);
                        }
                        sceneBuilder.Generate(spawnedSBTile, this);
                    }
                    

                }

                #endregion
                
                //SUCCESSFULLY INSERTED
                return true;
            }
        }

        //FAILED
        return false;
    }
    
    
    
    public bool InsertSignature_FromSceneObject(TileSignature tileSignature)
    {
        if (tileSignature == null || tileSignature.gameObject.scene.IsValid()==false) return false;
        
        Transform selectedTr = tileSignature.transform;
        Quaternion signRot = selectedTr.rotation;
        selectedTr.rotation = Quaternion.identity;
        
        bool result = InsertSignature(
            tileSignature, selectedTr.position, false, 
            false, true, signRot.eulerAngles
        );
        
        selectedTr.rotation = signRot;

        
        DrawPathLinks();

        return result;
    }
    private bool InsertSignatureFromEditorSelection_Check(out TileSignature tileSignature, bool checkIfSpawned = true)
    {
        tileSignature = null;
        
        if (Selection.objects.Length <= 0) return false;
        var so = Selection.objects[0] as GameObject;
        if (so == null) return false;
        
        tileSignature = so.GetComponent<TileSignature>();
        if (tileSignature == null) return false;
        
        bool sceneCheck = true;
        if (checkIfSpawned) sceneCheck = tileSignature.gameObject.scene.IsValid();
        
        return (tileSignature != null && sceneCheck);
    }

    #endregion
    
    
    #endregion
    
    
    
    
    #region Path Grow

    void GrowBranches()
    {
        if(pathToTarget.Count==0) return;
        
        List<int> branchesStartFloors = new List<int>();
        List<Vector3> verticalLinksVectorsPosList = new List<Vector3>();
        

        for (int i = 0; i < pathGrowSettings.branchesCount; i++)
        {
            if (PathGrow_GetNodeToGrow(branchesStartFloors, out var pathNode)) break;
            
            var useVectorSorting = Random.Range(0f, 100f) < pathGrowSettings.nextNodeSelection_useVectorSortingChance;
            var onePlane = Random.Range(0f, 100f) < pathGrowSettings.nextNodeSelection_onePlaneChance;
            var verticalLinks = Random.Range(0f, 100f) < 20;

            Node prevNode = null;
            
            GrowBranch(pathGrowSettings, pathNode, pathGrowSettings.branchMaxPtsCount, pathGrowSettings.inserSignatureChance, false,
                onePlane, verticalLinksVectorsPosList, useVectorSorting, verticalLinks, branchesStartFloors);
        }

    }

    
    
    private void GrowBranch(PathGrowSettings gs, PathNode pathNode, int growCount, float insertSignatureChance, bool isRecursion, bool onePlane,
        List<Vector3> verticalLinksVectorsPosList, bool useVectorSorting, bool verticalLinks, List<int> branchesStartFloors)
    {
        var branchNodes = new List<Node>(pathToTarget.Select(v=>v.node) );
        Vector3 branchStartWPoint = pathNode.node.worldPos;
        
//GROWING fron selected path node
        for (int j = 0; j < growCount; j++)
        {
            if (pathNode == null) break;

            #region Get Next Node from Links

            bool isAnyIncludeVolumeExists = MazeGenerator_BoundModifier.IsAnyIncludeVolumeExists();
            var links = pathNode.node.links
                .Where( v =>
                {
                    bool b = v.hasPreinserted == false && branchNodes.Contains(v) == false;
                    if (b == false) return false;

                    //b = MazeGenerator_BoundModifier.IsExcludeBoundsContains(v.worldPos) == false;
                    
                    if (/*b == true &&*/ isRecursion==false && isAnyIncludeVolumeExists)
                    {
                        return MazeGenerator_BoundModifier.IsIncludeBoundsContains(v.worldPos);
                    }
                    return b;
                })
                .ToList();
            if (links.Count == 0) break;


            
            #region INSERT SIGNATURE


            bool includeVolumes = true;
            if (MazeGenerator_BoundModifier.IsAnyIncludeVolumeExists())
            {
                includeVolumes = MazeGenerator_BoundModifier.IsIncludeBoundsContains(pathNode.node.worldPos);
            }

            if (isRecursion==false && /*nextNode*/pathNode.node.hasPreinserted == false && Random.Range(0f, 100f) < insertSignatureChance && includeVolumes)
            {
                #region Select Signature

                TileSignature signatureToInsert = signatureInsertion.SignatureToInsert.GetRandom();

                Vector3 insertSignatureStartShift = pathNode.node.worldPos;

                if (MazeGenerator_BoundModifier._allModifiers.Count > 0)
                {
                    var modifiers = MazeGenerator_BoundModifier.GetCollectionModifiersFromPos(insertSignatureStartShift);//  pathGrowSettings.boundModifiers_exclude.FirstOrDefault(v => v.CurrModifierType== MazeGenerator_BoundModifier.ModifierType.setCollection && v.Bounds.Contains(insertSignatureStartShift));
                    if (modifiers.Count > 0)
                    {
                        var modifier = modifiers.GetRandom();
                        if (modifier != null && modifier.SignatureInsertion.SignatureToInsert.Count > 0)
                        {
                            List<TileSignature> signList = modifier.SignatureInsertion.SignatureToInsert;
                            //if (Event.current.alt)
                            {
                                var signListVertical = signList.Where(v => v.isVerticalConnector == Event.current.alt).ToList();
                                if (signListVertical.Count > 0)
                                {
                                    signList = signListVertical;
                                }
                            }
                            signatureToInsert = signList.GetRandom();
                        }
                    }
                }

                #endregion
                
                

                if (InsertSignature( signatureToInsert, insertSignatureStartShift, true, true, false ))
                {
                    if (signatureToInsert.GetComponent<TileSignature>() == null && signatureToInsert.signatureLinks.Count>1)
                    {
                        pathNode = pathToTarget[pathToTarget.Count - 1];
                        branchStartWPoint = pathNode.node.worldPos;
                    }
                    else
                    {

                    }

                    //if block has many outer pathes => foreach grow shrot branch with simplified rules
                    if (isRecursion == false)
                    {
                        int pi_count = GrowPoints.Count;
                        for (int i = 0; i < pi_count ; i++)
                        {
                            Node node = GrowPoints[0];
                            GrowPoints.RemoveAt(0);

                            PathNode sign_pathNode = new PathNode(){node = node};

                            GrowBranch(pathGrowSettings, sign_pathNode, pathGrowSettings.secondaryBranchLength, 0, true,
                                true, verticalLinksVectorsPosList, true, false, branchesStartFloors);
                        } 
                    }


                    continue;
                }
            }

            #endregion

            
            
            #region Remove UpDownLinks

            //if (/*useVectorSorting && */onePlane )
            {
                if (links.Count > /*1*/0)
                {
                    var link = links.FirstOrDefault(v => (v.vectorPos.y - pathNode.node.vectorPos.y) > 0.1f);
                    if (link != null &&
                        (onePlane /*|| ExistingLinksIsNearToThis(verticalLinksVectorsPosList, link.vectorPos)*/ ))
                    {
                        links.Remove(link);
                    }
                }

                if (links.Count > /*1*/0)
                {
                    var link = links.FirstOrDefault(v => (v.vectorPos.y - pathNode.node.vectorPos.y) < -0.1f);
                    if (link != null &&
                        (onePlane /*|| ExistingLinksIsNearToThis(verticalLinksVectorsPosList, link.vectorPos)*/ ))
                    {
                        links.Remove(link);
                    }
                }
            }
            if (links.Count == 0) break;

            #endregion

            #region link with same direction

            if (useVectorSorting && /*!onePlane && */links.Count > 0 && pathNode.links.Count > 0)
            {
                Vector3 pathNodeVector = (pathNode.node.worldPos - branchStartWPoint/*pathNode.links[0].node.worldPos*/).normalized;

                var forwardLink = links.FirstOrDefault(v =>
                    Vector3.Angle(pathNodeVector, (v.worldPos - pathNode.node.worldPos).normalized) < 30);
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

            #endregion


            #region Add selected node to path and create links

            var nextPathNode = new PathNode() {node = nextNode };

            pathToTarget.Add(nextPathNode);
            pathNode.AddLink(nextPathNode);
            branchNodes.Add(nextNode);


            bool isVertical = Mathf.Abs(pathNode.node.vectorPos.y - nextNode.vectorPos.y) > 0.1f;
            if (isVertical)
            {
                verticalLinksVectorsPosList.Add(pathNode.node.vectorPos);
                verticalLinksVectorsPosList.Add(nextNode.vectorPos);
            }

            branchesStartFloors.Add((int) nextNode.vectorPos.y);
            


            pathNode = pathToTarget[pathToTarget.Count - 1];

            #endregion

            
            //break branch growth if outside of include volumes
            if (MazeGenerator_BoundModifier.IsAnyIncludeVolumeExists() && MazeGenerator_BoundModifier.IsIncludeBoundsContains(pathNode.node.worldPos)==false)
            {
                break;
            }
        }
        
        GrowPoints.Add(pathNode.node);
    }


    
    private int lastFloor;
    private int floorLockCounter;
    private bool PathGrow_GetNodeToGrow(List<int> branchesStartFloors, out PathNode pathNode )
    {
        #region Selecting pathNode to GROW

        pathNode = null;
      

        
        if (GrowPoints.Count > 0)
        {
/*            int randomIndex = Random.Range(0, PointsOfInterest.Count);
            pathNode = new PathNode() {node = PointsOfInterest[randomIndex]};
            PointsOfInterest.RemoveAt(randomIndex);*/

            var nodes = GrowPoints.Where(v =>
            {
                return MazeGenerator_BoundModifier.IsExcludeBoundsContains(v.worldPos) == false;
            }).ToList();

            var nodesIncludeTemp = nodes.Where(v => MazeGenerator_BoundModifier.IsIncludeBoundsContains(v.worldPos))
                .ToList();
            if (nodesIncludeTemp.Count > 0)
            {
                nodes = nodesIncludeTemp;
            }
            
            var node = nodes.GetRandom();
            GrowPoints.Remove(node);
            
            pathNode = new PathNode(){node = node};

            return false;
        }

        bool isAnyIncludeVolumeExists = MazeGenerator_BoundModifier.IsAnyIncludeVolumeExists();
        var pathNodes = pathToTarget
            .Where(v =>
            {
                bool b = v.node.hasPreinserted == false;

                if (b && isAnyIncludeVolumeExists)
                {
                    b = MazeGenerator_BoundModifier.IsIncludeBoundsContains(v.node.worldPos);
                }
                return b;
            }
/*&& v.node.links.Any(l=>l.hasPreinserted==false)*/).ToList();


        if (pathNodes.Count == 0)
        {
            //pathNode = pathToTarget[pathToTarget.Count-1];
            return true;
        }
        else
        {
/*            if (branchesStartFloors.Count > 0)
            {
                int floor = 0;

                if (floorLockCounter == 0)
                {
                    var ssT = branchesStartFloors
                        .GroupBy(v => v)
                        .Select(v => new Vector2Int(v.Key, v.Count()))
                        //.Where(v=>v[1] < 10)
                        .ToList();
                    if (ssT.Count > 0)
                    {
                        floor = ssT
                            .OrderBy(v => v[1]) 
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

                    if (floorLockCounter == 0 && Random.Range(0, 100) < 30)
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

            var nodesWithExclude = pathNodes.Where(v =>
            {
                return MazeGenerator_BoundModifier.IsExcludeBoundsContains(v.node.worldPos) == false;
            }).ToList();
            var nodesWithInclude = pathNodes.Where(v =>
            {
                return MazeGenerator_BoundModifier.IsIncludeBoundsContains(v.node.worldPos);
            }).ToList();

            if (nodesWithInclude.Count > 0)
            {
                pathNode = nodesWithInclude.GetRandom();
            }

            if (pathNode==null && nodesWithExclude.Count > 0)
            {
                pathNode = nodesWithExclude.GetRandom();
            }

            if (pathNode == null)
            {
                pathNode = pathNodes.GetRandom();
            }

            

        }


        #endregion

        return false;
    }

    #endregion

    
    
    #region PathGen_Utils


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
        //startPos.y = transform.position.y;
        Vector3 spawnShift = Vector3.zero;


        //nodeList = new List<Node> ( );
        nodeDict = new Dictionary<Vector3Int, Node>();
        pathToTarget = new List<PathNode> ( );
        GrowPoints = new List<Node> ( );



        Profiler.BeginSample("GRID");

        for ( int x = 0; x < gridStepsCountX; x++ )
        {
            for ( int z = 0; z < gridStepsCountZ; z++ )
            {
                for ( int y = gridStepsCountY/2-1; y < gridStepsCountY; y++ )
                {
                    spawnShift = new Vector3 ( x * stepDist, y * stepDist, z * stepDist ); //SHIFT POS

                    Node node = new Node
                    {
                        worldPos = startPos + spawnShift,
                        vectorPos = new Vector3(x, y, z)
                    }; 
                    
                    Profiler.BeginSample("GRID_DictAdd");

                    nodeDict.Add( new Vector3Int(x,y,z), node );
                    Profiler.EndSample();

                    var list = GetAllNodeConnections ( node );
                    for (var i = 0; i < list.Count; i++)
                    {
                        var nodeConnection = list[i];
                        nodeConnection.CreateLink(node);
                    }
                }
            }
        }

        
        Profiler.EndSample();
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
        DestroySpawnedMeshes();


        if (cleanLists)
        {

            //if (nodeList != null) nodeList.Clear();
            if (nodeDict != null) nodeDict.Clear();
            pathToTarget = new List<PathNode>();
            if (GrowPoints != null) GrowPoints.Clear();

            if (openNodes != null) openNodes.Clear();
            if (closedNodes != null) closedNodes.Clear();
        }
    }

    private void DestroySpawnedMeshes()
    {
        foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child != transform /*&& child != startPoint*/)
            {
                DestroyImmediate(child.gameObject);
            }
        }

        foreach (var node in nodeDict.Values)
        {
            node.occupiedByTile = false;
        }
    }


    #region World pos node test

    //public Transform wordPOsGizmoTr;
    
    [Serializable]
    public class WorldPosNodeDebug
    {
        public Vector3 wPOsNodePos;
        public List<Vector3> wPOsNodeLinks = new List<Vector3>();
    }
    [Header("World pos node test")]

    public List<WorldPosNodeDebug> worldPosNodeDebugList = new List<WorldPosNodeDebug>();
    public void GetNodeByWorldPosGizmo()
    {
        if(NodeSelector.pathNodeSelectionList.Count<2) return;
        
        worldPosNodeDebugList.Clear();
        var nodeByWorldPosList = pathToTarget
            .Where(v => Vector3.Distance(v.node.worldPos, NodeSelector.pathNodeSelectionList[1].worldPos)<stepDist)
            .ToList();

        worldPosNodeDebugList = nodeByWorldPosList.Select(v =>
        {
            //Debug.DrawRay(v.node.worldPos, Vector3.up*20, Color.magenta, 5);

            return new WorldPosNodeDebug()
                {wPOsNodePos = v.node.worldPos, wPOsNodeLinks = v.links.Select(n => n.node.worldPos).ToList()};
        }).ToList();

    }

    #endregion
    
    
    #endregion

    
    #region Build Various Pathes



    
    
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
                //pointsOfInterest.Add(pathToTarget.Last().node);
            }

        }
    }
    
    public void CloseOpenPathEnds()
    {
        int closedEndsCounter = 0;
        for (int i = 0; i < 400; i++)
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
                if (pathFindLoopResult != null && pathFindLoopResult.All(v=>v.node.hasPreinserted==false && v.links.All(l=> l.node.hasPreinserted==false)))
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
                        //pointsOfInterest.Add(pathToTarget.Last().node);
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

    [ContextMenu("ScanSignatures")]
    public void ScanSignatures()
    {
        //ScanPathForSignature();
        //StartCoroutine ( ScanPathForSignature ( ) );
        ScanPathForSignature();
    }
    
    
    private void ScanPathForSignature()
    {
        if( pathToTarget.Count<2) return;

        DateTime startTime = DateTime.Now;

        
                        
        if (/*signatures.Count == 0 || pathToSignaturesLast != pathToSignatures*/loadReses /*&& drawingGridBrushNow==false*/ )
        {
            signatures = Resources.LoadAll<TileSignature> ( pathToSignatures ).ToList ( );
        }
        //reset occupation flag for every node
/*        foreach (var node in nodeList)
        {
            node.occupiedByTile = false;
        }*/

        //clean signatures
/*        foreach ( var child in transform.GetComponentsInChildren<TileSignature> ( ) )
        {
            if ( child != null ) DestroyImmediate ( child.gameObject );
        }*/





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
                    if(currPathNode==null) continue;
                    
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
                    if (! ScanSignature(nodeChain, signature, false))
                    {
                        //Reverse
                        nodeChain.Reverse ( );
                        ScanSignature ( nodeChain, signature, true ); 
                    }
                }
                //yield return null;
            }
        }


        print("Not occupied nodes count = "+pathToTarget.Count(v=> v!=null && !v.node.occupiedByTile) );
        Debug.Log("Signatures checked and pasted for " + (DateTime.Now - startTime).TotalSeconds);
        //yield return null;
    }


    
    public class SignatureScanPoint
    {
        public Vector3 point;
        public bool setOccupiedByTile = true;
    }
    
    private bool ScanSignature ( List<PathNode> nodes, TileSignature signature, bool isReversed)
    {
     
        //add every link of nodeChain to nodes list
        List<PathNode> linksList = new List<PathNode>();
        linksList = nodes.SelectMany ( v => v.links ).Except ( nodes ).ToList ( );


        //Calc signature for nodes chain
        List<SignatureScanPoint> signaturePoints = new List<SignatureScanPoint> ( nodes.Count );
        List<int> notOccupiedIndexList = new List<int>();
        for (var i = 0; i < signature.signatureVector.Count; i++)
        {
            var v3 = signature.signatureVector[i];
            if (signature.pointsOfInterest.Any(v => Vector3.Distance(v, v3) < 1))
            {
                notOccupiedIndexList.Add(i);
            }
        }

        for ( int i = 0; i < nodes.Count; i++ )
        {
            var v3 = nodes[i].node.vectorPos - nodes[0].node.vectorPos;
            signaturePoints.Add( new SignatureScanPoint(){point = v3/*, setOccupiedByTile = setTileOccupied*/});
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
                signaturePoints[i].point = rot*signaturePoints[i].point;
            }
            for ( int i = 0; i < pointsLinksVectors.Count; i++ )
            {
                pointsLinksVectors[i] = rot * pointsLinksVectors[i];
            }

            //CHECK
            if ( IsMatchSignature ( signaturePoints, pointsLinksVectors, signature ) && linksList.Count>0)
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

                    if (nodeIndex>=0 && linksList.Count > nodeIndex && nodes.Count > 0)
                    {
                        rotDir = linksList[nodeIndex].node.worldPos - nodes[0].node.worldPos;
                    }

                    rotDir.y = 0;
                }

                #endregion



                #region SPAWN

#if UNITY_EDITOR
                Quaternion go2Rotation = Quaternion.LookRotation(rotDir) * Quaternion.Euler(signature.rotCorrection);

                var go2 = PrefabUtility.InstantiatePrefab ( signature.transform,
                    transform.gameObject.scene
                ) as Transform;

                if (go2 != null)
                {
                    go2.position = nodes[0].node.worldPos;
                    go2.rotation = go2Rotation;
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
                //if (isReversed) signaturePoints.Reverse();
                for (var i = 0; i < nodes.Count; i++)
                {
                    PathNode node = nodes[i];
                    //signaturePoints.FirstOrDefault(v=> Vector3.Distance(v.point, node.w))
                    node.node.occupiedByTile = !notOccupiedIndexList.Contains(i); //true;
                }

                #endregion

                return true;
            }

        }

        return false;
    }

    bool IsMatchSignature(/*List<Vector3>*/List<SignatureScanPoint> points, List<Vector3> pointsLinks, TileSignature signature)
    {
        int counter = 0;
        int linksMatchCounter = 0;

        //SIGNATURE CHECK
        for ( int i = 0; i < signature.signatureVector.Count; i++ )
        {
            if (Vector3.SqrMagnitude(signature.signatureVector[i] - points[i].point) < 0.1f) counter++;
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
        Profiler.BeginSample("GRID_Connections");
        
        List<Node> nodes = new List<Node> (6 );
        
        
        for (int i = 0; i < 6; i++)
        {
            var dir = Vector3.zero;
            dir[i/2] = i%2 == 0 ? 1 : -1;
            
            var wPos = node.vectorPos + dir ;
            var wPosInt = new Vector3Int( (int)wPos.x, (int)wPos.y, (int)wPos.z);
            var retNode = GetNodeByArrayId ( wPosInt  );
            
            //retNode = GetNodeByArrayId (node.vectorPos + dir );
            if ( retNode != null ) nodes.Add ( retNode );
        }
        
        Profiler.EndSample();

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
        var gridCenterCorrection = ( gridSize / 2f - new Vector3 ( stepDist / 2f, stepDist / 2f, stepDist / 2f ) );
        var localPos = transform.InverseTransformPoint ( pos );
        pos = ( localPos + gridCenterCorrection )/ stepDist;
        
        if (clamp)
        {
            pos = ClampPositionToGrid(pos);
        }
        
        return GetNodeByArrayId ( /*pos*/ new Vector3Int( Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y), Mathf.RoundToInt(pos.z)) );
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

    public void PathFindSelectionNodes()
    {
        if(NodeSelector.pathNodeSelectionList.Count<2) return;
        
        startNode = NodeSelector.pathNodeSelectionList[0];
        endNode = NodeSelector.pathNodeSelectionList[1];

        var path = PathFindLoop();
        
        pathToTarget.AddRange(path);
        //ConnectSeparatedPathParts();
        
        DrawPathLinks();
    }


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



    
    static class NodeSelector
    {
        public static Ray editorCameraPointerRay;
        public static List<Node> pathNodeSelectionList = new List<Node>();
        public static int maxStackCount = 2;
        public static int currFloorY = 0;
        
        public static void NodeSelectionViaMouse(List<PathNode> pathToTarget, MazeGenerator mazeGenerator)
        {
            var e = Event.current;
            //needRedrawPathLinks = false;
            if(pathToTarget==null || pathToTarget.Count==0 || e==null) return;



            if (e.isMouse && e.shift && (e.button==0 || e.button==1) )
            {
                if (e.button == 1 || e.OnMouseDown(0, true) )
                {
                    SelectNodeToQueue(pathToTarget, mazeGenerator);
                    if(mazeGenerator!=null) mazeGenerator.GetNodeByWorldPosGizmo();
                    
                    if (e.control )
                    {
                        mazeGenerator.PathFindSelectionNodes();
                    }
                }
                e.Use();
                e.OnMouseMoveDrag(true);
                //e.OnMouseDown(0, true);

                //ERASER
                if ( e.button==1 && pathNodeSelectionList.Count>1 && e.delta.sqrMagnitude > 2 && mazeGenerator.transform.childCount==0)
                {
                    PathNode selectedNode = pathToTarget.FirstOrDefault(v=>v.node==pathNodeSelectionList[0]);

                    if (selectedNode != null)
                    {
                        PathNode link = selectedNode.links.FirstOrDefault(v=>v.node == pathNodeSelectionList[1]);
                        if (link != null) selectedNode.RemoveLink(link);

                        pathToTarget.Remove(selectedNode);
                        
                        //REPAINT SCENE
                        RepaintSceneView();

                        mazeGenerator.DrawPathLinks();
                    }
                }
            }
            
        }

        public static void RepaintSceneView()
        {
            var mazeGeneratorBoundModifier = MazeGenerator_BoundModifier._allModifiers.FirstOrDefault();
            if (mazeGeneratorBoundModifier != null)
                mazeGeneratorBoundModifier.transform.position += Vector3.forward * 0.01f * Random.Range(-1f, 1f);
        }

        
        
        private static void SelectNodeToQueue(List<PathNode> pathToTarget, MazeGenerator mazeGenerator)
        {
            if(pathToTarget.Count==0 || Event.current==null ) return;

            #region Select
            
            editorCameraPointerRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            
            List<PathNode> pathNodesInAngle = new List<PathNode>();
            pathNodesInAngle = pathToTarget
                .Where(v => Angle(v.node) < 2).ToList();

            if (pathNodesInAngle.Count == 0)
            {
                var floor = Floor(mazeGenerator);
                floor = floor * mazeGenerator.stepDist;
                currFloorY = floor - mazeGenerator.stepDist/2;
                
                
                pathNodesInAngle = mazeGenerator.nodeDict.Values
                    .Where(v =>
                    {
                        return Mathf.Abs(v.worldPos.y  - currFloorY) < mazeGenerator.stepDist && v.worldPos.y < currFloorY+1
                               && Angle(v) < 5;
                    })
                    .Select(v => new PathNode() {node = v})
                    .ToList();
                
            }
            if (pathNodesInAngle.Count == 0) return;

            
            PathNode pathNode = pathNodesInAngle
                .OrderBy(v=> Angle(v.node) )
                .First();

            #endregion
            
            
            if (Event.current.button == 0)
            {
                if( pathNodeSelectionList.Contains(pathNode.node)) return;
            }
            else
            {
                if(pathNodeSelectionList.Count>1 && pathNode.node == pathNodeSelectionList[1]) return;
            }
            
            
            
            AddNodeToList(pathNode);
        }

        public static void AddNodeToList(PathNode pathNode)
        {
            pathNodeSelectionList.Add(pathNode.node);

            if (pathNodeSelectionList.Count > maxStackCount)
            {
                pathNodeSelectionList.RemoveAt(0 /*pathNodeSelectionStack.Count-1*/);
            }
        }

        public static int Floor(MazeGenerator mazeGenerator)
        {
            var halfGridHeight = Mathf.RoundToInt((mazeGenerator.gridSize.y / mazeGenerator.stepDist) / 2);
            int floor = mazeGenerator.drawPathLinksSettings.floor - halfGridHeight;
            return floor;
        }

        private static float Angle(Node v)
        {
            if (v == null) return 360;
            
            return Vector3.Angle(editorCameraPointerRay.direction,
                (v.worldPos - editorCameraPointerRay.origin).normalized);
        }
    }


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


    
/*    public class ForcedSignature
    {
        public TileSignature signature;
        public Quaternion rotation;
    }

    public ForcedSignature forcedSignature = null;*/

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




        if ( GUILayout.Button ( "Generate nodes (L_Shift to Clear)" ) )
        {
            generator.BuildPathPointsForSignatureCheck();
        }
        if ( GUILayout.Button ( "Past signatures" ) )
        {
            generator.ScanSignatures();
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