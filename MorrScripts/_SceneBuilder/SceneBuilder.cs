using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProceduralToolkit;
using UnityEngine;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif


//[ExecuteInEditMode]
public class SceneBuilder : MonoBehaviour 
{
    #region Vars

/*    [Serializable]
    public class SceneBuilder_Tile_WithRatio
    {
        public SceneBuilder_Tile tile;
        public int ratio = 1;
    }*/
    
    
    public List<SceneBuilder_Tile/*_WithRatio*/> tilesList_withRatio = new List<SceneBuilder_Tile/*_WithRatio*/>();
    private List<SceneBuilder_Tile> tilesList = new List<SceneBuilder_Tile>();

    //public float waitSecAfterSpawn = 0.1f;

    public int maxTilesToPlace = 10;
    public int currPlacedTilesCount = 0;
    public int coridorsRatioMult = 10;


    public SceneBuilder_Tile startTile;
    public bool generateOnStart = false;

    public List<TileConnection> openConnections = new List<TileConnection>();


    public class BoundsWithObject
    {
        public Bounds bounds;
        public GameObject gameObject;
    }
    List<BoundsWithObject> spawnedBounds = new List<BoundsWithObject>();
    
    
    public List<SceneBuilder_Tile> TilesList
    {
        get
        {
/*            if (tilesList.Count == 0)
            {
                tilesList = Resources.LoadAll<SceneBuilder_Tile> ( "SceneBuilder" ).ToList();
            }*/
            
            return tilesList;
        }
        set { tilesList = value; }
    }
    
    
    
    public MazeGenerator LastMazeGenerator { get; set; }

    #endregion



    void Start()
    {
        if (generateOnStart)
        {
            //StartCoroutine(GenRoutine());
            Generate();
        }
    }



    #region Generation

    [ContextMenu("Generate")]
    public void Generate()
    {
        Generate(true);
    }

