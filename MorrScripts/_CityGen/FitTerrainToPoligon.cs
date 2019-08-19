using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif



public class FitTerrainToPoligon : MonoBehaviour {

    #region Vars

    [Header("Terrain height fit")]
    private Terrain terrain = null;
    public float terrainCorrectionRadius = 3f;
    public float terrainCorrectionFalloffRadius = 5f;
    public float terrainCorrection_AddHeightShift = -0.5f;
/*    public int terrainShifPos_CurrLerp = 20;
    public int terrainHeightShifPos_CurrLerp = 20;*/


    //public float meshWidth = 1;

    [Header("Terrain SplatMaps")]
    public int splatMapIndex = 3;
    public float splatMapSearchDist = 1;

    List<Edge> edges = new List<Edge> ( );


    #endregion









    #region EdgesGEn

    public void MeshToQuads ( bool drawDebugLines = false )
    {
        edges.Clear();

        MeshFilter meshFilter = this.GetComponent<MeshFilter> ( );
        Mesh mesh=null;
        if ( meshFilter != null ) mesh = meshFilter.sharedMesh;

        if ( mesh == null ) return;

        List<Triangle> tris = new List<Triangle> ( );
        List<TriangleGroup> trisGroup = new List<TriangleGroup> ( );



        //CREATE TRIANGLES LIST

        #region CreateTriangle

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Transform transf = meshFilter.transform;

        for ( int i = 0; i < triangles.Length - 1; i += 3 )
        {
            List<Vector3> vertsList = new List<Vector3> ( );
            var edgesList = new List<Edge> ( );


            //VERTS
            for ( int j = 0; j < 3; j++ )
            {
                vertsList.Add ( meshFilter.transform.TransformPoint ( vertices[triangles[i + j]] ) );
            }

            //EDGES
            edgesList.Add ( new Edge (
                    transf.TransformPoint ( vertices[triangles[i]] ),
                    transf.TransformPoint ( vertices[triangles[i + 1]] )
                )
            );
            edgesList.Add ( new Edge (
                    transf.TransformPoint ( vertices[triangles[i + 1]] ),
                    transf.TransformPoint ( vertices[triangles[i + 2]] )
                )
            );
            edgesList.Add ( new Edge (
                    transf.TransformPoint ( vertices[triangles[i + 2]] ),
                    transf.TransformPoint ( vertices[triangles[i]] )
                )
            );


            //TRIS
            tris.Add (
                new Triangle ( )
                {
                    normal = Vector3.Cross ( vertsList[0] - vertsList[1], vertsList[2] - vertsList[1] ).normalized,
                    verts = vertsList.ToArray ( ),
                    edges = edgesList
                }
            );
        }

        #endregion




        //FIND TRIS WITH SIMILAR NORMALS AND Common vertices
        while ( tris.Count ( v => v.checkEnded == false ) > 0 )
        {
            var triangle = tris.FirstOrDefault ( v => v.checkEnded == false );

            if ( triangle != null )
            {
                List<Triangle> trianglesSet = new List<Triangle> ( );

                triangle.area = triangle.CalcArea ( );

                var trisGroupList = tris.Where (
                    v =>
                        v != triangle
                        && !v.checkEnded
                        && CompareVectorArrays ( triangle.verts, v.verts ) == 2
                        && Vector3.Angle ( v.normal, triangle.normal ) < 15
                );

                Triangle trFound = trisGroupList
                    .OrderBy ( v => CompareVectorArraysByLongestSyde ( triangle.verts, v.verts ) )
                    .ThenBy ( v => Vector3.Angle ( v.normal, triangle.normal ) )
                    .ThenBy ( v => Mathf.Abs ( v.CalcArea ( ) - triangle.area ) )
                    .FirstOrDefault ( );

                if ( trFound != null )
                {
                    trianglesSet.Add ( trFound );
                    trFound.checkEnded = true;
                }
                /*                foreach ( var tr in tris.Where ( v =>v!=triangle && !v.checkEnded && CompareVectorArrays ( triangle.verts, v.verts ) > 0 ) )
                                {
                                    if (Vector3.Angle(tr.normal, triangle.normal) < normalAngleTolerance
                                        && CompareVectorArrays(triangle.verts, tr.verts) == 2)
                                    {
                                        trianglesSet.Add(tr);
                                        tr.checkEnded = true;
                                        break;
                                    }
                                }*/


                if ( trianglesSet.Count > 0 )
                {
                    trianglesSet.Add ( triangle );

                    trisGroup.Add ( new TriangleGroup ( trianglesSet ) );
                }

                triangle.checkEnded = true;
            }
        }




        //SIMILAR TRIS TO QUADS, FILTER TRIS TO POINTS
        foreach ( TriangleGroup triangleGroup in trisGroup )
        {

            //FIND EDGES WITH SIMILAR DIRECTIONS AND NOT COMMON VERTICES
            List<Edge> allEdges = triangleGroup.trisList.SelectMany ( v => v.edges ).ToList ( );
            List<Edge> parallelEdges = new List<Edge> ( );

            while ( allEdges.Count ( v => !v.checkEnded ) > 0 )
            {
                Edge edge = allEdges.FirstOrDefault ( v => v.checkEnded == false );

                if ( edge != null )
                {
                    foreach ( var tr in allEdges.Where ( v => !v.checkEnded ) )
                    {
                        Vector3[] vertices1 = new[] { tr.start, tr.end };
                        Vector3[] vertices2 = new[] { edge.start, edge.end };


                        if ( Mathf.Abs ( Vector3.Dot ( ( tr.start - tr.end ).normalized, ( edge.start - edge.end ).normalized ) ) > 0.75f
                             && CompareVectorArrays ( vertices1, vertices2 ) == 0
                        )
                        {
                            parallelEdges.Add ( edge );
                            parallelEdges.Add ( tr );
                            tr.checkEnded = true;
                            edge.checkEnded = true;

                            break;
                        }
                    }
                    edge.checkEnded = true;
                }
            }

            var parallelEdgesColl = parallelEdges
                .OrderBy ( v => Vector3.Angle ( v.start - v.end, Vector3.ProjectOnPlane ( v.start - v.end, Vector3.up ) ) )
                .Take ( 2 );
            Edge edgeFound = parallelEdgesColl
                .OrderBy ( v => Vector3.Lerp ( v.start, v.end, 0.5f ).y )
                .FirstOrDefault ( );

            Edge edgeHeight = parallelEdgesColl.FirstOrDefault ( v => v != edgeFound );


            //QUAD NORMAL
            if ( edgeFound != null && edgeHeight != null )
            {
                Vector3 midPoint = Vector3.Lerp ( edgeFound.start, edgeFound.end, 0.5f );
                Vector3 midPointEnd = Vector3.Lerp ( edgeHeight.start, edgeHeight.end, 0.5f );





                //CREATE NEW EDGE
                bool isVecorsLooksInOneDirection =
                    Vector3.Dot ( ( edgeFound.start - edgeFound.end ).normalized, ( edgeHeight.start - edgeHeight.end ).normalized ) > 0;

                Edge newEdge = edgeFound;
                newEdge.startUp = isVecorsLooksInOneDirection ? edgeHeight.start : edgeHeight.end;
                newEdge.endUp = isVecorsLooksInOneDirection ? edgeHeight.end : edgeHeight.start;

                newEdge.height = Vector3.Distance ( midPoint, midPointEnd );



                if ( drawDebugLines )
                {
                    Debug.DrawRay ( midPointEnd, triangleGroup.normal * -10, Color.white, 20 );
                    Debug.DrawLine ( edgeFound.start + Vector3.up, edgeFound.end + Vector3.up, Color.blue, 10 );

                    Debug.DrawLine ( triangleGroup.GetTrisGroupMidPoint ( ), triangleGroup.GetTrisGroupMidPoint ( ) - triangleGroup.normal * 10, Color.blue, 50 );

                    Debug.DrawLine (
                        newEdge.start + triangleGroup.normal * -10,
                        newEdge.startUp + triangleGroup.normal * -10,
                        Color.blue, 50 );
                    Debug.DrawRay (
                        newEdge.startUp,
                        triangleGroup.normal * -10,
                        Color.yellow, 50 );
                    Debug.DrawRay (
                        newEdge.start,
                        triangleGroup.normal * -10,
                        Color.yellow, 50 );
                }



                //FACING NORMAL 

                #region CAlcNormals

                newEdge.facingNormal = Quaternion.LookRotation ( Vector3.Cross ( newEdge.end - newEdge.start, Vector3.up ) );


                if ( newEdge.startUp.sqrMagnitude > 0 )
                {
                    var vect = Vector3.Lerp ( newEdge.endUp, newEdge.startUp, 0.5f ) - Vector3.Lerp ( newEdge.end, newEdge.start, 0.5f );
                    newEdge.facingNormal = Quaternion.LookRotation ( Vector3.Cross ( newEdge.end - newEdge.start, vect ) );


                    //FIX FOR NORMALs POINTING DIRECT TO UP/DOWN
                    if ( Mathf.Abs ( Vector3.Dot ( Vector3.up, ( newEdge.facingNormal * Vector3.forward ).normalized ) ) > 0.99f )
                    {
                        newEdge.startUp += new Vector3 ( 0, 0.1f, 0 );
                        vect = Vector3.Lerp ( newEdge.endUp, newEdge.startUp, 0.5f ) - Vector3.Lerp ( newEdge.end, newEdge.start, 0.5f );
                        newEdge.facingNormal = Quaternion.LookRotation ( Vector3.Cross ( newEdge.end - newEdge.start, vect ) );
                    }


                }
                newEdge.facingNormal = Quaternion.LookRotation (
                    newEdge.facingNormal * Vector3.forward,
                    Quaternion.LookRotation ( newEdge.end - newEdge.start ) * Vector3.up
                );


                #endregion
                

                


                edges.Add ( newEdge );

            }
        }

    }


