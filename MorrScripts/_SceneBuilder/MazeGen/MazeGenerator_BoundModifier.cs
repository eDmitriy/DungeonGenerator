using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


[ExecuteInEditMode]
public class MazeGenerator_BoundModifier : MonoBehaviour
{
    #region Vars

    private BoxCollider boxCollider = null;
    public Bounds bounds;
    public Bounds Bounds
    {
        get
        {
            bounds.center = transform.position;
            bounds.size =  new Vector3(
                transform.localScale.x, 
                transform.localScale.y, 
                transform.localScale.z
            );

            return bounds;
        }
        //set { bounds = value; }
    }

    
    
    
    public enum ModifierType
    {
        exclude, include, setCollection
    }


    public ModifierType currModifierType = ModifierType.exclude;
    public MazeGenerator.SignatureInsertion signatureInsertion = new MazeGenerator.SignatureInsertion();
    
    
    
    public ModifierType CurrModifierType
    {
        get { return currModifierType; }
        set { currModifierType = value; }
    }

    public MazeGenerator.SignatureInsertion SignatureInsertion
    {
        get { return signatureInsertion; }
        set { signatureInsertion = value; }
    }

    
    public static List<MazeGenerator_BoundModifier> _allModifiers = new List<MazeGenerator_BoundModifier>();
    
    #endregion


    private void OnEnable()
    {
        _allModifiers.Add(this);
    }

    private void OnDisable()
    {
        _allModifiers.Remove(this);
    }


    public void Init()
    {
        //bounds = boxCollider.bounds;
/*        bounds.center = transform.position;
        bounds.size =  new Vector3(
            transform.localScale.x, 
            transform.localScale.y, 
            transform.localScale.z
        );*/
        
        signatureInsertion.Init();
    }

    
    

    public static bool IsExcludeBoundsContains(Vector3 pos)
    {
        if (_allModifiers.Count > 0)
        {
            return _allModifiers
                .Where(m=>m.CurrModifierType==ModifierType.exclude)
                .Any(bm => bm.Bounds.Contains(pos));
        }

        return false;
    }
    public static bool IsIncludeBoundsContains(Vector3 pos)
    {
        if (_allModifiers.Count > 0)
        {
            return _allModifiers
                .Where(m=>m.CurrModifierType==ModifierType.include)
                .Any(bm => bm.Bounds.Contains(pos));
        }

        return false;
    }
    
    public static List<MazeGenerator_BoundModifier> GetCollectionModifiersFromPos(Vector3 pos)
    {
        if (_allModifiers.Count > 0)
        {
            return _allModifiers
                .Where(m=>m.CurrModifierType==ModifierType.setCollection || m.CurrModifierType == ModifierType.include)
                .Where(bm => bm.Bounds.Contains(pos)).ToList();
        }

        return new List<MazeGenerator_BoundModifier>();
    }
    
/*    public static List<MazeGenerator_BoundModifier> GetIncludeodifiersFromPos(Vector3 pos)
    {
        if (_allModifiers.Count > 0)
        {
            return _allModifiers
                .Where(m=>m.CurrModifierType == ModifierType.include)
                .Where(bm => bm.Bounds.Contains(pos)).ToList();
        }

        return new List<MazeGenerator_BoundModifier>();
    }*/
    

}