    public void Generate(bool clearData)
    {
        var startTime = DateTime.Now;
        
        #region Init data

        if (clearData)
        {
            if (InitDataForGeneration()) return;
        }

        #endregion


        #region PlaceTiles

        List<SceneBuilder_Tile> openTiles = TilesList.Where(v => !v.deadEnd).ToList();


        for (int i = 0; i < maxTilesToPlace; i++)
        {
            SpawningLoop( ref openTiles, true);
        }

        #endregion


        if (clearData)
        {
            CloseDeadEnds();
        }

        
        #region MarkSCeneDirty

#if UNITY_EDITOR
        if (Application.isPlaying==false)
        {
            //EditorUtility.SetDirty(gameObject.scene);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        #endregion
        
        Debug.Log( "GenerationTime = " + (DateTime.Now - startTime).TotalSeconds );
    }

    public void CloseDeadEnds()
    {
        List<SceneBuilder_Tile> deadEndTiles = TilesList.Where(v => v.deadEnd).ToList();

        if (deadEndTiles.Count > 0)
        {
            Debug.Log("OPenConnection count for deadEnds = " + openConnections.Count);

            var openConnectionsCount = openConnections.Count;
            for (int i = 0; i < openConnectionsCount; i++)
            {
                SpawningLoop(ref deadEndTiles, false);
            }
        }
    }

    public bool InitDataForGeneration()
    {
        //ClearConsole();
        DestroyChilds();
        ClearSpawnedBounds();
        TilesList.Clear();
        spawnedBounds.Clear();

        if (startTile != null)
        {
            startTile.CompoundBounds();
            spawnedBounds.Add(new BoundsWithObject() {bounds = startTile.Bounds, gameObject = startTile.gameObject});


            foreach (var tile in tilesList_withRatio)
            {
                int tileSpawnRatio = tile.spawnRatio;
                if (tile.isCoridor) tileSpawnRatio *= coridorsRatioMult;

                for (int i = 0; i < tileSpawnRatio; i++)
                {
                    TilesList.Add(tile);
                }
            }

            //TilesList = Resources.LoadAll<SceneBuilder_Tile> ( "SceneBuilder" ).ToList();
            if (TilesList.Count == 0) return true;

            openConnections.Clear();
            currPlacedTilesCount = 0;

            foreach (TileConnection tileConnection in startTile.connectionsList)
            {
                openConnections.Add(tileConnection);
            }
        }

        foreach (SceneBuilder_Tile tile in TilesList)
        {
            tile.CompoundBounds();
        }

        return false;
    }


    private void SpawningLoop ( ref List<SceneBuilder_Tile> tiles, bool checkConnection )
    {
        if(openConnections.Count==0) return;
        
        
        //int randomIndex = Random.Range ( 0, openConnections.Count );
        List<TileConnection> tileConnections = openConnections.Where(v =>
        {
            if (LastMazeGenerator != null && checkConnection)
            {
                return LastMazeGenerator.gridBounds.Contains(v.connectionTransf.position);
            }
            return true;
        }).ToList();
        if(tileConnections.Count==0) return;
        
        TileConnection connection = tileConnections.GetRandom();  //openConnections[randomIndex];

        SceneBuilder_Tile newTile = SpawnTile ( connection, ref tiles, checkConnection );



        if ( newTile != null )
        {
            connection.IsOpened = false;
            //openConnections.RemoveAt ( randomIndex );
            openConnections.Remove(connection);
            
            currPlacedTilesCount++;

            foreach ( TileConnection tileConnection in newTile.connectionsList )
            {
                if (tileConnection.IsOpened  )
                {
                    openConnections.Add(tileConnection);
                }
            }
        }
        else if(checkConnection==false)
        {
            GameObject deadEndFailedGameObject = new GameObject("Dead_end > type: " + connection.connectionType );

            deadEndFailedGameObject.transform.position = connection.connectionTransf.position;
            deadEndFailedGameObject.transform.SetParent( transform);
        }
    }


    private SceneBuilder_Tile SpawnTile(TileConnection spawnConnection, ref List<SceneBuilder_Tile> tiles, bool checkConnection)
    {
        var spawnConnection_Tile = spawnConnection.connectionTransf.GetComponentInParent<SceneBuilder_Tile>().gameObject;
        
        tiles.Shuffle ( );
        List<SceneBuilder_Tile> badTiles = new List<SceneBuilder_Tile>();

        for ( int i = 0; i < tiles.Count; i++ )
        {
            #region Collect connectionsList

            SceneBuilder_Tile tileToSpawn = tiles[ i];
            if(badTiles.Contains(tileToSpawn)) continue; //double check fix
            if(tileToSpawn.connectionsList.Any(v=>v.connectionType == spawnConnection.connectionType) == false) continue;
            
            
            List<TileConnection> connectionsList = tileToSpawn.connectionsList.Where(v=>v.connectionType == spawnConnection.connectionType).ToList();
            if(connectionsList.Count==0) continue;

            if (Vector3.Angle(spawnConnection.connectionTransf.forward, Vector3.up) < 10)
            {
                Debug.Log(spawnConnection.connectionType + " IsFlipped", spawnConnection.connectionTransf);
                
                connectionsList = connectionsList
                    .Where(v =>
                        {
                            if (v == null || v.connectionTransf == null) return false;
    
                            /*var sceneBuilderTile = v.connectionTransf.GetComponentInParent<SceneBuilder_Tile>();
                            if (sceneBuilderTile == null) return false;*/
                            
                            //return  Vector3.Angle( sceneBuilderTile.transform.up, Vector3.down ) < 10;
                            return  Vector3.Angle( v.connectionTransf.forward, spawnConnection.connectionTransf.forward ) > 90;

                        }
                    )
                    .ToList();
                
                Debug.Log("FlippedTile availableConnections count =" + connectionsList.Count());
            }
            if(connectionsList.Count==0) continue;

            connectionsList.Shuffle();

            #endregion
            
            

            for ( int j = 0; j < connectionsList.Count; j++ )
            {
                //int randomConnectIndex = j;//Random.Range ( 0, tileToSpawn.connectionsList.Count );
                TileConnection tileConnection = connectionsList [ j ];

                if ( tileConnection != null )
                {
                    Quaternion rot = Quaternion.Inverse ( tileConnection.connectionTransf.rotation ) *
                                     tileConnection.connectionTransf.root.rotation;
                    rot = ( spawnConnection.connectionTransf.rotation * Quaternion.Euler ( 0, 180, 0 ) ) * rot;

                    Vector3 pos = tileConnection.InverseCoonectionPos ( spawnConnection.connectionTransf.position, rot );

                    
                    //отразить вращение блока если он перевернут вверх ногами
                    if (Vector3.Angle(/*rot * Vector3.up*/spawnConnection.connectionTransf.up, Vector3.down) < 20)
                    {
                        //rot = Quaternion.Euler(rot.eulerAngles.x, -rot.eulerAngles.y, rot.eulerAngles.z);
                        rot *= Quaternion.Euler(0,0,180);
                        //Debug.DrawRay(pos, Vector3.up*500, Color.red, 20);
                        Debug.Log(spawnConnection.connectionTransf.name + " FLIP");
                    }
                    
                   

                    bool checkSpawnAreaIsClear = true;
                    if (checkConnection)
                    {
                        checkSpawnAreaIsClear = CheckSpawnAreaIsClear_WithSpawn(tileToSpawn, pos, rot, spawnConnection_Tile);
                    }
                    
                    if ( checkSpawnAreaIsClear)
                    {
                        var gp = SpawnTilePrefab(tileToSpawn, pos, rot);


                        if ( gp != null )
                        {
                            SceneBuilder_Tile spawnedTile = gp.GetComponent<SceneBuilder_Tile> ( );
                            
                            spawnedTile.CompoundBounds();
                            spawnedTile.Bounds.size -= Vector3.one * 3; //*= 0.8f;
                            spawnedBounds.Add( new BoundsWithObject(){bounds = spawnedTile.Bounds, gameObject = spawnedTile.gameObject} );
                            
                            
                            var openTileConnections = spawnedTile.connectionsList.Where(v=>v.IsOpened).ToList();
                            if ( openTileConnections.Count>0)
                            {
                                var connection = /*spawnedTile.connectionsList.Where(v=>v.IsOpened)*/
                                    openTileConnections.OrderBy(v =>
                                        Vector3.Distance(v.connectionTransf.position,
                                            spawnConnection.connectionTransf.position)).First();//spawnedTile.connectionsList[j];
                                connection.CloseConnection ( spawnConnection );
                                spawnConnection.CloseConnection(connection);
                                //spawnConnection.IsOpened = false;
                            }

                            if (tileToSpawn.isUnique)
                            {
                                TilesList.Remove(tileToSpawn);
                            }
                            
                            
                            //INSERT SIGNATURE
                            TileSignature tileSignature = gp.GetComponent<TileSignature>();
                            if (LastMazeGenerator != null && tileSignature != null)
                            {
                                bool insertionResult = LastMazeGenerator.InsertSignature_FromSceneObject(tileSignature);
                                gp.transform.SetParent(LastMazeGenerator.transform);

                                if (insertionResult == false)
                                {
                                    DestroyImmediate(gp.gameObject);
                                }

                            }
                            
                            return spawnedTile;
                        }
                    }
                    else
                    {
                        badTiles.Add( tileToSpawn);
                    }


                }
            }


        }
        Debug.Log("No fitting connections ", spawnConnection.connectionTransf);
        //spawnConnection.CloseConnection(/*spawnConnection*/null); //this will prevent deadEnd spawning
        
        
        return null;
    }



    private Transform SpawnTilePrefab(SceneBuilder_Tile tileToSpawn, Vector3 pos, Quaternion rot)
    {
        Transform gp = null;

        #region SPAWN

        if (Application.isPlaying)
        {
            gp = Instantiate(tileToSpawn.transform, pos, rot) as Transform;
        }
        else
        {
#if UNITY_EDITOR
            gp = PrefabUtility.InstantiatePrefab(tileToSpawn.transform, transform) as Transform;
            if (gp != null)
            {
                gp.transform.position = pos;
                gp.transform.rotation = rot;
            }
#endif
        }

        #endregion

        return gp;
    }

    #endregion
    


    #region CheckSPawnArea

    private List<Bounds> checkBoundsList = new List<Bounds>();
    private bool CheckSpawnAreaIsClear(Vector3 size, Transform rootConnect, Vector3 centerShift, Vector3 boundCenterShift, Quaternion rot/*, Vector3 spawnPos*/)
    {
        Vector3 castPos = Vector3.zero;
        rootConnect.localScale = Vector3.one;

        centerShift = rot * centerShift;
        centerShift.x = 0;
        //centerShift.z *= 1.5f;
        
        
        bool retVal = true;
        
        castPos = rootConnect.TransformPoint (  centerShift ) - boundCenterShift;
        Debug.DrawRay (  castPos , Vector3.down * 50, Color.magenta, 20, false );

        var connectionRootGO = rootConnect.GetComponentInParent<SceneBuilder_Tile>().gameObject;
        if (connectionRootGO == null) return false;
        
        
        var checkBounds = new Bounds(castPos, rot * size  );

        
        if (spawnedBounds.Any(v =>
            {
                if ( connectionRootGO == v.gameObject ) return false;
                
                return v.bounds.Intersects(checkBounds);
            })
        )
        {
            retVal = false;
            //Debug.DrawRay (  castPos , Vector3.up * 30, Color.red, 20, false );
        }
        
        if(retVal==true) checkBoundsList.Add(checkBounds);

        return retVal;
    }
    
    private bool CheckSpawnAreaIsClear_WithSpawn(SceneBuilder_Tile tileToSpawn, Vector3 pos, Quaternion rot, GameObject spawnConnection_Tile)
    {
        bool checkSpawnAreaIsClear = true;


        var spawnedTile = SpawnTilePrefab(tileToSpawn, pos, rot);
        SceneBuilder_Tile spawnedTile_comp = spawnedTile.GetComponent<SceneBuilder_Tile>();

        spawnedTile_comp.CompoundBounds();
        Bounds bounds = spawnedTile_comp.Bounds;
        bounds.size -= Vector3.one*3;


        if (spawnedBounds.Any(v =>
            {
                if (spawnConnection_Tile == v.gameObject) return false;

                return v.bounds.Intersects(bounds);
            })
        )
        {
            checkSpawnAreaIsClear = false;
            //Debug.DrawRay (  castPos , Vector3.up * 30, Color.red, 20, false );
        }

        if (LastMazeGenerator != null)
        {
            List<PathNode> pathToTarget = LastMazeGenerator.GetPathToTarget();
            for (var i = 0; i < pathToTarget.Count; i++)
            {
                PathNode pn = pathToTarget[i];
                if (bounds.Contains(pn.node.worldPos + Vector3.up*5))
                {
                    checkSpawnAreaIsClear = false;
                    break;
                }
            }
        }
        

        //if (checkSpawnAreaIsClear == true)
        {
            checkBoundsList.Add(bounds);
        }

        DestroyImmediate(spawnedTile.gameObject);
        return checkSpawnAreaIsClear;
    }

    #endregion



    #region Utils
    
    
    public bool drawGizmos = true;
    public bool drawGizmos_spawnCheckBounds = true;

    void OnDrawGizmos/*Selected*/ ()
    {
        if (drawGizmos)
        {

            Gizmos.color = new Color(1, 0, 0, 0.5F);

            foreach (var b in spawnedBounds)
            {
                var bb = b.bounds;
                Gizmos.DrawCube(bb.center, bb.size);
            }
        }


        //last check bounds
        if (drawGizmos_spawnCheckBounds)
        {
            Gizmos.color = new Color ( 1, 1, 0, 0.15F );
            
            foreach (Bounds b in checkBoundsList)
            {
                //Gizmos.color = new Color ( Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 0.5F );
                //Gizmos.DrawCube ( b.center, b.size );
                Gizmos.DrawWireCube( b.center, b.size );

            }
        }

    }
    
    

    [ContextMenu("DestroyChilds")]
    public void DestroyChilds()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }

    public void ClearSpawnedBounds()
    {
        spawnedBounds.Clear();
        checkBoundsList.Clear();
    }
    
    
    

    #region ClearCosoleLog

    [ContextMenu("ClearConsole")]
    void ClearConsole()
    {
#if UNITY_EDITOR
        if (Application.isPlaying == false)
        {
            //Console.Clear();
            //Debug.ClearDeveloperConsole();
            ClearLog();
        }
#endif
    }
#if UNITY_EDITOR
    void ClearLog()
    {
        var assembly = Assembly.GetAssembly(typeof(Editor));
        if (assembly != null)
        {
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            if (method != null)
            {
                method.Invoke(new object(), null);
            }
        }
    }
#endif

#endregion

    #endregion

}



#if UNITY_EDITOR
[CustomEditor ( typeof ( SceneBuilder ) )]
[CanEditMultipleObjects]
public class SceneBuilder_Editor : Editor
{
    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ( );

        if ( GUILayout.Button ( "Generate" ) )
        {
            SceneBuilder sceneBuilder = (SceneBuilder)target;
            sceneBuilder.Generate();
        }
        if ( GUILayout.Button ( "DestroyChilds" ) )
        {
            SceneBuilder sceneBuilder = (SceneBuilder)target;
            sceneBuilder.DestroyChilds();
            sceneBuilder.ClearSpawnedBounds();
            sceneBuilder.currPlacedTilesCount = 0;
        }

    }
}
#endif