    int CompareVectorArrays ( Vector3[] arr1, Vector3[] arr2 )
    {
        int count = 0;

        foreach ( var vect1 in arr1 )
        {
            foreach ( var vect2 in arr2 )
            {
                if ( Vector3.SqrMagnitude ( vect1 - vect2 ) < 0.1f )
                {
                    count++;
                }

            }
        }

        return count;
    }

    float CompareVectorArraysByLongestSyde ( Vector3[] arr1, Vector3[] arr2 )
    {
        int count = 0;

        List<Vector3> edgePoints = new List<Vector3> ( );

        //CHECK IF TRIANGLE HAVE 2 points INTERSECT
        for ( var i = 0; i < arr1.Length; i++ )
        {
            var vect1 = arr1[i];
            for ( var j = 0; j < arr2.Length; j++ )
            {
                var vect2 = arr2[j];
                if ( Vector3.SqrMagnitude ( vect1 - vect2 ) < 0.1f )
                {
                    count++;

                    edgePoints.Add ( vect1 );
                }
            }
        }

        //IF POINTS INTERSECTS CHECK IF THIS EDGE IS LONGEST
        if ( count == 2 )
        {
            List<float> edgDist = new List<float> ( );
            edgDist.Add ( Vector3.Distance ( arr1[0], arr1[1] ) );
            edgDist.Add ( Vector3.Distance ( arr1[1], arr1[2] ) );
            edgDist.Add ( Vector3.Distance ( arr1[2], arr1[0] ) );

            List<float> edgDist2 = new List<float> ( );
            edgDist2.Add ( Vector3.Distance ( arr2[0], arr2[1] ) );
            edgDist2.Add ( Vector3.Distance ( arr2[1], arr2[2] ) );
            edgDist2.Add ( Vector3.Distance ( arr2[2], arr2[0] ) );

            float edgeFoundDist = Vector3.Distance ( edgePoints[0], edgePoints[1] );


            float edgesDiff = Mathf.Abs (
                Mathf.Abs ( edgDist.Max ( ) - edgeFoundDist )
                - Mathf.Abs ( edgDist2.Max ( ) - edgeFoundDist )
            );

            return edgesDiff;
            /*            if (edgesDiff < 0.1f)
                        {
                            return true;
                        }*/
        }

        return float.MaxValue;
    }

