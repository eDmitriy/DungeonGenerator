﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    public float waitSecAfterSpawn = 0.1f;

    public int maxTilesToPlace = 10;
    private int currPlacedTilesCount = 0;

    public SceneBuilder_Tile startTile;
    public bool generateOnStart = false;

    public List<TileConnection> openConnections = new List<TileConnection>();
    List<Bounds> spawnedBounds = new List<Bounds>();
    
    
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

    #endregion



    void Start()
    {
        if (generateOnStart)
        {
            //StartCoroutine(GenRoutine());
            Generate();
        }
    }
    

    
    [ContextMenu("Generate")]
    public void Generate()
    {
        DestroyChilds();
        
        
        #region Init data

        TilesList.Clear();
        spawnedBounds.Clear();
        
        foreach (var tile in tilesList_withRatio)
        {
            for (int i = 0; i < tile.spawnRatio; i++)
            {
                TilesList.Add(tile);
            }
        }
        
        //TilesList = Resources.LoadAll<SceneBuilder_Tile> ( "SceneBuilder" ).ToList();
        if (TilesList.Count == 0) return;
        
        openConnections.Clear();
        currPlacedTilesCount = 0;
        
        foreach (TileConnection tileConnection in startTile.connectionsList)
        {
            openConnections.Add ( tileConnection );
        }

        foreach (SceneBuilder_Tile tile in TilesList)
        {
            tile.CompoundBounds(tile.gameObject);
        }

        #endregion


        //Place all tiles
        List<SceneBuilder_Tile> openTiles = TilesList.Where(v => !v.deadEnd).ToList();


        for (int i = 0; i < maxTilesToPlace; i++)
        {
            SpawningLoop( ref openTiles, true);
        }
/*        while (openConnections.Count> 0 && currPlacedTilesCount < maxTilesToPlace )
        {
            SpawningLoop( ref openTiles, true);
            //yield return new WaitForSeconds ( waitSecAfterSpawn );
        }*/



        //close all open connections for placed tiles
        List<SceneBuilder_Tile> deadEndTiles = TilesList.Where ( v => v.deadEnd ).ToList ( );

        if (deadEndTiles.Count>0)
        {
            Debug.Log("OPenConnection count for deadEnds = " + openConnections.Count);
            for (int i = 0; i < openConnections.Count; i++)
            {
                SpawningLoop ( ref deadEndTiles, false );
            }
/*            while ( openConnections.Count > 0 )
            {
                SpawningLoop ( ref deadEndTiles, false );
                //yield return new WaitForSeconds ( waitSecAfterSpawn );
            }*/
        }

#if UNITY_EDITOR
        if (Application.isPlaying==false)
        {
            //EditorUtility.SetDirty(gameObject.scene);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    
    [ContextMenu("DestroyChilds")]
    public void DestroyChilds()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }

    

/*    void OnDrawGizmosSelected ()
    {
        Gizmos.color = new Color ( 1, 0, 0, 0.5F );

        foreach (Bounds b in bounds)
        {
            Gizmos.DrawCube ( b.center, b.size );

        }
    }*/



    private void SpawningLoop ( ref List<SceneBuilder_Tile> tiles, bool checkConnection )
    {
        if(openConnections.Count==0) return;
        
        
        int randomIndex = Random.Range ( 0, openConnections.Count );
        TileConnection connection = openConnections[randomIndex];

        SceneBuilder_Tile newTile = SpawnTile ( connection, ref tiles, checkConnection );



        if ( newTile != null )
        {
            connection.IsOpened = false;
            openConnections.RemoveAt ( randomIndex );

            currPlacedTilesCount++;

            foreach ( TileConnection tileConnection in newTile.connectionsList )
            {
                if ( tileConnection.IsOpened ) openConnections.Add ( tileConnection );
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
        tiles.Shuffle ( );

        for ( int i = 0; i < tiles.Count; i++ )
        {
            SceneBuilder_Tile tileToSpawn = tiles[ i];
            List<TileConnection> connectionsList = tileToSpawn.connectionsList.Where(v=>v.connectionType == spawnConnection.connectionType).ToList();
            if(connectionsList.Count==0) continue;
            
            connectionsList.Shuffle();

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
                    /*if (Vector3.Angle(rot * Vector3.up, Vector3.down) < 20)
                    {
                        //rot = Quaternion.Euler(rot.eulerAngles.x, -rot.eulerAngles.y, rot.eulerAngles.z);
                        rot *= Quaternion.Euler(0,0,180);
                        Debug.DrawRay(pos, Vector3.up*50, Color.red, 20);
                    }*/
                    
                    //tileConnection.IsOpened = false;
                    

                    bool checkSpawnAreaIsClear = true;
                    if (checkConnection)
                    {
                        checkSpawnAreaIsClear = CheckSpawnAreaIsClear (
                            tileToSpawn.Bounds.size,
                            spawnConnection.connectionTransf,
                            spawnConnection.connectionTransf.InverseTransformPoint ( pos ),
                            pos
                        );
                    }
                    
                    if ( checkSpawnAreaIsClear)
                    {
                        Transform gp = null;

                        #region SPAWN

                        if (Application.isPlaying)
                        {
                            gp = Instantiate ( tileToSpawn.transform, pos, rot ) as Transform;
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
                        
                        
                        if ( gp != null )
                        {
                            SceneBuilder_Tile spawnedTile = gp.GetComponent<SceneBuilder_Tile> ( );
                            //if ( spawnedTile.connectionsList.Count > j )
                            {
                                var connection = spawnedTile.connectionsList.OrderBy(v =>
                                    Vector3.Distance(v.connectionTransf.position,
                                        spawnConnection.connectionTransf.position)).First();//spawnedTile.connectionsList[j];
                                connection.CloseConnection ( spawnConnection );

                                spawnedTile.CompoundBounds(spawnedTile.gameObject);
                                spawnedBounds.Add(spawnedTile.Bounds );

                            }

                            return spawnedTile;
                        }
                    }


                }
            }


        }

        return null;
    }


    private bool CheckSpawnAreaIsClear(Vector3 size, Transform rootConnect, Vector3 centerShift, Vector3 spawnPos)
    {
        //return bounds.Any(b => !b.Intersects(bound) );

        //float step = 2;
        const float radius = 2;
        RaycastHit hit;
        Vector3 currShift = -size/2;
        //currShift.y = 0;
        Vector3 heightShift = Vector3.up*(size.y /*- radius/2*/);
        Vector3 castPos = Vector3.zero;
        //centerShift = Vector3.ClampMagnitude(centerShift, Mathf.Abs(centerShift.magnitude) - radius/2);

        //Debug.DrawRay ( rootConnect.position, Vector3.down * 50, Color.red, /*waitSecAfterSpawn*/10, false );

        rootConnect.localScale = Vector3.one;
        
        bool retVal = true;
        
        castPos = rootConnect.TransformPoint (  centerShift );
        //Debug.DrawRay (  castPos , Vector3.down * heightShift.y, Color.red, 20, false );

/*        if (Physics.BoxCast(castPos, size / 2, Vector3.down))
        {
            retVal = false;
        }*/

        Bounds checkBounds = new Bounds(castPos, rootConnect.rotation * size * 2);

        if (spawnedBounds.Any(v => v.Intersects(checkBounds)))
        {
            retVal = false;
        }

/*        for ( int i = 0; i < size.x / radius; i++ )
        {
            for ( int j = 0; j < size.z / radius; j++ )
            {
                currShift.x = Mathf.Clamp ( currShift.x, -( size.x / 2 ) + radius/4, ( size.x / 2 ) - radius/4 );
                currShift.z = Mathf.Clamp ( currShift.z, -( size.z / 2 ) + radius/4, ( size.z / 2 ) - radius/4 );

                castPos = rootConnect.TransformPoint (  centerShift + currShift );
                castPos.y = spawnPos.y;//rootConnect.position.y;

                Debug.DrawRay ( ( castPos + heightShift ), Vector3.down * heightShift.y, Color.red, /*waitSecAfterSpawn#1#20, false );

                if ( Physics.Raycast ( castPos + heightShift, Vector3.down, out hit, heightShift.y+0.5f ) )
                {
                    retVal = false;
                }
                currShift.z += radius;
            }
            currShift.z = -size.z / 2;
            currShift.x += radius;

        }*/


        return retVal;
    }
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
        }

    }
}
#endif