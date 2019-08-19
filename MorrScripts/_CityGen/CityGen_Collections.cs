using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProceduralToolkit;

public class CityGen_Collections : MonoBehaviour
{
    [Serializable]
    public struct CityGenCollection
    {
        public string name;

        //public Transform[] prefabs;
        public CityGen_Tile[] tiles;
    }


    public static CityGen_Collections instance;

    public List<CityGenCollection> collections = new List<CityGenCollection>(); 



	// Use this for initialization
	void Awake ()
	{
	    instance = this;
	}


    public Transform GetRandomPrefab(string collectionName)
    {
        if (collections.Count(v => v.name == collectionName) > 0)
        {
            CityGenCollection collection = collections.FirstOrDefault(v => v.name.Contains(collectionName) );

            return collection.tiles.GetRandom().transform;
        }

        return null;
    }

    public CityGen_Tile[] GetCollection(string collectionName)
    {
        List<CityGen_Tile> tiles = new List<CityGen_Tile>();
        if (collections.Count(v => v.name == collectionName) > 0)
        {
            CityGenCollection collection = collections.FirstOrDefault(v => v.name.Contains(collectionName));

/*            foreach (var tr in collection.tiles)
            {
                //if (tr.GetComponent<CityGen_Tile>() != null)
                {
                    tiles.Add( tr.GetComponent<CityGen_Tile>());
                }
            }*/

            return collection.tiles;

            //return tiles.ToArray();
        }

        return tiles.ToArray();
    }

}