    #endregion

    public void FitTerrainHeightToEdges()
    {
        //FitTerrainHeightToEdges( gameObject );

        foreach (var child in GetComponentsInChildren<MeshFilter>())
        {
            FitTerrainHeightToEdges(child.gameObject, terrainCorrection_AddHeightShift, terrainCorrectionRadius, terrainCorrectionFalloffRadius);
        }
    }



    public static void FitTerrainHeightToEdges (GameObject go, float terrainCorrection_AddHeightShift, float terrainCorrectionRadius, float terrainCorrectionFalloffRadius)
    {
        #region SurfaceTriangles

        MeshFilter meshFilter = go.GetComponent<MeshFilter> ( );
        Mesh mesh=null;
        if( meshFilter != null ) mesh = meshFilter.sharedMesh;

        if( mesh == null ) return;

        var tris = mesh.triangles;
        var verts = mesh.vertices;

        #endregion



        #region TerrainGenVars

        TerrainData terrainData;
        int xRes, yRes, xSetPos,ySetPos;
        float[,] heights;
        Vector3 lerpPosCurr;

        var terrain = GetTerrain();
        if ( terrain == null ) return;

        
        terrainData = terrain.terrainData;
        xRes = terrainData.heightmapWidth;
        yRes = terrainData.heightmapHeight;
        float terrainPosMultX = terrainData.size.x / xRes;
        float terrainPosMultY = terrainData.size.z / yRes;

        heights = terrainData.GetHeights ( 0, 0, xRes, yRes );


        Dictionary<Vector2Int, float> directWritedHeights = new Dictionary<Vector2Int, float>();
        Dictionary<Vector2Int, float> initialHeights = new Dictionary<Vector2Int, float>();

        #endregion


        //foreach ( Edge edge in edges )
        for (int tr = 0; tr < tris.Length; tr += 3)
        {
            List<Vector3> targetPositions = new List<Vector3> ( );
            
            float currDist = 0;
            float initialHeight;//= hit.point.y/terrainData.size.y;


            #region WorldPositions from triangles

            Vector3 p1, p2, p3, center;

            if ( tris [ tr ] >= verts.Length || tris [ tr+2 ] >= verts.Length)
            {
                string trisDebug = "";
                foreach( int tri in tris )
                {
                    trisDebug += ( " Tris index = " + tri + "\n");

                }
                Debug.Log("Wrong vert index "+tr + "// Verts count = "+verts.Length + "  // Tris length = "+tris.Length + "\n"+trisDebug );

                continue;
            }


            p1 = meshFilter.transform.TransformPoint( verts [ tris[tr] ] );
            p2 = meshFilter.transform.TransformPoint( verts [ tris [ tr+1 ] ]);
            p3 = meshFilter.transform.TransformPoint( verts [ tris [ tr+2 ] ] );

/*            p1 = ( verts [ tr ] );
            p2 = ( verts [ tr + 1 ] );
            p3 = ( verts [ tr + 2 ] );*/
            center = (p1 + p2 + p3) / 3;

/*            Debug.DrawRay(p1, Vector3.up*10, Color.red, 15);
            Debug.DrawRay( p2, Vector3.up * 10, Color.red, 15 );
            Debug.DrawRay( p3, Vector3.up * 10, Color.red, 15 );*/


            float targetHeight = center.y / terrainData.size.y;
            Vector2 targetPosTSpace = terrain.transform.InverseTransformPoint ( new Vector2 (
                    center.z / terrainPosMultX,
                    center.x / terrainPosMultY
                )
            );


            foreach (var point in GetTrianglePoints(p1, p2, p3, center))
            {
                targetPositions.Add(
                    terrain.transform.InverseTransformPoint(point)
                );
            }

            //targetPositions.AddRange(GetTrianglePoints(p1, p2, p3, center));

            #endregion



            foreach ( Vector3 targetPosition in targetPositions )
            {
                //Debug.DrawRay(targetPosition, Vector3.up*50f, Color.red, 10f);

                targetHeight = ( targetPosition.y + terrainCorrection_AddHeightShift ) / terrainData.size.y;
                targetPosTSpace = new Vector2 (
                    targetPosition.z / terrainPosMultX,
                    targetPosition.x / terrainPosMultY
                );
                float searchRadius = (int)( ( terrainCorrectionRadius + terrainCorrectionFalloffRadius ) / terrainPosMultX ) * 2;
                int iMinSerach = Mathf.Clamp ( (int)( targetPosTSpace.x - searchRadius ), 0, yRes );
                int iMaxSerach = Mathf.Clamp ( (int)( targetPosTSpace.x + searchRadius ), 0, yRes );

                int iiMinSearch = Mathf.Clamp ( (int)( targetPosTSpace.y - searchRadius ), 0, xRes );
                int iiMaxSearch = Mathf.Clamp ( (int)( targetPosTSpace.y + searchRadius ), 0, xRes );




                for (int i = iMinSerach; i < iMaxSerach; i++)
                {
                    for (int ii = iiMinSearch; ii < iiMaxSearch; ii++)
                    {
                        var key = new Vector2Int(i, ii);
                        
                        if (!initialHeights.Keys.Contains(key))
                        {
                            initialHeights.Add( key, heights[i, ii]);
                        }


                        currDist = Vector2.Distance( targetPosTSpace, new Vector2( i, ii ) );

                        if (currDist < terrainCorrectionRadius)
                        {
                            //heights[i, ii] = targetHeight;

                            if (directWritedHeights.Keys.Contains(key))
                            {
                                var heightval = /*targetHeight*/Mathf.Max( targetHeight, directWritedHeights [ key ] );
                                directWritedHeights [ key] = heightval;
                                heights[i, ii] = heightval;


/*                                Vector3 p = terrain.transform.TransformPoint(
                                    new Vector3(
                                        ii * terrainPosMultX,
                                        heightval,
                                        i * terrainPosMultY)
                                    );

                                Debug.DrawRay(p, Vector3.up * 1, Color.red, 15 );*/

                            }
                            else
                            {
                                directWritedHeights.Add(key, targetHeight);
                                heights [ i, ii ] = targetHeight;
                            }
                        }

                    }
                }


                //smooth dump
/*                if (terrainCorrectionFalloffRadius > 0.01f)
                {
                    for( int i = iMinSerach; i < iMaxSerach; i++ )
                    {
                        for( int ii = iiMinSearch; ii < iiMaxSearch; ii++ )
                        {
                            //if( directWritedHeights.Count(v=> (int)v.magnitude == (int)(new Vector2Int(i, ii).magnitude)) >0) continue;
                            var key = new Vector2Int(i, ii);
                            if (directWritedHeights.Keys.Count(v => v == key) > 0)
                            {
                                if (targetHeight < directWritedHeights[key]) continue;
                            }

                            //initialHeight = heights [ i, ii ];
                            initialHeight = initialHeights[new Vector2Int(i, ii)];
                            currDist = Vector2.Distance( targetPosTSpace, new Vector2( i, ii ) );


                            if( currDist < terrainCorrectionRadius + terrainCorrectionFalloffRadius
                                /*&& currDist > terrainCorrectionRadius#1#)
                            {
                                float lerpVAl = (currDist / (terrainCorrectionRadius + terrainCorrectionFalloffRadius));
                                float currHeight = heights[i, ii];
                                var newHeight = Mathf.Lerp(
                                    targetHeight,
                                    initialHeight /*0#1#,
                                    lerpVAl
                                );

                                if (newHeight > initialHeight)
                                {
                                    newHeight = Mathf.Max( currHeight, newHeight );
                                }
                                else
                                {
                                    newHeight = Mathf.Min( currHeight, newHeight );
                                }


                                heights [i, ii] = newHeight;
                            }

                        }
                    }
                    
                }*/
            }
            


        }
        terrainData.SetHeights ( 0, 0, heights );

    }

