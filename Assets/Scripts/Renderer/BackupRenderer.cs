using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BackupRenderer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Range(0, 1920)] private int screenWidth;
    [SerializeField, Range(0, 1440)] private int screenHeight;
    [SerializeField] private RawImage rawImage;

    [Header("Lighting")]
    [SerializeField, Range(0.0f,1.0f)] private float ambientLight;
    [SerializeField] private Light pointlightSource;

    private Camera _camera;
    private Bitmap _bitmap;
    private Texture2D _texture2D;
    private List<RenderMesh> _meshes;
    private PointLight _pointLight;

    //Matrices
    private Matrix4x4 _modelToWorld;
    private Matrix4x4 _viewMatrix;
    private Matrix4x4 _projectionMatrix;
    private Matrix4x4 _modelToView;
    private Matrix4x4 _mvp;


    private void Awake()
    {
    }


    void Start()
    {
        GetAllMeshes();
        _camera = GetComponent<Camera>();
        _camera.aspect = (float) screenWidth / screenHeight;
        _projectionMatrix = _camera.projectionMatrix;
        _bitmap = new Bitmap(screenWidth, screenWidth);
        _texture2D = new Texture2D(screenWidth, screenHeight, TextureFormat.RGBA32, false);
        _bitmap.Clear(0, 0, 255, 255);
        rawImage.texture = _texture2D;
        rawImage.texture.filterMode = FilterMode.Point;
        //rawImage.SetNativeSize();

        _pointLight = new PointLight(pointlightSource.transform.position, pointlightSource.intensity,
            pointlightSource.color, pointlightSource.range);
    }


    void Update()
    {
        UpdateLight();
    }

    private void LateUpdate()
    {
        _bitmap.Clear(0, 0, 0, 255);

        //render
        Render();

        Display();
    }


    private void GetAllMeshes()
    {
        _meshes = new List<RenderMesh>();

        var objects = FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var obj in objects)
        {
            if (obj.TryGetComponent(out SoftwareMaterial softwareMaterial))
            {
                if (obj.TryGetComponent(out MeshFilter meshFilter))
                {
                    var mesh =
                        new RenderMesh(meshFilter.sharedMesh, obj.GetComponent<Renderer>(), true,
                            softwareMaterial.texture2D);
                    _meshes.Add(mesh);
                }
            }
        }
    }

    private void UpdateLight()
    {
        _pointLight.Position = pointlightSource.transform.position;
    }

    private void Render()
    {
        _viewMatrix = _camera.worldToCameraMatrix;
        var planes = GeometryUtility.CalculateFrustumPlanes(_camera);

        foreach (var mesh in _meshes)
        {
            var color = mesh.Color;

            if (!GeometryUtility.TestPlanesAABB(planes, mesh.Renderer.bounds)) continue;

            _modelToWorld = mesh.Renderer.localToWorldMatrix;

            _mvp = _projectionMatrix * _viewMatrix * _modelToWorld;

            for (var i = 0; i < mesh.Mesh.GetIndices(0).Length; i += 3)
            {
                var vertex3A = mesh.Mesh.vertices[mesh.Mesh.triangles[i]];
                var vertex3B = mesh.Mesh.vertices[mesh.Mesh.triangles[i + 1]];
                var vertex3C = mesh.Mesh.vertices[mesh.Mesh.triangles[i + 2]];

                var uvA = mesh.Mesh.uv[mesh.Mesh.triangles[i]];
                var uvB = mesh.Mesh.uv[mesh.Mesh.triangles[i + 1]];
                var uvC = mesh.Mesh.uv[mesh.Mesh.triangles[i + 2]];
                
                var normalA = mesh.Mesh.normals[mesh.Mesh.triangles[i]];
                var normalB = mesh.Mesh.normals[mesh.Mesh.triangles[i + 1]];
                var normalC = mesh.Mesh.normals[mesh.Mesh.triangles[i + 2]];

                var lightFactorA = GetLightFactor(vertex3A, normalA, _pointLight, _modelToWorld);
                var lightFactorB = GetLightFactor(vertex3B, normalB, _pointLight, _modelToWorld);
                var lightFactorC = GetLightFactor(vertex3C, normalC, _pointLight, _modelToWorld);


                //MODEL SPACE VERTICES (vector4)
                var vector4A = new Vector4(vertex3A.x, vertex3A.y, vertex3A.z, 1);
                var vector4B = new Vector4(vertex3B.x, vertex3B.y, vertex3B.z, 1);
                var vector4C = new Vector4(vertex3C.x, vertex3C.y, vertex3C.z, 1);


                //CLIP SPACE
                var vertexA = new Vertex(_mvp * vector4A, uvA, 0);
                var vertexB = new Vertex(_mvp * vector4B, uvB, 0);
                var vertexC = new Vertex(_mvp * vector4C, uvC, 0);


                // Clip here against the [-1, +1] in x/y/z in clip space (without dividing by w!)


                //PERSPECTIVE DIVIDE, goes from clip space to NDC (Normalized Device Coordinates), invert y depending on platform, in this case depending on Texture2D
                vertexA.V4.y *= -1;
                vertexB.V4.y *= -1;
                vertexC.V4.y *= -1;
                vertexA.V4 /= vertexA.V4.w;
                vertexB.V4 /= vertexB.V4.w;
                vertexC.V4 /= vertexC.V4.w;


                //Back face culling by comparing winding order.

                var area2 = (vertexA.V4.x * vertexB.V4.y - vertexB.V4.x * vertexA.V4.y) +
                            (vertexB.V4.x * vertexC.V4.y - vertexC.V4.x * vertexB.V4.y) +
                            (vertexC.V4.x * vertexA.V4.y - vertexA.V4.x * vertexC.V4.y);
                if (area2 < 0.0f) continue;


                //SCREEN SPACE

                var pointA =
                    new ScreenVertex(
                        (new Vector2(vertexA.V4.x, vertexA.V4.y) * new Vector2(.5f, -.5f) + new Vector2(.5f, .5f)) *
                        new Vector2(screenWidth, screenHeight), vertexA.uv, vertexA.V4.z, lightFactorA);
                var pointB =
                    new ScreenVertex(
                        (new Vector2(vertexB.V4.x, vertexB.V4.y) * new Vector2(.5f, -.5f) + new Vector2(.5f, .5f)) *
                        new Vector2(screenWidth, screenHeight), vertexB.uv, vertexB.V4.z, lightFactorB);
                var pointC =
                    new ScreenVertex(
                        (new Vector2(vertexC.V4.x, vertexC.V4.y) * new Vector2(.5f, -.5f) + new Vector2(.5f, .5f)) *
                        new Vector2(screenWidth, screenHeight), vertexC.uv, vertexC.V4.z, lightFactorC);

                DrawTriangle(pointA, pointB, pointC, color, mesh.MainTexture);

                /*DrawPoint(pointA, new Color32(255, 255, 255, 255));
                DrawPoint(pointB, new Color32(255, 255, 255, 255));
                DrawPoint(pointC, new Color32(255, 255, 255, 255));*/
            }
        }
    }


    private void Display()
    {
        _texture2D.LoadRawTextureData(_bitmap.BackBuffer);
        _texture2D.Apply();
    }

    

    private void DrawTriangle(ScreenVertex screenVertexA, ScreenVertex screenVertexB, ScreenVertex screenVertexC,
        Color32 color, Texture2D texture)
    {
        var minYVert = screenVertexA;
        var midYVert = screenVertexB;
        var maxYVert = screenVertexC;

        //Sort vertices by height

        if (maxYVert.V2.y < midYVert.V2.y)
        {
            (maxYVert, midYVert) = (midYVert, maxYVert);
        }

        if (midYVert.V2.y < minYVert.V2.y)
        {
            (midYVert, minYVert) = (minYVert, midYVert);
        }

        if (maxYVert.V2.y < midYVert.V2.y)
        {
            (maxYVert, midYVert) = (midYVert, maxYVert);
        }

        //Check to see if triangle is natural flat top or flat bottom

        if (minYVert.V2.y == midYVert.V2.y)
        {
            //Sort by X for the sake of drawing algorithm needing correct order

            if (midYVert.V2.x < minYVert.V2.x) (midYVert, minYVert) = (minYVert, midYVert);


            DrawFlatBottomTriangle(minYVert, midYVert, maxYVert, color, texture);
        }
        else if (maxYVert.V2.y == midYVert.V2.y)
        {
            //Sort by X for the sake of drawing algorithm needing correct order

            if (midYVert.V2.x < maxYVert.V2.x) (maxYVert, midYVert) = (midYVert, maxYVert);

            DrawFlatTopTriangle(maxYVert, midYVert, minYVert, color, texture);
        }
        else
        {
            //Find split to divide general triangle into one flat top & one flat bottom, interpolating neccesary vertex values

            var splitAlpha = (midYVert.V2.y - minYVert.V2.y) / (maxYVert.V2.y - minYVert.V2.y);

            var splitVert = new ScreenVertex(minYVert.V2 + (maxYVert.V2 - minYVert.V2) * splitAlpha,
                minYVert.uv + (maxYVert.uv - minYVert.uv) * splitAlpha,
                minYVert.zDepth + (maxYVert.zDepth - minYVert.zDepth) * splitAlpha, minYVert.LightFactor + (maxYVert.LightFactor - minYVert.LightFactor) * splitAlpha);

            if (splitVert.V2.x < midYVert.V2.x)
            {
                DrawFlatBottomTriangle(splitVert, midYVert, maxYVert, color, texture);
                DrawFlatTopTriangle(splitVert, midYVert, minYVert, color, texture);
            }
            else
            {
                DrawFlatBottomTriangle(midYVert, splitVert, maxYVert, color, texture);
                DrawFlatTopTriangle(midYVert, splitVert, minYVert, color, texture);
            }
        }
    }

    private void DrawFlatTopTriangle(ScreenVertex leftVert, ScreenVertex rightVert, ScreenVertex bottomVert,
        Color32 color, Texture2D texture)
    {
        //Determine screen space slopes
        var bottomToLeft = (leftVert.V2.x - bottomVert.V2.x) / (leftVert.V2.y - bottomVert.V2.y);
        var bottomToRight = (rightVert.V2.x - bottomVert.V2.x) / (rightVert.V2.y - bottomVert.V2.y);

        //Determine start & ending scan lines depending on fill convention (top-left, might be called bottom-left because of screenspace 0,0 being bottom-left!)
        var yStart = Mathf.CeilToInt(bottomVert.V2.y - 0.5f);
        var yEnd = Mathf.CeilToInt(leftVert.V2.y - 0.5f);

        //Determine uv-coord change for every change in screen space Y

        var uvEdgeL = leftVert.uv;
        var uvEdgeR = rightVert.uv;
        var uvEdgeB = bottomVert.uv;


        var uvEdgeStepL = (uvEdgeL - uvEdgeB) / (leftVert.V2.y - bottomVert.V2.y);
        var uvEdgeStepR = (uvEdgeR - uvEdgeB) / (rightVert.V2.y - bottomVert.V2.y);

        //UV-coord prestep, since vertex position and center of pixel is not exactly same

        uvEdgeL += uvEdgeStepL * (yStart + 0.5f - rightVert.V2.y);
        uvEdgeR += uvEdgeStepR * (yStart + 0.5f - rightVert.V2.y);
        
        //interpolate Z-values

        var zEdgeL = leftVert.zDepth;
        var zEdgeR = rightVert.zDepth;
        var zEdgeB = bottomVert.zDepth;
        
        //Determine depth/z change for every change in screen space Y
        
        var zEdgeStepL = (zEdgeL - zEdgeB) / (leftVert.V2.y - bottomVert.V2.y);
        var zEdgeStepR = (zEdgeR - zEdgeB) / (rightVert.V2.y - bottomVert.V2.y);
        
        zEdgeL += zEdgeStepL * (yStart + 0.5f - rightVert.V2.y);
        zEdgeR += zEdgeStepR * (yStart + 0.5f - rightVert.V2.y);
        
        
        //Light interpolation
        
        var lightEdgeL = leftVert.LightFactor;
        var lightEdgeR = rightVert.LightFactor;
        var lightEdgeB = bottomVert.LightFactor;
        
        //Determine light factor change for every change in screen space Y
        
        var lightEdgeStepL = (lightEdgeL - lightEdgeB) / (leftVert.V2.y - bottomVert.V2.y);
        var lightEdgeStepR = (lightEdgeR - lightEdgeB) / (rightVert.V2.y - bottomVert.V2.y);
        
        lightEdgeL += lightEdgeStepL * (yStart + 0.5f - rightVert.V2.y);
        lightEdgeR += lightEdgeStepR * (yStart + 0.5f - rightVert.V2.y);


        for (var y = yStart; y < yEnd; y++, uvEdgeL += uvEdgeStepL, uvEdgeR += uvEdgeStepR, zEdgeL += zEdgeStepL, zEdgeR += zEdgeStepR, lightEdgeL += lightEdgeStepL, lightEdgeR += lightEdgeStepR)
        {
            //Determine start & ending x-coord depending on fill conv. Add 0.5f to base calculation on pixel centers!
            var leftX = bottomToLeft * (y + 0.5f - leftVert.V2.y) + leftVert.V2.x;
            var rightX = bottomToRight * (y + 0.5f - rightVert.V2.y) + rightVert.V2.x;

            //Determine actual pixels
            var xStartPixel = Mathf.CeilToInt(leftX - 0.5f);
            var xEndPixel = Mathf.CeilToInt(rightX - 0.5f);

            //determine uv-space scanline step size & prestep!
            var uvScanStep = (uvEdgeR - uvEdgeL) / (rightX - leftX);

            var uvCoord = uvEdgeL + uvScanStep * (xStartPixel + 0.5f - leftX);
            
            //determine z scanline step size
            
            var zScanStep = (zEdgeR - zEdgeL) / (rightX - leftX);

            var zValue = zEdgeL + zScanStep * (xStartPixel + 0.5f - leftX);
            
            //determine light factor scanline step size
            
            var lightScanStep = (lightEdgeR - lightEdgeL) / (rightX - leftX);

            var lightFactor = lightEdgeL + lightScanStep * (xStartPixel + 0.5f - leftX);


            for (var x = xStartPixel; x < xEndPixel; x++, uvCoord += uvScanStep, zValue += zScanStep, lightFactor += lightScanStep)
            {
                var xClamped = (int) Mathf.Clamp(uvCoord.x * texture.width - 1, 0, texture.width);
                var yClamped = (int) Mathf.Clamp(uvCoord.y * texture.height - 1, 0, texture.height);

                var litColour = (Color32)(texture.GetPixel(xClamped, yClamped) * lightFactor);
                litColour.a = 255;


                DrawPoint(new Vector2(x, y), litColour, zValue);
            }
        }
    }

    private void DrawFlatBottomTriangle(ScreenVertex leftVert, ScreenVertex rightVert, ScreenVertex topVert,
        Color32 color, Texture2D texture)
    {

        //Determine screen space slopes
        var leftToTop = (topVert.V2.x - leftVert.V2.x) / (topVert.V2.y - leftVert.V2.y);
        var rightToTop = (topVert.V2.x - rightVert.V2.x) / (topVert.V2.y - rightVert.V2.y);

        //Determine start & ending scan lines depending on fill convention (top-left, might be called bottom-left because of screenspace 0,0 being bottom-left!)
        var yStart = Mathf.CeilToInt(leftVert.V2.y - 0.5f);
        var yEnd = Mathf.CeilToInt(topVert.V2.y - 0.5f);
        
        //Determine uv-coord change for every change in screen space Y

        var uvEdgeL = leftVert.uv;
        var uvEdgeR = rightVert.uv;
        var uvEdgeT = topVert.uv;


        var uvEdgeStepL = (uvEdgeT - uvEdgeL) / (topVert.V2.y - leftVert.V2.y);
        var uvEdgeStepR = (uvEdgeT - uvEdgeR) / (topVert.V2.y - rightVert.V2.y);

        //UV-coord prestep, since vertex position and center of pixel is not exactly same

        uvEdgeL += uvEdgeStepL * (yStart + 0.5f - leftVert.V2.y);
        uvEdgeR += uvEdgeStepR * (yStart + 0.5f - leftVert.V2.y);
        
        //interpolate Z-values

        var zEdgeL = leftVert.zDepth;
        var zEdgeR = rightVert.zDepth;
        var zEdgeT = topVert.zDepth;
        
        //Determine depth/z change for every change in screen space Y
        
        var zEdgeStepL = (zEdgeT - zEdgeL) / (topVert.V2.y - leftVert.V2.y);
        var zEdgeStepR = (zEdgeT - zEdgeR) / (topVert.V2.y - rightVert.V2.y);
        
        zEdgeL += zEdgeStepL * (yStart + 0.5f - leftVert.V2.y);
        zEdgeR += zEdgeStepR * (yStart + 0.5f - leftVert.V2.y);
        
        //Light interpolation
        
        var lightEdgeL = leftVert.LightFactor;
        var lightEdgeR = rightVert.LightFactor;
        var lightEdgeT = topVert.LightFactor;
        
        //Determine light factor change for every change in screen space Y
        
        var lightEdgeStepL = (lightEdgeT - lightEdgeL) / (topVert.V2.y - leftVert.V2.y);
        var lightEdgeStepR = (lightEdgeT - lightEdgeR) / (topVert.V2.y - rightVert.V2.y);
        
        lightEdgeL += lightEdgeStepL * (yStart + 0.5f - rightVert.V2.y);
        lightEdgeR += lightEdgeStepR * (yStart + 0.5f - rightVert.V2.y);

        for (var y = yStart; y < yEnd; y++, uvEdgeL += uvEdgeStepL, uvEdgeR += uvEdgeStepR, zEdgeL += zEdgeStepL, zEdgeR += zEdgeStepR, lightEdgeL += lightEdgeStepL, lightEdgeR += lightEdgeStepR)
        {
            //Determine start & ending x-coord depending on fill conv. Add 0.5f to base calculation on pixel centers!
            var leftX = leftToTop * (y + 0.5f - leftVert.V2.y) + leftVert.V2.x;
            var rightX = rightToTop * (y + 0.5f - rightVert.V2.y) + rightVert.V2.x;

            //Determine actual pixels
            var xStartPixel = Mathf.CeilToInt(leftX - 0.5f);
            var xEndPixel = Mathf.CeilToInt(rightX - 0.5f);
            
            //determine uv-space scanline step size & prestep!
            var uvScanStep = (uvEdgeR - uvEdgeL) / (rightX - leftX);

            var uvCoord = uvEdgeL + uvScanStep * (xStartPixel + 0.5f - leftX);
            
            //determine z scanline step size
            
            var zScanStep = (zEdgeR - zEdgeL) / (rightX - leftX);

            var zValue = zEdgeL + zScanStep * (xStartPixel + 0.5f - leftX);
            
            //determine light factor scanline step size
            
            var lightScanStep = (lightEdgeR - lightEdgeL) / (rightX - leftX);

            var lightFactor = lightEdgeL + lightScanStep * (xStartPixel + 0.5f - leftX);


            for (var x = xStartPixel; x < xEndPixel; x++, uvCoord += uvScanStep, zValue += zScanStep, lightFactor += lightScanStep)
            {

                var xClamped = (int) Mathf.Clamp(uvCoord.x * texture.width - 1, 0, texture.width);
                var yClamped = (int) Mathf.Clamp(uvCoord.y * texture.height - 1, 0, texture.height);

                var litColour = (Color32)(texture.GetPixel(xClamped, yClamped) * lightFactor);
                litColour.a = 255;


                DrawPoint(new Vector2(x, y), litColour, zValue);
            }
        }
    }

    private float GetLightFactor(Vector3 vertexPos, Vector3 normal, PointLight pointLight, Matrix4x4 modelToWorld)
    {
        var worldVertex = modelToWorld.MultiplyPoint(vertexPos);
        var worldNormal = modelToWorld.MultiplyPoint(normal);
        var lightDir = (pointLight.Position - worldVertex).normalized;
        var distance = (pointLight.Position - worldVertex).magnitude;
        var distFactor = Mathf.Clamp(pointLight.Range / (distance * distance), 0,1);

        var factor = Mathf.Clamp(Vector3.Dot(worldNormal, lightDir) * distFactor, ambientLight, 2.0f);

        return factor;
    }

    


    private void DrawPoint(Vector2 point, Color32 color, float zDepth)
    {
        if (point.x >= 0 && point.y >= 0 && point.x < screenWidth && point.y < screenHeight)
        {
            _bitmap.PutPixel((int) point.x, (int) point.y, color.r, color.g, color.b, color.a, zDepth);
        }
    }
}