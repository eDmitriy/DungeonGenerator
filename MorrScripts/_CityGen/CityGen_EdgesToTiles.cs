using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProceduralToolkit;
//using UnityEngine.Profiling;
using Random = UnityEngine.Random;
using Vector2Int = UnityEngine.Vector2Int;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CityGen_EdgesToTiles : MonoBehaviour
{
//#if UNITY_EDITOR

    #region Variables

    private Mesh currMesh;
    private MeshFilter meshFilter;
    List<Edge> edges = new List<Edge>();

    public Vector3 scaleMult = Vector3.one;
    public Transform facingTarget;

    public List<float> floorHeights = new List<float>() {2, 5, 5, 5, 5, 2};
    //public int floorsCount = 4;
    public float tileWidth = 5f;
    //public float floorHeight = 0.5f;
    public float zShift = 0;

    public int tileCountToSwitchToFronSide = 3;
    public bool invertFacingNormal = false;
    public bool dontAllignYAxis = false;
    public bool useQuads = false;
    public bool generateLOD = false;


    //[Header("Plane mesh floor")]
    [HideInInspector]
    public bool generatePlaneMeshFromEdges = false;
    [HideInInspector]
    public float meshWidth = 10;
    [HideInInspector]
    public string floorMeshName = "Plane mesh floor";
    [HideInInspector]
    public Material meshFloorMat;
    [HideInInspector]
    public int uvStretch_counterTriggerVal = 5;
    [HideInInspector]
    public float uvStretch_x = 2;





    [Header("Prefabs")] 
    public string tilesCollectionName = "Building Parts 01";
    private List<CityGen_Tile> cityGenTiles = new List<CityGen_Tile> ( );
    static CityGen_Collections cityGenCollections;
    public static CityGen_Collections CityGenCollections
    {
        get
        {
            if( cityGenCollections == null )
            {
                cityGenCollections = Resources.LoadAll<CityGen_Collections>( "SB_Collections" ) [ 0 ];
            }
            return cityGenCollections;
        }

    }


    [Header("QuadGen")] 
    public float normalAngleTolerance = 15f;
    public bool skinDeform = false;
    public bool combineResults = true;
    public bool generateUV2 = false;



    #endregion



    #region GridGen

    public void Generate()
    {
        PrepareEdgesFromMesh(true);
        PlaceTiles();

        foreach (var pt in placeTileInfoList)
        {
            Transform inst = Instantiate(pt.Prefab, pt.position, pt.rotation/*, pt.parent*/);
            inst.SetParent(transform);
            inst.localScale = pt.scale;
        }

        AfterSpawnActions();
    }


    private void PrepareEdgesFromMesh(bool calcEdges)
    {
        if (meshFilter == null) meshFilter = this.GetComponent<MeshFilter>();
        if (meshFilter != null && currMesh == null) currMesh = meshFilter.sharedMesh;

        cityGenTiles.Clear();
        cityGenTiles.AddRange(CityGenCollections.GetCollection(tilesCollectionName));

        foreach (CityGen_Tile tile in cityGenTiles)
        {
            tile.bounds = tile.compoundBounds
                ? CompoundBounds(tile.gameObject)
                : tile.GetComponentInChildren<Renderer>().bounds;
        }

        //AdjustHeightToTerrain();

        edges.Clear();

        if( calcEdges ) CalcEdges();
    }

    private void AfterSpawnActions()
    {
        #region Spawners

        CityGen_TileSpawner[] spawners = GetComponentsInChildren<CityGen_TileSpawner>();
        for (int i = 0; i < spawners.Length; i++)
        {
            DestroyImmediate(spawners[i]);
        }

        #endregion

        if (generatePlaneMeshFromEdges) EdgesToPlaneMesh();


        if (combineResults)
        {
            List<GameObject> destrList = new List<GameObject>();

            string edgesParentName = "edgesParent";
            //Transform child = transform.GetComponentsInChildren<Transform>().FirstOrDefault(v => v.name == edgesParentName);
            //foreach ( Transform child in transform )
            {
                //if (child != null /*child.name == edgesParentName*/)
                {
                    MeshBatcher.Batch(transform);
                    if (transform != null)
                    {
                        List<Transform> gos = transform.GetComponentsInChildren<Transform>()
                            .Where(v => v.name.Contains("MeshLightmapIndex")).ToList();

                        foreach (var go in gos)
                        {
                            go.transform.SetParent(transform);
                            //destrList.Add(child.gameObject);
                        }
                    }

                    if (generateUV2)
                    {
#if UNITY_EDITOR

/*                        var mf = go.GetComponent<MeshFilter> ( );
                        if ( mf != null ) Unwrapping.GenerateSecondaryUVSet (
                            mf.sharedMesh, 
                            new UnwrapParam()
                            {
                                hardAngle = 89, 
                                areaError = 1f, 
                                angleError = 0f,
                                packMargin = 2
                            } 
                            );*/
#endif
                    }

                    //break;
                }
            }

/*            for (var i = 0; i < destrList.Count; i++)
            {
                GameObject d = destrList[i];
                if (Application.isPlaying)
                {
                    Destroy(d);
                }
                else
                {
                    DestroyImmediate(d);
                }
            }*/
        }
    }





    #region PlaceTiles

    public bool excludeHighEdges = true;
    public bool callTileSpawners = false;
    public int emptyChance = 0;
    public bool dontSpawnNearToSpawned = false;
    public float dontSpawnNearToSpawned_radius = 80;


    List<SceneObject> placeTileInfoList = new List<SceneObject>();


    private void PlaceTiles()
    {
        if(cityGenTiles.Count==0 || floorHeights.Count==0 || edges.Count==0) return;
        placeTileInfoList = new List<SceneObject>();

        #region Destory child edges

        string edgesParentName = "edgesParent";
        foreach (Transform child in transform)
        {
            //if (child.name == edgesParentName)
            if( child != transform )
            {
                if( Application.isPlaying )
                {
                    Destroy( child.gameObject );
                }
                else
                {
                    DestroyImmediate( child.gameObject );
                }
                //break;
            }
        }

        #endregion

        #region Create EdgeParent

        GameObject edgesParent = new GameObject(edgesParentName);
        edgesParent.transform.position = transform.position;
        edgesParent.transform.rotation = transform.rotation;
        edgesParent.transform.parent = transform;

        #endregion

        List<Vector3> spawnedPositions = new List<Vector3>();

        for (var index = 0; index < edges.Count; index++)
        {
            Edge edge = edges[index];
            if (excludeHighEdges && Mathf.Abs(edge.start.y - transform.position.y) > 1) continue;

            #region FloorHeights

            //GET FLOOR HEIGHTS FROM EDGE
            List<float> currFloorHeights = new List<float>();
            currFloorHeights = new List<float>(floorHeights);

            if (edge.height > 0)
            {
                currFloorHeights.Clear();
                currFloorHeights.Add(floorHeights.FirstOrDefault());

                float edgeLenght = edge.height - floorHeights.FirstOrDefault() - floorHeights.Last();

                for (int i = 1; i < (edgeLenght / 5) + 1; i++)
                {
                    currFloorHeights.Add(5);
                }

                currFloorHeights.Add(floorHeights.Last());
            }

            #endregion


            #region EdgeGO parent

            GameObject edgeGO = new GameObject("Edge " + index/*RandomE.femaleName + " - " + Random.Range(0, 99999)*/ );
            edgeGO.transform.position = edge.start;
            edgeGO.transform.rotation = edge.facingNormal;
            edgeGO.transform.SetParent(transform);

            #endregion

            int tilesCountWidth = (int) Mathf.Clamp(edge.length / tileWidth, 1, 1000);

            //MATRIX
            List<Vector2Int> tileMatrix = new List<Vector2Int>();

            float heightShift = 0;

            for (int floorN = 0; floorN < currFloorHeights.Count; floorN++) //for every floor
            {
                for (int columnN = 0; columnN < tilesCountWidth; columnN++) //every edge length segment
                {
                    if (IsTileMatrixContains(tileMatrix, columnN, floorN))
                        continue; //if this tile already mapped in matrix then skip it!
                    if (Random.Range(0, 100) < emptyChance) continue; //chance to skip


                    #region dontSpawnNearToSpawned

                    if (dontSpawnNearToSpawned)
                    {
                        Vector3 placePosPrediction = Vector3.Lerp(edge.start, edge.end,
                            (float) columnN / (float) tilesCountWidth);
                        if (spawnedPositions.Any(v =>
                            Vector3.Distance(v, placePosPrediction) < dontSpawnNearToSpawned_radius))
                        {
                            continue;
                        }
                    }

                    #endregion


                    #region Select Tiles

                    //get random tile from list with params
                    var columnN_1 = columnN;
                    var floorN_1 = floorN;
                    var tilesList = cityGenTiles.Where(
                        v =>
                            v.CheckColumn(columnN_1)
                            && v.CheckFloorN(floorN_1)
                            && v.CheckFacingType(edge.facingType)
                            && columnN_1 >= v.columnSize - 1 && columnN_1 + v.columnSize <= tilesCountWidth
                    );

                    #endregion

                    #region CornerFilters

                    //CORNER FILTER
                    if ((columnN == (tilesCountWidth - 1) || columnN == 0))
                    {
                        tilesList = tilesList.Where(v => v.canBeCornerElement);
                    }

                    //LAST FLOOR
                    if (floorN == currFloorHeights.Count - 1)
                    {
                        tilesList = tilesList.Where(v => v.canBeLastFloor);
                    }

                    //PRE-LAST FLOOR
                    if (floorN == currFloorHeights.Count - 2)
                    {
                        tilesList = tilesList.Where(v => v.canBePreLastFloor);
                    }

                    #endregion

                    //SPAWN
                    if (tilesList.Count() > 0)
                    {
                        #region FilterTiles

                        int maxPriority = tilesList.Max(v => v.priority);
                        tilesList = tilesList.Where(v => v.priority == maxPriority);

                        var currTile = tilesList.ToList().GetRandom(); /*new CityGen_Tile();*/

                        //if no tile in list with params then create white dummy
                        if (currTile == null)
                        {
/*                            currTile = GameObject.CreatePrimitive(PrimitiveType.Cube).AddComponent<CityGen_Tile>();
                            currTile.bounds = CompoundBounds(currTile.gameObject);*/
                            continue;
                        }

                        #endregion

                        #region ScaleCorrection

                        #region matrix

                        for (int y = 0; y < currTile.rawSize; y++)
                        {
                            for (int x = 0; x < currTile.columnSize; x++)
                            {
                                if (IsTileMatrixContains(tileMatrix, columnN + x, floorN + y) == false)
                                {
                                    tileMatrix.Add(new Vector2Int(columnN + x, floorN + y));
                                }
                            }
                        }

                        /*                        for (int i = 0; i < currTile.columnSize_matrixOverride; i++)
                                                {
                                                    if( IsTileMatrixContains( tileMatrix, columnN + i, floorN  ) == false )
                                                    {
                                                        tileMatrix.Add( new Vector2Int( columnN + i, floorN  ) );
                                                    }
                                                }*/

                        #endregion


                        float compoundRawHeight = 0;
                        for (int j = 0; j < currTile.rawSize; j++)
                        {
                            //FIX HEIGHT FOR TILE
                            if ((floorN + j) >= 0 && currFloorHeights.Count > floorN + j)
                            {
                                compoundRawHeight += currFloorHeights[floorN + j];
                            }
                            else
                            {
                                compoundRawHeight += currFloorHeights[0];
                            }

                            //if(edge.height>0)compoundRawHeight = edge.height;
                        }
                        /*                        float compoundRawWidth = 0;
                                                for( int j = 0; j < currTile.columnSize; j++ )
                                                {

                                                }*/


                        //scale correction

                        Vector3 currTileSize = currTile.bounds.size;
                        float widthCorrectionMult =
                            ((edge.length / tilesCountWidth) / (currTileSize.x / currTile.columnSize)) * scaleMult.x;

                        Vector3 spawnedTransfLocalScale = new Vector3(
                            1f * widthCorrectionMult /** currTile.columnSize*/,
                            1f * (compoundRawHeight / currTileSize.y) * scaleMult.y /** currTile.rawSize*/,
                            1f * scaleMult.z
                        );
                        /*                            spawnedTransf.localScale = spawnedTransfLocalScale;
                                                    spawnedTransf.parent = edgeGO.transform;*/

                        #endregion


                        #region Spawn

                        #region Position on Edge

                        float lerp = Mathf.Lerp((float) columnN, (float) (columnN + (currTile.columnSize - 1)), 0.5f)
                                     + 0.5f /**(float)currTile.columnSize*/;
                        lerp = lerp / (float) tilesCountWidth;
                        Vector3 placePos = Vector3.Lerp(
                                               edge.start,
                                               edge.end,
                                               //(((float) columnN + 0.5f*(float)currTile.columnSize) ) / (float) tilesCountWidth //position on edge
                                               lerp //position on edge
                                           )
                                           + edge.facingNormal * Vector3.up * heightShift
                                           + edge.facingNormal * Vector3.forward * zShift;

                        #endregion

                        //spawn
                        //Transform spawnedTransf = Instantiate( currTile.transform, placePos, edge.facingNormal ) as Transform;
                        SceneObject newPlaceTileInfo = new SceneObject()
                        {
                            Prefab = currTile.transform,
                            //parent = edgeGO.transform,
                            position = placePos,
                            scale = spawnedTransfLocalScale,
                            rotation = edge.facingNormal
                        };
                        placeTileInfoList.Add(newPlaceTileInfo);
                        /*                    Transform spawnedTransf = PrefabUtility.InstantiatePrefab(currTile.transform) as Transform;
                                            spawnedTransf.position = placePos;
                                            spawnedTransf.rotation = edge.facingNormal;*/

                        //spawnedTransf.gameObject.name += "_" + columnN + "_" + floorN;
                        spawnedPositions.Add(placePos);

                        #endregion


                        #region Spawn detail immideatly, dont wait for next frame

                        //Spawn detail immideatly, dont wait for next frame
                        /*                        if ( callTileSpawners )
                                                {
                                                    CityGen_TileSpawner[] detailSpawners = spawnedTransf.GetComponentsInChildren<CityGen_TileSpawner>();
                                                    if (detailSpawners.Length > 0)
                                                    {
                                                        foreach (CityGen_TileSpawner detailSpawner in detailSpawners)
                                                        {
                                                            if (detailSpawner != null) detailSpawner.Spawn();
                                                        }
                                                    }
                                                }*/

                        #endregion
                    }
                }

                heightShift += currFloorHeights[floorN];
            }


            #region AfterEdgeTilesSpawned

            //edgeGO.transform.parent = transform;


            //GenerateLOD(edgeGO, edge);
            //edgeGO.transform.parent = edgesParent.transform;


            //Profiler.BeginSample ( "__EDGES_TO_TILES_BATCH", this );
            //if(skinDeform) BindBonesToMesh ( MeshBatcher.Batch ( edgeGO.transform ), edgeGO, edge, currFloorHeights.Sum() );
            //Profiler.EndSample ( );

            #endregion
        }


        //return edgesParent;
    }

    private IEnumerator GeneratePlaceTileInfoList_Routine( float yieldChance = 10 )
    {
        if( cityGenTiles.Count == 0 || floorHeights.Count == 0 || edges.Count == 0 ) yield break;
        placeTileInfoList = new List<SceneObject>();

        #region Destory child edges

        string edgesParentName = "edgesParent";
/*        foreach( Transform child in transform )
        {
            //if (child.name == edgesParentName)
            if( child != transform )
            {
                if( Application.isPlaying )
                {
                    Destroy( child.gameObject );
                }
                else
                {
                    DestroyImmediate( child.gameObject );
                }
                //break;
            }
        }*/

        #endregion

        #region Create EdgeParent

        GameObject edgesParent = new GameObject(edgesParentName);
        edgesParent.transform.position = transform.position;
        edgesParent.transform.rotation = transform.rotation;
        edgesParent.transform.parent = transform;

        #endregion

        List<Vector3> spawnedPositions = new List<Vector3>();
        var startTime = DateTime.Now;

        for( var index = 0; index < edges.Count; index++ )
        {
            Edge edge = edges[index];
            if( excludeHighEdges && Mathf.Abs( edge.start.y - transform.position.y ) > 1 ) continue;

            #region FloorHeights

            //GET FLOOR HEIGHTS FROM EDGE
            List<float> currFloorHeights = new List<float>();
            currFloorHeights = new List<float>( floorHeights );

            if( edge.height > 0 )
            {
                currFloorHeights.Clear();
                currFloorHeights.Add( floorHeights.FirstOrDefault() );

                float edgeLenght = edge.height - floorHeights.FirstOrDefault() - floorHeights.Last();

                for( int i = 1; i < ( edgeLenght / 5 ) + 1; i++ )
                {
                    currFloorHeights.Add( 5 );
                }

                currFloorHeights.Add( floorHeights.Last() );
            }

            #endregion


            #region EdgeGO parent

            GameObject edgeGO = new GameObject("Edge " + index/*RandomE.femaleName + " - " + Random.Range(0, 99999)*/ );
            edgeGO.transform.position = edge.start;
            edgeGO.transform.rotation = edge.facingNormal;
            edgeGO.transform.SetParent( transform );

            #endregion

            int tilesCountWidth = (int) Mathf.Clamp(edge.length / tileWidth, 1, 1000);

            //MATRIX
            List<Vector2Int> tileMatrix = new List<Vector2Int>();
            float heightShift = 0;



            for( int floorN = 0; floorN < currFloorHeights.Count; floorN++ ) //for every floor
            {
                for( int columnN = 0; columnN < tilesCountWidth; columnN++ ) //every edge length segment
                {
                    if( IsTileMatrixContains( tileMatrix, columnN, floorN ) )
                        continue; //if this tile already mapped in matrix then skip it!
                    if( Random.Range( 0, 100 ) < emptyChance ) continue; //chance to skip

                    Vector3 placePosPrediction = Vector3.Lerp(edge.start, edge.end, (float) columnN / (float) tilesCountWidth);

                    //if(CityGen_ExcludeBound.allCityGen_ExcludeBounds.Any( v => v.Bounds.Contains( placePosPrediction ) ) )continue;


                    #region dontSpawnNearToSpawned

                    if( dontSpawnNearToSpawned )
                    {
                        if( spawnedPositions.Any( v => Vector3.Distance( v, placePosPrediction ) < dontSpawnNearToSpawned_radius ) )
                        {
                            continue;
                        }
                    }

                    #endregion


                    #region Select Tiles

                    //get random tile from list with params
                    var columnN_1 = columnN;
                    var floorN_1 = floorN;
                    var tilesList = cityGenTiles.Where(
                        v =>
                            v.CheckColumn(columnN_1)
                            && v.CheckFloorN(floorN_1)
                            && v.CheckFacingType(edge.facingType)
                            && columnN_1 >= v.columnSize - 1 && columnN_1 + v.columnSize <= tilesCountWidth
                    );

                    #endregion

                    #region CornerFilters

                    //CORNER FILTER
                    if( ( columnN == ( tilesCountWidth - 1 ) || columnN == 0 ) )
                    {
                        tilesList = tilesList.Where( v => v.canBeCornerElement );
                    }

                    //LAST FLOOR
                    if( floorN == currFloorHeights.Count - 1 )
                    {
                        tilesList = tilesList.Where( v => v.canBeLastFloor );
                    }

                    //PRE-LAST FLOOR
                    if( floorN == currFloorHeights.Count - 2 )
                    {
                        tilesList = tilesList.Where( v => v.canBePreLastFloor );
                    }

                    #endregion

                    //SPAWN
                    if( tilesList.Count() > 0 )
                    {
                        #region FilterTiles

                        int maxPriority = tilesList.Max(v => v.priority);
                        tilesList = tilesList.Where( v => v.priority == maxPriority );

                        var currTile = tilesList.ToList().GetRandom(); /*new CityGen_Tile();*/

                        //if no tile in list with params then create white dummy
                        if( currTile == null )
                        {
                            /*                            currTile = GameObject.CreatePrimitive(PrimitiveType.Cube).AddComponent<CityGen_Tile>();
                                                        currTile.bounds = CompoundBounds(currTile.gameObject);*/
                            continue;
                        }

                        #endregion

                        #region ScaleCorrection

                        #region matrix

                        for( int y = 0; y < currTile.rawSize; y++ )
                        {
                            for( int x = 0; x < currTile.columnSize; x++ )
                            {
                                if( IsTileMatrixContains( tileMatrix, columnN + x, floorN + y ) == false )
                                {
                                    tileMatrix.Add( new Vector2Int( columnN + x, floorN + y ) );
                                }
                            }
                        }


                        #endregion


                        float compoundRawHeight = 0;
                        for( int j = 0; j < currTile.rawSize; j++ )
                        {
                            //FIX HEIGHT FOR TILE
                            if( ( floorN + j ) >= 0 && currFloorHeights.Count > floorN + j )
                            {
                                compoundRawHeight += currFloorHeights [ floorN + j ];
                            }
                            else
                            {
                                compoundRawHeight += currFloorHeights [ 0 ];
                            }

                        }


                        //scale correction
                        Vector3 currTileSize = currTile.bounds.size;
                        float widthCorrectionMult =
                            ((edge.length / tilesCountWidth) / (currTileSize.x / currTile.columnSize)) * scaleMult.x;

                        Vector3 spawnedTransfLocalScale = new Vector3(
                            1f * widthCorrectionMult /** currTile.columnSize*/,
                            1f * (compoundRawHeight / currTileSize.y) * scaleMult.y /** currTile.rawSize*/,
                            1f * scaleMult.z
                        );

                        #endregion


                        #region Spawn

                        #region Position on Edge

                        float lerp = Mathf.Lerp((float) columnN, (float) (columnN + (currTile.columnSize - 1)), 0.5f)
                                     + 0.5f /**(float)currTile.columnSize*/;
                        lerp = lerp / (float)tilesCountWidth;
                        Vector3 placePos = Vector3.Lerp(
                                               edge.start,
                                               edge.end,
                                               //(((float) columnN + 0.5f*(float)currTile.columnSize) ) / (float) tilesCountWidth //position on edge
                                               lerp //position on edge
                                           )
                                           + edge.facingNormal * Vector3.up * heightShift
                                           + edge.facingNormal * Vector3.forward * zShift;

                        #endregion

                        //spawn
                        //Transform spawnedTransf = Instantiate( currTile.transform, placePos, edge.facingNormal ) as Transform;
                        SceneObject newPlaceTileInfo = new SceneObject()
                        {
                            Prefab = currTile.transform,
                            //parent = edgeGO.transform,
                            position = placePos,
                            scale = spawnedTransfLocalScale,
                            rotation = edge.facingNormal
                        };
                        placeTileInfoList.Add( newPlaceTileInfo );
                        spawnedPositions.Add( placePos );

                        #endregion
                        
                    }
                }

                heightShift += currFloorHeights [ floorN ];

                if (FunctionTimeCheck(startTime))
                {
                    yield return 0;
                    startTime = DateTime.Now;
                }

            }


            if( FunctionTimeCheck( startTime ) )
            {
                yield return 0;
                startTime = DateTime.Now;
            }
        }


        //return edgesParent;
    }

    public static bool FunctionTimeCheck(DateTime startTime, float maxDuration = 2)
    {
        return (startTime - DateTime.Now).Duration().TotalMilliseconds > maxDuration;
    }

    #endregion





    bool IsTileMatrixContains(List<Vector2Int> tileMatrix, int  columnN, int floorN)
    {
        bool isTileMatrixContains = tileMatrix.Any(v => v.x == columnN && v.y==floorN );
        return isTileMatrixContains;
    }

    #endregion





    #region Bones

    void BindBonesToMesh(GameObject meshObject, GameObject go, Edge edge, float floorHeightsSum)
    {
        if (meshObject == null || meshObject.GetComponent<MeshFilter>() == null) return;

        var sm = go.AddComponent<SkinnedMeshRenderer>();
        Mesh mesh = meshObject.GetComponent<MeshFilter>().sharedMesh;
        meshObject.SetActive(false);


        #region MaxDistForWeights

        Vector3 edgeMidPoint= Vector3.zero;
        edgeMidPoint += edge.start;
        edgeMidPoint += edge.end;
        edgeMidPoint += edge.startUp;
        edgeMidPoint += edge.endUp;
        edgeMidPoint /= 4;

        Vector2 maxDistVect = new Vector2(edge.length, floorHeightsSum);
        Matrix4x4 m = Matrix4x4.TRS ( edgeMidPoint, edge.facingNormal, new Vector3 ( 1, 1, 1 ) );

        #endregion





        // Assign bone weights to mesh
        // We use 2 bones. One for the lower vertices, one for the upper vertices.

        #region DefineBonePositions

        Vector3 bonepos1 = edge.start;
        Vector3 bonepos2 = edge.start + edge.facingNormal * Vector3.up * floorHeightsSum;
        Vector3 bonepos3 = edge.end;
        Vector3 bonepos4 = edge.end + edge.facingNormal * Vector3.up * floorHeightsSum;

        List<Vector3> bonePosList = new List<Vector3>();
        bonePosList.Add ( bonepos1 );
        bonePosList.Add(bonepos2);
        bonePosList.Add ( bonepos3 );
        bonePosList.Add ( bonepos4 );

        #endregion


        Vector3[] vertices = mesh.vertices;
        BoneWeight[] weights = new BoneWeight[vertices.Length];
        List< BoneWeightTable> weightTableList = new List<BoneWeightTable>();




        #region IterateAllIndicesAndSetWeights

        for (int i = 0; i < weights.Length; i++)
        {
            Vector3 vertPos = vertices[i];
            vertPos = go.transform.TransformPoint(vertPos);


            var weightTable = new BoneWeightTable();
            weightTable.vertIndex = i;
            weightTableList.Add ( weightTable );


            //Foreach bone cald weight based on distance
            for ( int j = 0; j < bonePosList.Count; j++ )
            {
                Vector3 bonePOs = m.inverse.MultiplyPoint3x4(vertPos );
                Vector3 vertPOsTemp = m.inverse.MultiplyPoint3x4 ( bonePosList[j] );

                //Debug.DrawLine ( edgeMidPoint + bonePOs, edgeMidPoint + vertPOsTemp, Color.green, 10 );


                Vector3 boneToMidDir = ( bonePOs - vertPOsTemp ).normalized;
                boneToMidDir.x *= maxDistVect.x;
                boneToMidDir.y *= maxDistVect.y;

                float maxDist = Mathf.Abs(boneToMidDir.magnitude);



                weightTable.boneIndexList.Add (
                    new BoneWeightTable.Weights ( )
                    {
                        boneIndex = j,
                        weight = Mathf.Clamp (
                            ( maxDist - Vector3.Distance ( vertPos, bonePosList[j] ))/ maxDist, 
                            0, 1 
                        )
                    }
                );
            }
            weightTable.NormalizeWeights();



            weights[i].boneIndex0 = weightTable.boneIndexList[0].boneIndex;
            weights[i].weight0 = weightTable.boneIndexList[0].weight;

            weights[i].boneIndex1 = weightTable.boneIndexList[1].boneIndex;
            weights[i].weight1 = weightTable.boneIndexList[1].weight;

            weights[i].boneIndex2 = weightTable.boneIndexList[2].boneIndex;
            weights[i].weight2 = weightTable.boneIndexList[2].weight;

            weights[i].boneIndex3 = weightTable.boneIndexList[3].boneIndex;
            weights[i].weight3 = weightTable.boneIndexList[3].weight;


        }

        #endregion


        #region CreateBoneTransforms

        // A BoneWeights array (weights) was just created and the boneIndex and weight assigned.
        // The weights array will now be assigned to the boneWeights array in the Mesh.
        mesh.boneWeights = weights;

        // Create Bone Transforms and Bind poses
        // One bone at the bottom and one at the top
        Transform[] bones = new Transform[bonePosList.Count];
        Matrix4x4[] bindPoses = new Matrix4x4[bonePosList.Count];

        for (int i = 0; i < bonePosList.Count; i++)
        {
            bones[i] = new GameObject ( i.ToString()+" Bone" ).transform;
            bones[i].parent = go.transform;
            bones[i].localRotation = Quaternion.identity;
            bones[i].position = bonePosList[i];


            // The bind pose is bone's inverse transformation matrix
            // In this case the matrix we also make this matrix relative to the root
            // So that we can move the root game object around freely
            bindPoses[i] = bones[i].worldToLocalMatrix * go.transform.localToWorldMatrix;
        }

        #endregion


        // assign the bindPoses array to the bindposes array which is part of the mesh.
        mesh.bindposes = bindPoses;

        // Assign bones and bind poses
        sm.bones = bones;
        sm.sharedMesh = mesh;
        sm.sharedMaterials = meshObject.GetComponent<Renderer>().sharedMaterials;


        bones[1].position = edge.startUp;
        bones[2].position = edge.endOrig;
        bones[3].position = edge.endUp;






        Mesh bakeMesh = new Mesh();
        sm.BakeMesh ( bakeMesh );

        DestroyImmediate(sm);
        var mr = go.AddComponent<MeshRenderer> ( );
        mr.sharedMaterials = meshObject.GetComponent<Renderer>().sharedMaterials;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = bakeMesh;


        //DESTROY PREV OBJECTS
        Transform[] childs = go.GetComponentsInChildren<Transform>();
        childs = childs./*Except(bones).*/Where(v=>v!=go.transform).ToArray();

        for (int i = 0; i < childs.Length; i++)
        {
            if ( childs[i] != null ) DestroyImmediate ( childs[i].gameObject );
        }

    }

    
    public class BoneWeightTable
    {
        public class Weights
        {
            public int boneIndex;
            public float weight;
        }

        public List<Weights> boneIndexList = new List<Weights> ( );
        public int vertIndex;


        public void NormalizeWeights()
        {
/*            float maxWeight= boneIndexList.Max(v => v.weight);
            for (var i = 0; i < boneIndexList.Count; i++)
            {
                Weights w = boneIndexList[i];
                w.weight /= maxWeight;
                //w.weight = 1 - w.weight; //!!!!!!!!!!!!!!!!!!!!

                //w.weight = Mathf.Pow(w.weight, 2);
            }*/



            //NORMAILZE to sum = 1
            var weightSumm = boneIndexList.Sum(v => v.weight);
            for (var i = 0; i < boneIndexList.Count; i++)
            {
                Weights w = boneIndexList[i];
                w.weight /= weightSumm;
                //w.weight = 1 - w.weight;
            }

            boneIndexList = boneIndexList.OrderByDescending(v => v.weight).ToList();
        }

    }

    #endregion



    

    #region Utils

    private void AdjustHeightToTerrain()
    {
        if (currMesh == null) return;

        Vector3 raycastPos = Vector3.zero; //transform.position;

        for (int i = 0; i < currMesh.vertices.Length; i++)
        {
            raycastPos += transform.TransformPoint(currMesh.vertices[i]);
        }
        raycastPos /= (float) currMesh.vertices.Length;
        raycastPos -= Vector3.up*0.1f;

        Debug.DrawRay(raycastPos, Vector3.down*100, Color.red, 100f);

        RaycastHit hit;
        if (Physics.Raycast( /*transform.position*/raycastPos, Vector3.down, out hit, 5000f))
        {
            transform.position += Vector3.up*(hit.point.y - transform.position.y);
        }

    }

    private Bounds CompoundBounds(GameObject go)
    {
        Bounds bounds = new Bounds();
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer1 in renderers)
        {
            if (bounds.extents == Vector3.zero)
            {
                bounds = renderer1.bounds;
            }

            bounds.Encapsulate(renderer1.bounds);
        }


        if (bounds.extents == Vector3.zero)
        {
            bounds = new Bounds(go.transform.position, Vector3.one);
        }


        return bounds;
    }

    private void GenerateLOD(GameObject go, Edge currEdge)
    {
        if(!generateLOD) return;

        LODGroup currLOD = go.gameObject.AddComponent<LODGroup>();

        currEdge.lod_0 = go.GetComponentsInChildren<Renderer>();

        CityGen_TileSpawner spawner = go.AddComponent<CityGen_TileSpawner>();
        spawner.collectionName = "LOD_BOX";
        spawner.destroyThis = false;

        GameObject generatedLOD = spawner.Spawn();
        currEdge.lod_1 = generatedLOD.GetComponentsInChildren<Renderer>();
        generatedLOD.GetComponentInChildren<CityGen_GenerateLOD>().lodGroup = currLOD;

        currLOD.SetLODs(new[]
        {
            new LOD(1f/2.7f, currEdge.lod_0),
            new LOD(1f/30, currEdge.lod_1),
            //new LOD(), 
        });
        //currLOD.fadeMode = LODFadeMode.CrossFade;
        currLOD.RecalculateBounds();

    }


    #endregion



    #region FloorGen

    private void EdgesToPlaneMesh()
    {
        foreach (Transform child in transform)
        {
            if (child.name == "Plane mesh floor")
            {
                DestroyImmediate(child.gameObject);
                break;
            }
        }

        GameObject go = new GameObject(floorMeshName);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        MeshCollider mc = go.AddComponent<MeshCollider>();

        MeshDraft meshDraft = new MeshDraft();
        Vector3[] prevPoints = new Vector3[2];

        int counter = 0;


        foreach (Edge edge in edges)
        {
                             
            Vector3 p1, p2, p3, p4;
            p1 = edge.start + edge.facingNormal * Vector3.forward * -meshWidth/2;
            p2 = edge.start + (edge.facingNormal) * Vector3.forward * meshWidth/2;
            p3 = edge.end + (edge.facingNormal) * Vector3.forward * -meshWidth/2;
            p4 = edge.end + (edge.facingNormal) * Vector3.forward * meshWidth/2;

            if (prevPoints[0] != Vector3.zero) p1 = prevPoints[0];
            if (prevPoints[1] != Vector3.zero) p2 = prevPoints[1];
            prevPoints[0] = p3;
            prevPoints[1] = p4;


            Vector3[] vertices = new[] {p1, p2, p3, p4};
            int[] indices = new[]
            {
                2, 1, 0,
                1, 2, 3
            };
            float currUV_Y_Shift = (1f/(float)uvStretch_counterTriggerVal)*(float)(counter+1);
            float prevUV_Y_Shift = (1f / (float)uvStretch_counterTriggerVal) * (float)counter;

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0, prevUV_Y_Shift),
                new Vector2(1*uvStretch_x, prevUV_Y_Shift),
                new Vector2(0, currUV_Y_Shift),
                new Vector2(1*uvStretch_x, currUV_Y_Shift)
            };

            //UV Y STRETCHING 
            counter++;
            if (counter >= uvStretch_counterTriggerVal) counter = 0;


            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.uv = uvs;

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            MeshDraft tempMeshDraft = new MeshDraft(mesh);
            meshDraft.Add(tempMeshDraft);


        }
        go.transform.position = transform.position;
        go.transform.rotation = transform.rotation;
        go.transform.SetParent(transform);
        meshDraft.Move(-transform.position);   


        Mesh endMesh = meshDraft.ToMesh();
        endMesh.RecalculateBounds();
        endMesh.RecalculateNormals();


        //SAVE MESH To FILE