    public static Terrain GetTerrain()
    {
        return GameObject.FindObjectOfType<Terrain>(); //gameObject.scene.GetRootGameObjects().FirstOrDefault(v => v.GetComponent<Terrain>() != null);
/*        if (terrObj == null) return true;

        terrain = terrObj.GetComponent<Terrain>();
        return false;*/
    }


    #region TriangleToPoints

    
    public static float traingleSamplingStep = 1f;
    static List<Vector3> GetTrianglePoints( Vector3 boundPoint1, Vector3 boundPoint2, Vector3 boundPoint3, Vector3 boundCenter/*, float traingleSamplingStep = 1f*/)
    {
        List<Vector3> points = new List<Vector3>();
        //Vector3 tempPoint = b.boundPoint1;
        Vector3 p1, p2, p3, center;

        p1 = boundPoint1;
        p2 = boundPoint2;
        p3 = boundPoint3;
        center = boundCenter;


        points.Add( center );
        points.Add( p1 );
        points.Add( p2 );
        points.Add( p3 );


        float dist = float.MaxValue;
        while(dist > traingleSamplingStep )
        {
            points.AddRange( GetTrianglePoints( p1, p2, p3 ) );

            p1 = Vector3.MoveTowards( p1, center, traingleSamplingStep );
            p2 = Vector3.MoveTowards( p2, center, traingleSamplingStep );
            p3 = Vector3.MoveTowards( p3, center, traingleSamplingStep );


            dist = Vector3.Distance( p1, center );
            if( dist < traingleSamplingStep )
            {
                points.Add( center );
                break;
            }

        }
        

        return points;
    }


