using UnityEngine;
using System.Collections;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;

[CanEditMultipleObjects]
#endif
public class CityGen_Tile : MonoBehaviour
{
    #region Variables

    public CityGenTyleFacingType[] facingTypes = new CityGenTyleFacingType[1];


    [Header("Floors range")] public int minFloor = 0;
    public int maxFloor = 1;

    [Header("Columns range. Will check every n times")]
    //public int excludeEveryColumns = 4;
    public int[] everyNColumns = {1, 2, 3};
    public int priority = 1;


    [Header("Tile size")] 
    public int rawSize = 1;
    public int columnSize = 1;
    //public int columnSize_matrixOverride = 0;
    public float minDistanceToNewInstance = -1;


    [HideInInspector] public Bounds bounds;
    //public Vector2 tileSize = new Vector2(4, 5);
    public bool canBeCornerElement = true;
    public bool canBeLastFloor = true;
    public bool canBePreLastFloor = false;

    public bool compoundBounds = true;

    #endregion


/*	void Start () {
	    //print("Spawned!!!");
	    foreach (CityGen_TileSpawner spawner in GetComponentsInChildren<CityGen_TileSpawner>())
	    {
	        spawner.Spawn();
	    }
	}*/

    void OnEnable()
    {
        DestroyImmediate(this);
    }


    public bool CheckFloorN(int floorN)
    {
        bool result = floorN >= minFloor && floorN <= maxFloor;

        //print(name + " CheckFloor n = " + floorN + " result = " + result);

        return result;
    }

    public bool CheckColumn(int columnN)
    {
        bool result = false;

        foreach (int i in everyNColumns)
        {
            if (columnN%i == 0)
            {
/*                if (columnN == excludeEveryColumns && excludeEveryColumns > -1)
                {
                    result = false;
                    break;
                }*/

                result = true;
                break;
            }
        }

        //print(name+" CheckColumn n = "+columnN+" result = "+result);

        return result;
    }

    public bool CheckFacingType(CityGenTyleFacingType facingType)
    {
        return facingTypes.Contains(facingType)|| facingTypes.Contains(CityGenTyleFacingType.any);
    }

}


public enum CityGenTyleFacingType
{
    frontSide,
    sideWall,
    backSide,
    roof,
    any
}