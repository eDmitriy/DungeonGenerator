using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CityGen_ExcludeBound : MonoBehaviour
{
    Bounds bounds;
    public static List<CityGen_ExcludeBound> allCityGen_ExcludeBounds = new List<CityGen_ExcludeBound>();

    public Bounds Bounds
    {
        get
        {
            if (bounds.extents.x < 0.1f)
            {
                //bounds = Arkham_FlowchartSelector.CompoundBounds(gameObject);
            }

            return bounds;
        }
    }


    private void OnEnable()
    {
        if( !allCityGen_ExcludeBounds.Contains(this) ) allCityGen_ExcludeBounds.Add(this);
    }

    private void OnDisable()
    {
        if( allCityGen_ExcludeBounds.Contains( this ) ) allCityGen_ExcludeBounds.Remove( this );
    }


}