    static List<Vector3> GetTrianglePoints( Vector3 p1, Vector3 p2, Vector3 p3 )
    {
        List<Vector3> points = new List<Vector3>();
        Vector3 tempPoint = p1;
        float dist = 0;

        dist = float.MaxValue;
        while( dist > traingleSamplingStep )
        {
            points.Add( tempPoint );
            tempPoint = Vector3.MoveTowards( tempPoint/*p1*/, p2, traingleSamplingStep );
            dist = Vector3.Distance( tempPoint, p2 );
        }

        tempPoint = p2;
        dist = float.MaxValue;
        while( dist > traingleSamplingStep )
        {
            points.Add( tempPoint );
            tempPoint = Vector3.MoveTowards( tempPoint/*p2*/, p3, traingleSamplingStep );
            dist = Vector3.Distance( tempPoint, p3 );
        }

        tempPoint = p3;
        dist = float.MaxValue;
        while( dist > traingleSamplingStep )
        {
            points.Add( tempPoint );
            tempPoint = Vector3.MoveTowards( tempPoint/*p3*/, p1, traingleSamplingStep );
            dist = Vector3.Distance( tempPoint, p1 );
        }

        return points;
    }

    #endregion



    public void SetTerrainGrassToZero()
    {
        foreach (var child in GetComponentsInChildren<MeshFilter>())
        {
            SetTerrainGrassToZero(child.gameObject, splatMapSearchDist, terrainCorrectionRadius);
        }
    }


