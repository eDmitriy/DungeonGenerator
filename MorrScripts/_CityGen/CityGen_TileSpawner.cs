using UnityEngine;
using System.Collections;
//using ProceduralToolkit;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CityGen_TileSpawner : MonoBehaviour
{
//#if UNITY_EDITOR

    //public Transform[] prefabs = new Transform[0];
    public string collectionName = "";
    public bool destroyThis = true;
    public bool rescale = true;
    public bool rescale2 = true;

    public bool useRaycast = false;

    public float heightShift = 0f;

    private bool alreadySpawned = false;


    private void Start()
    {
        if(!alreadySpawned) Spawn();
    }

    public GameObject Spawn()
    {
        //CityGen_Collections cityGenCollections = GameObject.FindObjectOfType<CityGen_Collections>();
        CityGen_Collections cityGenCollections = Resources.LoadAll<CityGen_Collections>("SB_Collections")[0];

        Transform targetPrefab = cityGenCollections.GetRandomPrefab(collectionName);
        if(targetPrefab==null 
            || targetPrefab.name == name //ANTI RECURSION
            ) return null;

        //save curr parent and set transform parent to null 
        Transform startParent = transform.parent;
        transform.parent = null;
        transform.SetParent(null);
        Quaternion startRot = transform.rotation;
        transform.rotation = Quaternion.LookRotation(Vector3.forward);


        RaycastHit hit;
        Physics.Raycast ( transform.position + Vector3.up * 10, Vector3.down * 50, out hit );


        //SPAWN
        Transform go = null;
        if (Application.isEditor == false)
        {
            /*Transform*/ go = Instantiate(
                targetPrefab, 
                transform.position, 
                //Quaternion.identity
                transform.rotation
            ) as Transform;
        }
        else
        {
#if UNITY_EDITOR
            /*Transform*/ go = PrefabUtility.InstantiatePrefab(targetPrefab) as Transform;
#endif
        }

        if (go != null)
        {
            go.position = transform.position;
            go.rotation = transform.rotation;
            //go.parent = transform;


            Bounds targetBounds = CompoundBounds(this.gameObject);
            Bounds targetPrefabBounds = CompoundBounds(go.gameObject);

            Vector3 targetScale = new Vector3(
                1 * (targetBounds.size.x / Mathf.Clamp(targetPrefabBounds.size.x, 0.01f, float.MaxValue)),
                1 * (targetBounds.size.y / Mathf.Clamp(targetPrefabBounds.size.y, 0.01f, float.MaxValue)),
                1 * (targetBounds.size.z / Mathf.Clamp(targetPrefabBounds.size.z, 0.01f, float.MaxValue))
            );
/*        targetScale = startParent.rotation*targetScale;
        if (targetScale.z < 0) targetScale.z *= -1; */

            if (rescale) go.localScale = targetScale;
            go.position = transform.position;
            //go.position += targetBounds.center - targetPrefabBounds.center;
            if (rescale)
            {
                go.position += go.up * (targetBounds.center - targetPrefabBounds.center).y * go.localScale.y;
                go.position += go.right * (targetBounds.center - targetPrefabBounds.center).x * go.localScale.x;
                go.position += go.forward * (targetBounds.center - targetPrefabBounds.center).z * go.localScale.z;
            }

            //go.transform.rotation = transform.rotation;

            //transform.parent = startParent;
            go.parent = transform;
            transform.rotation = startRot;
            go.parent = startParent;
            transform.parent = startParent;


            if (useRaycast && hit.collider != null)
            {
                go.position = hit.point;
            }


            //Spawn detail immideatly, dont wait for next frame
            CityGen_TileSpawner[] detailSpawners = go.GetComponentsInChildren<CityGen_TileSpawner>();
            if (detailSpawners.Length > 0)
            {
                //print(detailSpawners.Length);
                foreach (CityGen_TileSpawner detailSpawner in detailSpawners)
                {
                    if (detailSpawner != null) detailSpawner.Spawn();
                }
            }
            /*CityGen_GenerateLOD lodGenerator = go.GetComponentInChildren<CityGen_GenerateLOD>();
        if(lodGenerator) lodGenerator.Generate();*/


            if (destroyThis)
            {
                DestroyImmediate(this.gameObject);
            }
            else
            {
            }

            if (!destroyThis && rescale2)
            {
                go.parent = transform;
                go.transform.localPosition = Vector3.zero;
                go.localScale = new Vector3(go.localScale.x, go.localScale.y, 0.1f);

                go.position += go.right * (targetBounds.center - targetPrefabBounds.center).x -
                               go.forward * go.transform.lossyScale.z / 2f;
                //go.localPosition = new Vector3(go.localPosition.x, go.localPosition.y, 0);

                //go.position += Vector3.up * (targetBounds.center - targetPrefabBounds.center).y /** go.localScale.y*/;
            }

            go.position += Vector3.up * heightShift;

            alreadySpawned = true;

            return go.gameObject;
        }
        return null;
    }

    Bounds CompoundBounds(GameObject go)
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
       // bounds = go.GetComponent<Renderer>().bounds;
        return bounds;
    }

//#endif
}


#if UNITY_EDITOR
[CustomEditor(typeof(CityGen_TileSpawner))]
[CanEditMultipleObjects]
public class CityGen_TileSpawner_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        //CityGen_TileSpawner myScript = (CityGen_TileSpawner)target;
        if(GUILayout.Button("Spawn!"))
        {
            //myScript.Spawn();
            foreach ( var targ in targets )
            {
                //generator.FitTerrainHeightToEdges();
                ( (CityGen_TileSpawner)targ ).Spawn( );
            }
        }
    }
}
#endif
 