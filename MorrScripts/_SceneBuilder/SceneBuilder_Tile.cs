﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[SelectionBase]
public class SceneBuilder_Tile : MonoBehaviour
{
    public int spawnRatio = 1;
    public bool deadEnd = false;

    //public static List<SceneBuilder_Tile> tylesList = new List<SceneBuilder_Tile>(); 

    public List<TileConnection> connectionsList = new List<TileConnection>(){new TileConnection(), new TileConnection()};
    public Bounds Bounds { get; set; }


    // Use this for initialization
/*	void Enable () {
		//if(tylesList.Contains(this)) tylesList.Add(this);
	}*/


/*    private void Update()
    {
        foreach (TileConnection connection in connectionsList.Where(v=>!v.IsOpened))
        {
            if (connection.linkedToTransf != null)
                Debug.DrawLine ( connection.connectionTransf.position, connection.linkedToTransf.position, Color.yellow );
        }
    }*/
/*    void OnDrawGizmosSelected ()
    {
        Gizmos.color = new Color ( 1, 0, 0, 0.5F );
        Gizmos.DrawCube ( bounds.center, bounds.size );
    }*/




    public void CompoundBounds ( GameObject go )
    {

        Bounds = new Bounds ( );
        Renderer[] renderers = go.GetComponentsInChildren<Renderer> ( );

        foreach ( Renderer renderer1 in renderers )
        {
            if ( Bounds.extents == Vector3.zero )
            {
                Bounds = renderer1.bounds;
                continue;
            }

            Bounds.Encapsulate ( renderer1.bounds );
        }

        //bounds.extents *= 1.3f;
        // bounds = go.GetComponent<Renderer>().bounds;
        //bounds = bounds;
    }

}




#region data

[Serializable]
public class TileConnection
{
    #region Vars

/*    public enum TileConnectionType
    {
        doorWay,
        metro,
        streetRoad
    }*/
    
    public Transform connectionTransf;
    public /*TileConnectionType*/string connectionType = "door";

    public Transform LinkedToTransf { get; set; }


    #region isOpen

    public bool IsOpened
    {
        get { return isOpened; }
        set { isOpened = value; }
    }

    private bool isOpened = true;

    #endregion

    #endregion



    public void CloseConnection ( TileConnection link )
    {
        IsOpened = false;
        link.IsOpened = false;
        LinkedToTransf = link.connectionTransf;
        link.LinkedToTransf = connectionTransf;
    }

    public Vector3 InverseCoonectionPos (Vector3 pos, Quaternion rot)
    {
        Vector3 vectDiff = connectionTransf.root.InverseTransformPoint(connectionTransf.position);


        Vector3 retVal = rot * vectDiff;
        retVal = pos - retVal;


        return retVal;
    }
}




#endregion


#if UNITY_EDITOR
[CustomEditor ( typeof ( SceneBuilder_Tile ) )]
[CanEditMultipleObjects]
public class SceneBuilder_Tile_Editor : Editor
{
    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ( );

        //CityGen_TileSpawner myScript = (CityGen_TileSpawner)target;
/*        if ( GUILayout.Button ( "Calc Bounds" ) )
        {
            //myScript.Spawn();
            foreach ( var targ in targets )
            {
                SceneBuilder_Tile obj = ( (SceneBuilder_Tile)targ );
                if ( obj != null ) obj.CompoundBounds ( obj.gameObject );
            }
        }*/

    }
}
#endif