    public static void SetTerrainGrassToZero (GameObject go, float splatMapSearchDist, float terrainCorrectionRadius)
    {
        #region SurfaceTriangles

        MeshFilter meshFilter = go.GetComponent<MeshFilter> ( );
        Mesh mesh=null;
        if( meshFilter != null ) mesh = meshFilter.sharedMesh;

        if( mesh == null ) return;

        var tris = mesh.triangles;
        var verts = mesh.vertices;

        #endregion



        #region TerrainGenVars

        TerrainData terrainData;
        int xRes, yRes, xSetPos,ySetPos;
        //float[,,] alphamaps;
        Vector3 lerpPosCurr;

        var terrain = GetTerrain();
        if ( terrain == null ) return;

        
        terrainData = terrain.terrainData;
        xRes = terrainData.detailWidth;
        yRes = terrainData.detailHeight;
        float terrainPosMultX = terrainData.size.x / xRes;
        float terrainPosMultY = terrainData.size.z / yRes;

        
        #endregion


        var detailPrototypesLength = terrainData.detailPrototypes.Length;

        for (int a = 0; a < detailPrototypesLength; a++)
        {
            int[,] detailLayer = terrainData.GetDetailLayer ( 0, 0, xRes, yRes, a );
            
            
            for (int tr = 0; tr < tris.Length; tr += 3)
            {
                List<Vector3> targetPositions = new List<Vector3> ( );
                
                float currDist = 0;
                float initialHeight;//= hit.point.y/terrainData.size.y;
    
    
                #region WorldPositions from triangles
    
                Vector3 p1, p2, p3, center;
    
                if ( tris [ tr ] >= verts.Length || tris [ tr+2 ] >= verts.Length)
                {
                    string trisDebug = "";
                    foreach( int tri in tris )
                    {
                        trisDebug += ( " Tris index = " + tri + "\n");
    
                    }
                    Debug.Log("Wrong vert index "+tr + "// Verts count = "+verts.Length + "  // Tris length = "+tris.Length + "\n"+trisDebug );
    
                    continue;
                }
    
    
                p1 = meshFilter.transform.TransformPoint( verts [ tris[tr] ] );
                p2 = meshFilter.transform.TransformPoint( verts [ tris [ tr+1 ] ]);
                p3 = meshFilter.transform.TransformPoint( verts [ tris [ tr+2 ] ] );
    
    /*            p1 = ( verts [ tr ] );
                p2 = ( verts [ tr + 1 ] );
                p3 = ( verts [ tr + 2 ] );*/
                center = (p1 + p2 + p3) / 3;
    
    /*            Debug.DrawRay(p1, Vector3.up*10, Color.red, 15);
                Debug.DrawRay( p2, Vector3.up * 10, Color.red, 15 );
                Debug.DrawRay( p3, Vector3.up * 10, Color.red, 15 );*/
    
    
                float targetHeight = center.y / terrainData.size.y;
                Vector2 targetPosTSpace = terrain.transform.InverseTransformPoint ( new Vector2 (
                        center.z / terrainPosMultX,
                        center.x / terrainPosMultY
                    )
                );
    
    
                foreach (var point in GetTrianglePoints(p1, p2, p3, center))
                {
                    targetPositions.Add(
                        terrain.transform.InverseTransformPoint(point)
                    );
                }
    
                //targetPositions.AddRange(GetTrianglePoints(p1, p2, p3, center));
    
                #endregion
    
    
    
    
                float searchRadius = splatMapSearchDist;
    
                foreach ( Vector3 targetPosition in targetPositions )
                {
                    //Debug.DrawRay(targetPosition, Vector3.up*50f, Color.red, 10f);
    
                    targetPosTSpace = new Vector2 (
                        targetPosition.z / terrainPosMultX,
                        targetPosition.x / terrainPosMultY
                    );
                    int iMinSerach = Mathf.Clamp ( (int)( targetPosTSpace.x - searchRadius ), 0, yRes );
                    int iMaxSerach = Mathf.Clamp ( (int)( targetPosTSpace.x + searchRadius ), 0, yRes );
    
                    int iiMinSearch = Mathf.Clamp ( (int)( targetPosTSpace.y - searchRadius ), 0, xRes );
                    int iiMaxSearch = Mathf.Clamp ( (int)( targetPosTSpace.y + searchRadius ), 0, xRes );
    
    
                    for (int i = iMinSerach; i < iMaxSerach; i++)
                    {
                        for (int ii = iiMinSearch; ii < iiMaxSearch; ii++)
                        {
                            currDist = Vector2.Distance( targetPosTSpace, new Vector2( i, ii ) );
                            
                            if (currDist < terrainCorrectionRadius)
                            {
                                var i1 = detailLayer[i, ii];
    
                                detailLayer[i, ii] = 0;
                            }
                        }
                    }
                }
            }
            
            terrainData.SetDetailLayer( 0, 0, a, detailLayer );
        }
    }



