using UnityEngine;
using System.Collections;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif



[ExecuteInEditMode]
public class CityGen_GenerateLOD : MonoBehaviour
{
//#if UNITY_EDITOR

    //public LayerMask excludeLayerFromRender;
    private Shader unlitShader;
    private RenderTexture renderTexture;
    private Texture2D texture;

    public bool renderWithUnlitShader = true;
    public Vector2 renderTextureDimensions = Vector2.one*256;

    private Camera camera;

    private Renderer thisRenderer;
    private Bounds bounds;

    private float zOffset = 0;

    public LODGroup lodGroup;


/*    private void OnEnable()
    {
        //Debug.Log("GEnerate lod on enable!");

        StartCoroutine(Init());
    }*/


    public void Generate(/*LODGroup lodGroupToSet*/)
    {
        //lodGroup = lodGroupToSet;
        gameObject.SetActive(true);
        StartCoroutine(Init());
    }

    public IEnumerator Init()
    {
        //print("GenerateLOD INIT");
        if (lodGroup != null)
        {
            lodGroup.enabled = false;

            camera = GetComponentInChildren<Camera>();
            camera.enabled = false;

            //Debug.Log("GEnerate lod start!");

            yield return Random.Range(0.01f, 1f);

            //Debug.Log("GEnerate lod!");

            Transform tempParent = transform.parent;
            transform.SetParent(null);

            unlitShader = Shader.Find( /*"Unlit/Texture"*/  "Unlit/Transparent Cutout");
            renderTexture = new RenderTexture(
                (int)renderTextureDimensions.x,
                (int)renderTextureDimensions.y,
                0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default
                );
            texture = new Texture2D((int)renderTextureDimensions.x, (int)renderTextureDimensions.y, TextureFormat.ARGB32, true, true);

            thisRenderer = GetComponent<Renderer>();

            //thisRenderer.material.mainTexture = renderTexture;


            //CAmera positioning
            camera.enabled = true;

            zOffset = camera.transform.localPosition.z;
            bounds = thisRenderer.bounds;
            camera.transform.position = bounds.center;//zOffset;
            camera.transform.localPosition = new Vector3( 
                camera.transform.localPosition.x, 
                camera.transform.localPosition.y, 
                //bounds.size.z/transform.localScale.z + 2.5f
                5f / transform.localScale.z
                );

            camera.aspect = transform.lossyScale.x / transform.lossyScale.y;
            //bounds.size.x / bounds.size.y;

            camera.orthographicSize = (transform.lossyScale.y / 2f);
            camera.targetTexture = renderTexture;
            camera.farClipPlane = Vector3.Distance(bounds.center, camera.transform.position)*1.5f /*/ (transform.localScale.z*10)*/;


            //RENDER
            RenderTexture.active = renderTexture;

            if (renderWithUnlitShader) camera.SetReplacementShader(unlitShader, "");
            //camera.RenderWithShader(unlitShader, "");
            camera.Render();


            texture.ReadPixels( 
                new Rect(Vector2.zero, renderTextureDimensions)
                ,0,0 
                );
            texture.Apply();



            //SAVE TEXTURE To FILE
#if UNITY_EDITOR
            string lodTexturesFolderPath = "Z_GeneratedLOD_Textures";
            if (!AssetDatabase.IsValidFolder("Assets/" + lodTexturesFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", lodTexturesFolderPath);
            }
            AssetDatabase.Refresh();

            byte[] pngData = texture.EncodeToPNG();

            string texturePath = AssetDatabase.GenerateUniqueAssetPath("Assets/" + lodTexturesFolderPath + "/Texture.png");

            if (pngData != null)
                File.WriteAllBytes(
                    texturePath,
                    pngData
                );



            AssetDatabase.Refresh();



            string newAssetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/" + lodTexturesFolderPath + "/New Material.mat");
            AssetDatabase.CreateAsset(new Material(Shader.Find(/*"Unlit/Texture"*/"Standard")), newAssetPath);

            Material material = (Material)(AssetDatabase.LoadAssetAtPath(newAssetPath, typeof(Material)));
        
            Texture2D newTexture = (Texture2D)( AssetDatabase.LoadAssetAtPath(
                        //"Assets/texture.png",
                        texturePath,
                        typeof(Texture2D)
                    )
                );
            material.mainTexture = newTexture;
            material.SetTexture("_EmissionMap", newTexture);



            //ASSIGN ASSET TO MAT
            material.SetFloat("_Glossiness", 0);
            material.SetColor("_EmissionColor", new Color(1,1,1,1)*1);
            material.EnableKeyword("_EMISSION");

            thisRenderer.material = material;
#endif

            transform.SetParent(tempParent);

            //DESTROY!!!
            RenderTexture.active = null;
            camera.targetTexture = null;
            camera.enabled = false;
            DestroyImmediate(texture);
            DestroyImmediate(renderTexture);
            //DestroyImmediate(camera.gameObject);

            //DestroyImmediate(this);
            //gameObject.SetActive(false);
            lodGroup.enabled = true;
        }
    }

//#endif
}