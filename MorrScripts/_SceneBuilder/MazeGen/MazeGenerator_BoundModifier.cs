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

            if (_mazeGenerator != null && setFloorSlectionFromThis)
            {
                int floor = Mathf.CeilToInt(_mazeGenerator.gridSize.y/_mazeGenerator.stepDist/2) + 
                            Mathf.CeilToInt((bounds.center.y + bounds.extents.y) / _mazeGenerator.stepDist);
                _mazeGenerator.drawPathLinksSettings.Floor = floor -1;

                _mazeGenerator.drawPathLinksSettings.floorRange = Mathf.CeilToInt( bounds.size.y / _mazeGenerator.stepDist);
            }
            
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

    public bool setFloorSlectionFromThis = false;
    
    
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
    private static MazeGenerator _mazeGenerator;

    #endregion


    private void OnEnable()
    {
        _allModifiers.Add(this);
        signatureInsertion.Init();

        if (_mazeGenerator == null)
        {
            _mazeGenerator = FindObjectOfType<MazeGenerator>();
        }
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
            var generatorBoundModifiers = _allModifiers
                .Where(m=>m.CurrModifierType!=ModifierType.exclude).ToList();
            
            if (generatorBoundModifiers.Count() == 0) return true;
            return generatorBoundModifiers.Any(bm => bm.Bounds.Contains(pos));
        }

        return true;
    }

    public static bool IsAnyIncludeVolumeExists()
    {
        return _allModifiers.Any(m => m.CurrModifierType != ModifierType.exclude);
    }
    
    public static List<MazeGenerator_BoundModifier> GetCollectionModifiersFromPos(Vector3 pos)
    {
        if (_allModifiers.Count > 0)
        {
            return _allModifiers
                .Where(m=>m.CurrModifierType!=ModifierType.exclude)
                .Where(bm => bm.Bounds.Contains(pos)).ToList();
        }

        return new List<MazeGenerator_BoundModifier>();
    }

    

    
    
    private void OnDrawGizmos()
    {
        var color = new Color(1,1,1, 0.3f);
        if (_mazeGenerator != null)
        {
            color = CurrModifierType != MazeGenerator_BoundModifier.ModifierType.exclude ?
                _mazeGenerator.drawPathLinksSettings.modifierVolumesColor 
                : _mazeGenerator.drawPathLinksSettings.modifierVolumesColor_exclude;
        }

        Gizmos.color = color;

        Gizmos.DrawCube(Bounds.center, Bounds.size);

    }

}