    public void SetTerrainSplatmap()
    {
        foreach (var child in GetComponentsInChildren<MeshFilter>())
        {
            SetTerrainSplatmap(child.gameObject, splatMapSearchDist, terrainCorrectionRadius, splatMapIndex);
        }
    }

    
    public static void SetTerrainSplatmap (GameObject go, float splatMapSearchDist, float terrainCorrectionRadius, int splatMapIndex)
    {
        #region SurfaceTriangles

        MeshFilter meshFilter = go.GetComponent<MeshFilter> ( );
        Mesh mesh=null;
        if( meshFilter != null ) mesh = meshFilter.sharedMesh;

        if( mesh == null ) return;

        var tris = mesh.triangles;
        var verts = mesh.vertices;

        #endregion



        #region TerrainGenVars

        TerrainData terrainData;
        int xRes, yRes, xSetPos,ySetPos;
        float[,,] alphamaps;
        Vector3 lerpPosCurr;

        var terrain = GetTerrain();
        if ( terrain == null ) return;

        
        terrainData = terrain.terrainData;
        xRes = terrainData.alphamapWidth;
        yRes = terrainData.alphamapHeight;
        float terrainPosMultX = terrainData.size.x / xRes;
        float terrainPosMultY = terrainData.size.z / yRes;

        alphamaps = terrainData.GetAlphamaps ( 0, 0, xRes, yRes );


        #endregion

        var terrainDataAlphamapLayers = terrainData.alphamapLayers;


        //foreach ( Edge edge in edges )
        for (int tr = 0; tr < tris.Length; tr += 3)
        {
            List<Vector3> targetPositions = new List<Vector3> ( );
            
            float currDist = 0;
            float initialHeight;//= hit.point.y/terrainData.size.y;


            #region WorldPositions from triangles

            Vector3 p1, p2, p3, center;

            if ( tris [ tr ] >= verts.Length || tris [ tr+2 ] >= verts.Length)
            {
                string trisDebug = "";
                foreach( int tri in tris )
                {
                    trisDebug += ( " Tris index = " + tri + "\n");

                }
                Debug.Log("Wrong vert index "+tr + "// Verts count = "+verts.Length + "  // Tris length = "+tris.Length + "\n"+trisDebug );

                continue;
            }


            p1 = meshFilter.transform.TransformPoint( verts [ tris[tr] ] );
            p2 = meshFilter.transform.TransformPoint( verts [ tris [ tr+1 ] ]);
            p3 = meshFilter.transform.TransformPoint( verts [ tris [ tr+2 ] ] );

/*            p1 = ( verts [ tr ] );
            p2 = ( verts [ tr + 1 ] );
            p3 = ( verts [ tr + 2 ] );*/
            center = (p1 + p2 + p3) / 3;

/*            Debug.DrawRay(p1, Vector3.up*10, Color.red, 15);
            Debug.DrawRay( p2, Vector3.up * 10, Color.red, 15 );
            Debug.DrawRay( p3, Vector3.up * 10, Color.red, 15 );*/


            float targetHeight = center.y / terrainData.size.y;
            Vector2 targetPosTSpace = terrain.transform.InverseTransformPoint ( new Vector2 (
                    center.z / terrainPosMultX,
                    center.x / terrainPosMultY
                )
            );


            foreach (var point in GetTrianglePoints(p1, p2, p3, center))
            {
                targetPositions.Add(
                    terrain.transform.InverseTransformPoint(point)
                );
            }

            //targetPositions.AddRange(GetTrianglePoints(p1, p2, p3, center));

            #endregion




            float searchRadius = splatMapSearchDist;

            foreach ( Vector3 targetPosition in targetPositions )
            {
                //Debug.DrawRay(targetPosition, Vector3.up*50f, Color.red, 10f);

                targetPosTSpace = new Vector2 (
                    targetPosition.z / terrainPosMultX,
                    targetPosition.x / terrainPosMultY
                );
                int iMinSerach = Mathf.Clamp ( (int)( targetPosTSpace.x - searchRadius ), 0, yRes );
                int iMaxSerach = Mathf.Clamp ( (int)( targetPosTSpace.x + searchRadius ), 0, yRes );

                int iiMinSearch = Mathf.Clamp ( (int)( targetPosTSpace.y - searchRadius ), 0, xRes );
                int iiMaxSearch = Mathf.Clamp ( (int)( targetPosTSpace.y + searchRadius ), 0, xRes );


                for (int i = iMinSerach; i < iMaxSerach; i++)
                {
                    for (int ii = iiMinSearch; ii < iiMaxSearch; ii++)
                    {
                        currDist = Vector2.Distance( targetPosTSpace, new Vector2( i, ii ) );
                        
                        if (currDist < terrainCorrectionRadius)
                        {
                            for (int a = 0; a < terrainDataAlphamapLayers; a++)
                            {
                                alphamaps[i, ii, a] = 0;
                            }
                            
                            alphamaps[i, ii, splatMapIndex] = 1;
                        }
                    }
                }
            }
        }
        
        terrainData.SetAlphamaps ( 0, 0, alphamaps );

    }


}