/*        string generatedMeshesPath = "Z_GeneratedMeshes";
        if (!AssetDatabase.IsValidFolder("Assets/" + generatedMeshesPath))
        {
            AssetDatabase.CreateFolder("Assets", generatedMeshesPath);
        }

        string newAssetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/" + generatedMeshesPath + "/NewMesh.asset");
        AssetDatabase.CreateAsset(endMesh, newAssetPath);
        AssetDatabase.SaveAssets();

        endMesh = (Mesh)(AssetDatabase.LoadAssetAtPath(newAssetPath, typeof(Mesh)));*/

       
        mf.mesh = endMesh;
        if (meshFloorMat != null) mr.sharedMaterial = meshFloorMat;
        mc.sharedMesh = endMesh;

    }



    #endregion



    #region EdgeGen


    float triggerAngle = 0.999f;
    private void CalcEdges()
    {
        if (useQuads)
        {
            MeshToQuads();
        }
        else
        {
            for (int i = 0; i < currMesh.triangles.Length - 1; i += 3)
            {
                //CALC FROM MESH OPEN EDGES vertices

                TrisToEdge(currMesh, i, i + 1);
                TrisToEdge(currMesh, i + 1, i + 2);
                TrisToEdge(currMesh, i + 2, i);
            }
        }



        foreach (Edge edge in edges)
        {
            //EDGE LENGTH
            edge.length = Vector3.Distance(
                edge.start/* - Vector3.up * edge.start.y*/,
                edge.end/* - Vector3.up * edge.end.y*/
                );

            //AVERAGE WIDTH of UP and DOWN EDGES if upper edge setted
            if (edge.startUp.sqrMagnitude > 0)
            {
                edge.length = Mathf.SmoothStep(
                    Vector3.Distance(edge.start, edge.end),
                    Vector3.Distance(edge.startUp, edge.endUp),
                    0.5f);
                edge.endOrig = edge.end;
                edge.end = edge.start + ( edge.end - edge.start).normalized * edge.length;
            }




            //FACING NORMAL 
            if (!edge.facingNormalCalculated)
            {
                edge.facingNormal = Quaternion.LookRotation ( Vector3.Cross ( edge.end - edge.start, Vector3.up ) );


                if ( edge.startUp.sqrMagnitude > 0 )
                {
                    var vect = Vector3.Lerp ( edge.endUp, edge.startUp, 0.5f ) - Vector3.Lerp ( edge.end, edge.start, 0.5f );
                    edge.facingNormal = Quaternion.LookRotation ( Vector3.Cross ( edge.end - edge.start, vect ) );


                    //FIX FOR NORMALs POINTING DIRECT TO UP/DOWN
                    if ( Mathf.Abs ( Vector3.Dot ( Vector3.up, (edge.facingNormal * Vector3.forward).normalized ) ) > triggerAngle )
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



        float longest = float.MinValue;
        float angle;
        for (int i = 0; i < edges.Count; i++)
        {
            Vector3 facingTargetPos = facingTarget!=null ? facingTarget.position : transform.position+transform.up*-10f;

/*            Debug.DrawRay(edges[i].start, edges[i].facingNormal*Vector3.forward, Color.red, 10f);
            Debug.DrawRay(edges[i].start, facingTargetPos - Vector3.Lerp(edges[i].start, edges[i].end, 0.5f), Color.blue, 10f);*/


            angle = Vector3.Angle(
                edges[i].facingNormal * Vector3.forward,
                facingTargetPos - Vector3.Lerp(edges[i].start, edges[i].end, 0.5f)
                );
            edges[i].facingType = angle < 89f ? CityGenTyleFacingType.frontSide : CityGenTyleFacingType.backSide;

            if ((int) (edges[i].length/tileWidth) < tileCountToSwitchToFronSide)
            {
                edges[i].facingType = CityGenTyleFacingType.sideWall;
            }
        }
    }



    private void TrisToEdge(Mesh currMesh, int n1, int n2)
    {
        Vector3 val1 = transform.TransformPoint(currMesh.vertices[currMesh.triangles[n1]]);
        Vector3 val2 = transform.TransformPoint(currMesh.vertices[currMesh.triangles[n2]]);

        Edge newEdge = new Edge(val1, val2);

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


        edges.Add(newEdge);
    }


    public void MeshToQuads( bool drawDebugLines = false)
    {
        if ( meshFilter == null ) meshFilter = this.GetComponent<MeshFilter> ( );
        if ( meshFilter != null && currMesh == null ) currMesh = meshFilter.sharedMesh;

        if(currMesh==null) return;
        Mesh mesh = currMesh;//GetComponent<MeshFilter>().sharedMesh;

        List<Triangle> tris = new List<Triangle>();
        //List<Quad> quads = new List<Quad>();
        List<TriangleGroup> trisGroup = new List<TriangleGroup>();



        //CREATE TRIANGLES LIST

        #region CreateTriangle

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Transform transf = meshFilter.transform;

        for ( int i = 0; i < triangles.Length - 1; i += 3 )
        {
            List<Vector3> vertsList = new List<Vector3>();
            var edgesList = new List<Edge>();


            //VERTS
            for (int j = 0; j < 3; j++)
            {
                vertsList.Add ( meshFilter.transform.TransformPoint ( vertices[triangles[i + j]] ) );
            }

            //EDGES
            edgesList.Add(new Edge(
                    transf.TransformPoint (vertices[triangles[i]] ),
                    transf.TransformPoint (vertices[triangles[i + 1]] )
                )
            );
            edgesList.Add ( new Edge (
                    transf.TransformPoint ( vertices[triangles[i + 1]] ),
                    transf.TransformPoint ( vertices[triangles[i + 2]] )
                )
            );
            edgesList.Add ( new Edge (
                    transf.TransformPoint ( vertices[triangles[i + 2]] ),
                    transf.TransformPoint ( vertices[triangles[i]] )
                )
            );


            //TRIS
            tris.Add( 
                new Triangle()
                {
                    normal = Vector3.Cross ( vertsList[0] - vertsList[1], vertsList[2] - vertsList[1] ).normalized,
                    verts = vertsList.ToArray(),
                    edges = edgesList
                }
            );
        }

        #endregion




        //FIND TRIS WITH SIMILAR NORMALS AND Common vertices
        while (tris.Count(v=>v.checkEnded==false)>0)
        {
            var triangle = tris.FirstOrDefault(v => v.checkEnded == false);

            if (triangle!=null)
            {
                List<Triangle> trianglesSet = new List<Triangle>();

                triangle.area = triangle.CalcArea();

                var trisGroupList = tris.Where(
                    v =>
                        v!=triangle 
                        && !v.checkEnded 
                        && CompareVectorArrays(triangle.verts, v.verts) == 2
                        && Vector3.Angle ( v.normal, triangle.normal ) < normalAngleTolerance
                );

                Triangle trFound = trisGroupList
                    .OrderBy ( v => CompareVectorArraysByLongestSyde ( triangle.verts, v.verts ) )
                    .ThenBy ( v => Vector3.Angle ( v.normal, triangle.normal ) )
                    .ThenBy( v => Mathf.Abs ( v.CalcArea ( ) - triangle.area ) )
                    .FirstOrDefault();

                if (trFound != null)
                {
                    trianglesSet.Add ( trFound );
                    trFound.checkEnded = true;
                }
/*                foreach ( var tr in tris.Where ( v =>v!=triangle && !v.checkEnded && CompareVectorArrays ( triangle.verts, v.verts ) > 0 ) )
                {
                    if (Vector3.Angle(tr.normal, triangle.normal) < normalAngleTolerance
                        && CompareVectorArrays(triangle.verts, tr.verts) == 2)
                    {
                        trianglesSet.Add(tr);
                        tr.checkEnded = true;
                        break;
                    }
                }*/


                if (trianglesSet.Count > 0)
                {
                    trianglesSet.Add ( triangle );

                    trisGroup.Add ( new TriangleGroup ( trianglesSet) );
                }

                triangle.checkEnded = true;
            }
        }




        //SIMILAR TRIS TO QUADS, FILTER TRIS TO POINTS
        foreach (TriangleGroup triangleGroup in trisGroup)
        {

            //FIND EDGES WITH SIMILAR DIRECTIONS AND NOT COMMON VERTICES
            List<Edge> allEdges = triangleGroup.trisList.SelectMany ( v => v.edges ).ToList();
            List<Edge> parallelEdges = new List<Edge>();

            while ( allEdges.Count ( v => !v.checkEnded) > 0 )
            {
                Edge edge = allEdges.FirstOrDefault ( v => v.checkEnded == false );

                if ( edge != null )
                {
                    foreach ( var tr in allEdges.Where ( v => !v.checkEnded ) )
                    {
                        Vector3[] vertices1 = new[] { tr.start ,  tr.end  };
                        Vector3[] vertices2 = new[] { edge.start , edge.end  };


                        if ( Mathf.Abs(Vector3.Dot ( (tr.start - tr.end).normalized, (edge.start - edge.end).normalized )) > 0.75f
                             && CompareVectorArrays (vertices1, vertices2 ) == 0 
                            )
                        {
                            parallelEdges.Add(edge);
                            parallelEdges.Add ( tr );
                            tr.checkEnded = true;
                            edge.checkEnded = true;

                            break;
                        }
                    }
                    edge.checkEnded = true;
                }
            }

            var parallelEdgesColl = parallelEdges
                .OrderByDescending(v => Vector3.Angle(v.start - v.end, Vector3.ProjectOnPlane(v.start - v.end, Vector3.right/*Vector3.up*/)  ))
                .Take(2);
            var edgesColl = parallelEdgesColl as Edge[] ?? parallelEdgesColl.ToArray();
            Edge edgeFound = edgesColl
                .OrderBy ( v => /*Vector3.Lerp ( v.start, v.end, 0.5f ).y*/v.start.y ).ThenBy(v=>v.end.y)
                .FirstOrDefault();

            Edge edgeHeight = edgesColl.FirstOrDefault ( v => v != edgeFound );


            //QUAD NORMAL
            if ( edgeFound != null && edgeHeight !=null)
            {
                Vector3 midPoint = Vector3.Lerp(edgeFound.start, edgeFound.end, 0.5f);
                Vector3 midPointEnd = Vector3.Lerp ( edgeHeight.start, edgeHeight.end, 0.5f );





                //CREATE NEW EDGE
                bool isVecorsLooksInOneDirection =
                    Vector3.Dot((edgeFound.start - edgeFound.end).normalized, (edgeHeight.start - edgeHeight.end).normalized) > 0;

                Edge newEdge = edgeFound;
                newEdge.startUp = isVecorsLooksInOneDirection ? edgeHeight.start : edgeHeight.end;
                newEdge.endUp = isVecorsLooksInOneDirection ? edgeHeight.end : edgeHeight.start;

                newEdge.height = Vector3.Distance(midPoint, midPointEnd);



                if (drawDebugLines)
                {
                    Debug.DrawRay ( midPointEnd, triangleGroup.normal * -10, Color.white, 20 );
                    Debug.DrawLine ( edgeFound.start + Vector3.up, edgeFound.end + Vector3.up, Color.blue, 10 );

                    Debug.DrawLine ( triangleGroup.GetTrisGroupMidPoint ( ), triangleGroup.GetTrisGroupMidPoint ( ) - triangleGroup.normal * 10, Color.blue, 50 );

                    Debug.DrawLine(
                        newEdge.start + triangleGroup.normal * -10,
                        newEdge.startUp + triangleGroup.normal * -10,
                        Color.blue, 50 );
                    Debug.DrawRay (
                        newEdge.startUp,
                        triangleGroup.normal * -10,
                        Color.yellow, 50 );
                    Debug.DrawRay (
                        newEdge.start,
                        triangleGroup.normal * -10,
                        Color.yellow, 50 );
                }

                edges.Add ( newEdge );

            }
        }

    }


    int CompareVectorArrays(Vector3[] arr1, Vector3[] arr2)
    {
        int count = 0;

        for (var i = 0; i < arr1.Length; i++)
        {
            var vect1 = arr1[i];
            for (var i2 = 0; i2 < arr2.Length; i2++)
            {
                var vect2 = arr2[i2];
                if (Vector3.SqrMagnitude(vect1 - vect2) < 0.1f)
                {
                    count++;
                }
            }
        }

        return count;
    }

    float CompareVectorArraysByLongestSyde ( Vector3[] arr1, Vector3[] arr2 )
    {
        int count = 0;

        List<Vector3> edgePoints = new List<Vector3>();

        //CHECK IF TRIANGLE HAVE 2 points INTERSECT
        for (var i = 0; i < arr1.Length; i++)
        {
            var vect1 = arr1[i];
            for (var j = 0; j < arr2.Length; j++)
            {
                var vect2 = arr2[j];
                if (Vector3.SqrMagnitude(vect1 - vect2) < 0.1f)
                {
                    count++;

                    edgePoints.Add ( vect1 );
                }
            }
        }

        //IF POINTS INTERSECTS CHECK IF THIS EDGE IS LONGEST
        if (count == 2)
        {
            List<float> edgDist = new List<float>();
            edgDist.Add ( Vector3.Distance ( arr1[0], arr1[1] ) );
            edgDist.Add ( Vector3.Distance ( arr1[1], arr1[2] ) );
            edgDist.Add ( Vector3.Distance ( arr1[2], arr1[0] ) );

            List<float> edgDist2 = new List<float> ( );
            edgDist2.Add ( Vector3.Distance ( arr2[0], arr2[1] ) );
            edgDist2.Add ( Vector3.Distance ( arr2[1], arr2[2] ) );
            edgDist2.Add ( Vector3.Distance ( arr2[2], arr2[0] ) );

            float edgeFoundDist = Vector3.Distance(edgePoints[0], edgePoints[1]);


            float edgesDiff = Mathf.Abs( 
                Mathf.Abs ( edgDist.Max ( ) - edgeFoundDist )
                - Mathf.Abs ( edgDist2.Max ( ) - edgeFoundDist )
                );

            return edgesDiff;
/*            if (edgesDiff < 0.1f)
            {
                return true;
            }*/
        }

        return float.MaxValue;
    }

    #endregion

//#endif
}


#region Structs

[Serializable]
public class Edge
{
    public Vector3 start;
    public Vector3 end;
    public Vector3 endOrig;

    public Vector3 startUp;
    public Vector3 endUp;



    public float height = 0;

    public CityGenTyleFacingType facingType;

    public float length;
    public Quaternion facingNormal;
    public bool facingNormalCalculated = false;

    public Renderer[] lod_0 = new Renderer[0];
    public Renderer[] lod_1 = new Renderer[0];

    public bool checkEnded = false;


    public Edge(Vector3 startPoint, Vector3 endPoint)
    {
        start = startPoint;
        end = endPoint;
    }
}




public class Triangle
{
    public Vector3[] verts=new Vector3[3];
    public List<Edge> edges = new List<Edge>();
    public Vector3 normal = Vector3.forward;

    public float area =0;

    public bool checkEnded = false;


    public float CalcArea()
    {
        var a = Vector3.Distance(verts[0], verts[1]);
        var b = Vector3.Distance ( verts[1], verts[2] );
        var c = Vector3.Distance ( verts[2], verts[0] );

        float p = (a + b + c) / 2;

/*        float s = Mathf.Sqrt(
            p* (p-a)*(p-b)*(p-c) 
        );*/

        return p;

    }
}

public class TriangleGroup
{
    public List<Triangle> trisList = new List<Triangle>();

    public Vector3 normal;


    public TriangleGroup ( List<Triangle> tris )
    {
        trisList.AddRange ( tris );
        normal = tris.FirstOrDefault().normal;

        for ( int i = 0; i < tris.Count; i++ )
        {
            tris[i].checkEnded = true;
        }
    }


    public Vector3 GetTrisGroupMidPoint()
    {
        List<Vector3> allTris = trisList.SelectMany ( v => v.verts ).ToList ( );

        Vector3 groupCenter = Vector3.zero;
        foreach ( var tr in allTris )
        {
            groupCenter += tr;
        }
        groupCenter /= allTris.Count;

        return groupCenter;
    }

    public Vector3 GetTrisMidPoint (Triangle triangle)
    {
        //List<Vertex> allTris = trisList.SelectMany ( v => v.verts ).ToList ( );

        Vector3 groupCenter = Vector3.zero;
        foreach ( var tr in triangle.verts )
        {
            groupCenter += tr;
        }
        groupCenter /= triangle.verts.Length;

        return groupCenter;
    }

}


/*[Serializable]
public class PlaceTileInfo : SceneObject
{
    public bool isImportant = false;
    public Transform prefab;
    
    //public Transform parent;
/*    public Vector3 position;
    public Vector3 scale;
    public Quaternion rotation;#1#

    //public Vector2Int gridCell;
//    public Vector2Int worldPosOnGrid;

}*/

#endregion




//DATA
[Serializable]
public class SceneObject
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;

    public Transform Prefab { get; set; }
}




#if UNITY_EDITOR

[CustomEditor ( typeof ( CityGen_EdgesToTiles ) )]
[CanEditMultipleObjects]
public class CityGen_EdgesToTiles_Editor : Editor
{

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ( );

        //CityGen_EdgesToTiles[] generators = (CityGen_EdgesToTiles[]) targets;

        if ( GUILayout.Button ( "Generate" ) )
        {
            foreach ( var targ in targets )
            {
                ( (CityGen_EdgesToTiles)targ ).Generate ( );
            }
        }

/*        if ( GUILayout.Button ( "Fit Terrain height" ) )
        {
            foreach ( var targ in targets )
            {
                //generator.FitTerrainHeightToEdges();
                ( (CityGen_EdgesToTiles)targ ).FitTerrainHeightToEdges ( );
            }
        }
        if ( GUILayout.Button ( "Paint Terrain splatmap" ) )
        {
            foreach ( var targ in targets )
            {
                //generator.FitTerrainHeightToEdges();
                ( (CityGen_EdgesToTiles)targ ).SetTerrainSplatmap ( );
            }
        }*/

        if ( GUILayout.Button ( "GenerateLODs" ) )
        {
            foreach ( var targ in targets )
            {
                CityGen_GenerateLOD[] lodGens = ( (CityGen_EdgesToTiles)targ ).GetComponentsInChildren<CityGen_GenerateLOD> ( true );
                /*                LODGroup[] lodGroups = ((CityGen_EdgesToTiles)targ).GetComponentsInChildren<LODGroup>(true);

                                foreach (LODGroup lodGroup in lodGroups)
                                {
                                    lodGroup.enabled = false;
                                }*/

                foreach ( CityGen_GenerateLOD lod in lodGens )
                {
                    lod.Generate ( );
                }
            }
        }



        if ( GUILayout.Button ( "Mesh to Quads" ) )
        {
            foreach ( var targ in targets )
            {
                //generator.FitTerrainHeightToEdges();
                ( (CityGen_EdgesToTiles)targ ).MeshToQuads(true);
            }
        }
    }
}

#endif