#if UNITY_EDITOR

[CustomEditor ( typeof ( FitTerrainToPoligon ) )]
[CanEditMultipleObjects]
public class FitTerrainToPoligon_Editor : Editor
{

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ( );


        if ( GUILayout.Button ( "Fit Terrain height" ) )
        {
            foreach ( var targ in targets )
            {
                //generator.FitTerrainHeightToEdges();
                ( (FitTerrainToPoligon)targ ).FitTerrainHeightToEdges ( );
            }
        }
        if ( GUILayout.Button ( "Paint Terrain splatmap" ) )
        {
            foreach ( var targ in targets )
            {
                //generator.FitTerrainHeightToEdges();
                ( (FitTerrainToPoligon)targ ).SetTerrainSplatmap ( );
            }
        }
        if ( GUILayout.Button ( "SetTerrainGrassToZero" ) )
        {
            foreach ( var targ in targets )
            {
                //generator.FitTerrainHeightToEdges();
                ( (FitTerrainToPoligon)targ ).SetTerrainGrassToZero();
            }
        }


/*        if ( GUILayout.Button ( "Mesh to Quads" ) )
        {
            foreach ( var targ in targets )
            {
                //generator.FitTerrainHeightToEdges();
                ( (FitTerrainToPoligon)targ ).MeshToQuads ( true );
            }
        }*/
    }
}

#endif