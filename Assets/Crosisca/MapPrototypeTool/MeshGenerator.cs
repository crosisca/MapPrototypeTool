using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class MeshGenerator : MonoBehaviour
{
    SquareGrid squareGrid;
    List<Vector3> vertices;
    List<int> triangles;

    //[SerializeField]
    MeshFilter obstaclesMesh;
    //[SerializeField]
    MeshFilter walls;
    [SerializeField]
    float wallheight = 5;


    Dictionary<int, List<Triangle>> triangleDictionary = new Dictionary<int, List<Triangle>>();

    List<List<int>> outlines = new List<List<int>>();
    //Used to make sure we don't check a vertex twice for outline edge. Contains is faster on a HashSet than on a List.
    HashSet<int> checkedVertices = new HashSet<int>();

    [SerializeField]
    bool drawGizmos;
    
    public void GenerateMesh (int[,] map, float squareSize)
    {
        squareGrid = new SquareGrid(map, squareSize);

        vertices = new List<Vector3>();
        triangles = new List<int>();

        triangleDictionary.Clear();
        outlines.Clear();
        checkedVertices.Clear();

        for (int x = 0; x < squareGrid.squares.GetLength(0); x++)
        {
            for (int y = 0; y < squareGrid.squares.GetLength(1); y++)
            {
                TriangulateSquare(squareGrid.squares[x, y]);
            }
        }

        Mesh mesh = new Mesh();

        if(obstaclesMesh != null)
            DestroyImmediate(obstaclesMesh.gameObject);

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        
        obstaclesMesh = new GameObject("ObstaclesTopMesh").AddComponent<MeshFilter>();
        obstaclesMesh.gameObject.isStatic = true;
        obstaclesMesh.gameObject.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
        obstaclesMesh.gameObject.GetComponent<MeshRenderer>().material.color = Color.grey;
        obstaclesMesh.gameObject.layer = LayerMask.NameToLayer("VisibleWalls");
        obstaclesMesh.transform.parent = transform;
        obstaclesMesh.transform.localPosition = Vector3.zero + Vector3.up * wallheight / 2;
        obstaclesMesh.mesh = mesh;

        CreateWallMesh();
    }

    void TriangulateSquare (Square square)
    {
        switch (square.configuration)
        {
            case 0:
                break;

            // 1 points:
            case 1:
                MeshFromPoints(square.centerLeft, square.centerBottom, square.bottomLeft);
                break;
            case 2:
                MeshFromPoints(square.bottomRight, square.centerBottom, square.centerRight);
                break;
            case 4:
                MeshFromPoints(square.topRight, square.centerRight, square.centerTop);
                break;
            case 8:
                MeshFromPoints(square.topLeft, square.centerTop, square.centerLeft);
                break;

            // 2 points:
            case 3:
                MeshFromPoints(square.centerRight, square.bottomRight, square.bottomLeft, square.centerLeft);
                break;
            case 6:
                MeshFromPoints(square.centerTop, square.topRight, square.bottomRight, square.centerBottom);
                break;
            case 9:
                MeshFromPoints(square.topLeft, square.centerTop, square.centerBottom, square.bottomLeft);
                break;
            case 12:
                MeshFromPoints(square.topLeft, square.topRight, square.centerRight, square.centerLeft);
                break;
            case 5:
                MeshFromPoints(square.centerTop, square.topRight, square.centerRight, square.centerBottom, square.bottomLeft, square.centerLeft);
                break;
            case 10:
                MeshFromPoints(square.topLeft, square.centerTop, square.centerRight, square.bottomRight, square.centerBottom, square.centerLeft);
                break;

            // 3 point:
            case 7:
                MeshFromPoints(square.centerTop, square.topRight, square.bottomRight, square.bottomLeft, square.centerLeft);
                break;
            case 11:
                MeshFromPoints(square.topLeft, square.centerTop, square.centerRight, square.bottomRight, square.bottomLeft);
                break;
            case 13:
                MeshFromPoints(square.topLeft, square.topRight, square.centerRight, square.centerBottom, square.bottomLeft);
                break;
            case 14:
                MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.centerBottom, square.centerLeft);
                break;

            // 4 point:
            case 15:
                MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.bottomLeft);
                //All vertices here are walls so there's no way they can be an outline edge. So adding them to checkdVertices list makes the checks faster
                checkedVertices.Add(square.topLeft.vertexIndex);
                checkedVertices.Add(square.topRight.vertexIndex);
                checkedVertices.Add(square.bottomRight.vertexIndex);
                checkedVertices.Add(square.bottomLeft.vertexIndex);
                break;
        }

    }

    void MeshFromPoints (params Node[] points)
    {
        AssignVertices(points);

        if (points.Length >= 3)
            CreateTriangle(points[0], points[1], points[2]);
        if (points.Length >= 4)
            CreateTriangle(points[0], points[2], points[3]);
        if (points.Length >= 5)
            CreateTriangle(points[0], points[3], points[4]);
        if (points.Length >= 6)
            CreateTriangle(points[0], points[4], points[5]);

    }

    void AssignVertices (Node[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i].vertexIndex == -1)
            {
                points[i].vertexIndex = vertices.Count;
                vertices.Add(points[i].position);
            }
        }
    }

    void CreateTriangle (Node a, Node b, Node c)
    {
        triangles.Add(a.vertexIndex);
        triangles.Add(b.vertexIndex);
        triangles.Add(c.vertexIndex);

        Triangle triangle = new Triangle(a.vertexIndex, b.vertexIndex, c.vertexIndex);
        AddTriangleToDictionary(triangle.vertexIndexA, triangle);
        AddTriangleToDictionary(triangle.vertexIndexB, triangle);
        AddTriangleToDictionary(triangle.vertexIndexC, triangle);
    }

    void AddTriangleToDictionary(int vertexIndexKey, Triangle triangle)
    {
        if (triangleDictionary.ContainsKey(vertexIndexKey))
            triangleDictionary[vertexIndexKey].Add(triangle);
        else
        {
            List<Triangle> triangleList = new List<Triangle>();
            triangleList.Add(triangle);
            triangleDictionary.Add(vertexIndexKey, triangleList);
        }
    }

    void CreateWallMesh()
    {
        CalculateMeshOutlines();
        
        List<Vector3> wallVertices = new List<Vector3>();
        List<int> wallTriangles = new List<int>();

        Mesh wallMesh = new Mesh();
        
        foreach (List<int> outline in outlines)
        {
            for (int i = 0; i < outline.Count - 1; i++)//Count -1 here to be able to get i + 1 on the right vertex
            {
                int startIndex = wallVertices.Count;
                wallVertices.Add(vertices[outline[i]]); // top left = 0 
                wallVertices.Add(vertices[outline[i+1]]); // top right = 1
                wallVertices.Add(vertices[outline[i]] - Vector3.up * wallheight); // bottom left = 2
                wallVertices.Add(vertices[outline[i+1]] - Vector3.up * wallheight); // bottom right = 3

                //Add triangles anti-clockwise because it's being viewed from inside
                wallTriangles.Add(startIndex + 0); //left
                wallTriangles.Add(startIndex + 2); //bottom left
                wallTriangles.Add(startIndex + 3); //bottom right


                wallTriangles.Add(startIndex + 3); //bottom right
                wallTriangles.Add(startIndex + 1); //top right
                wallTriangles.Add(startIndex + 0); //top left

            }
        }

        wallMesh.vertices = wallVertices.ToArray();
        wallMesh.triangles = wallTriangles.ToArray();
        wallMesh.RecalculateNormals();


        if (walls != null)
            DestroyImmediate(walls.gameObject);

        walls = new GameObject("ObstaclesVerticalMesh").AddComponent<MeshFilter>();
        walls.gameObject.isStatic = true;
        walls.gameObject.layer = LayerMask.NameToLayer("VisibleWalls");
        walls.gameObject.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
        walls.gameObject.GetComponent<MeshRenderer>().material.color = Color.grey;
        walls.gameObject.AddComponent<MeshCollider>();
        walls.transform.parent = transform;
        walls.transform.localPosition = Vector3.zero + Vector3.up * wallheight/2;

        walls.mesh = wallMesh;

        MeshCollider wallCollider = walls.gameObject.GetComponent<MeshCollider>();
        wallCollider.sharedMesh = wallMesh;
    }

    void CalculateMeshOutlines()
    {
        for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
        {
            if (!checkedVertices.Contains(vertexIndex))
            {
                int newOutlineVertex = GetConnectedOutlineVertex(vertexIndex);

                //If it exists
                if (newOutlineVertex != -1)
                {
                    checkedVertices.Add(vertexIndex);

                    List<int> newOutline = new List<int>();
                    newOutline.Add(vertexIndex);
                    outlines.Add(newOutline);

                    FollowOutline(newOutlineVertex, outlines.Count - 1);

                    outlines[outlines.Count-1].Add(vertexIndex);
                }
            }

        }
    }

    void FollowOutline(int vertexIndex, int outlineIndex)
    {
        outlines[outlineIndex].Add(vertexIndex);
        checkedVertices.Add(vertexIndex);

        int nextVertexIndex = GetConnectedOutlineVertex(vertexIndex);

        if (nextVertexIndex != -1)
        {
            FollowOutline(nextVertexIndex, outlineIndex);
        }
    }

    int GetConnectedOutlineVertex(int vertexIndex)
    {
        List<Triangle> trianglesContainingVertex = triangleDictionary[vertexIndex];

        //Find connected vertex that find an outline edge
        for (int i = 0; i < trianglesContainingVertex.Count; i++)
        {
            Triangle triangle = trianglesContainingVertex[i];

            //3 = triangles vertex count (a,b,c)
            for (int j = 0; j < 3; j++)
            {
                int vertexB = triangle[j];
                //checkedVertices.Contais is only optimization
                if (vertexB != vertexIndex && !checkedVertices.Contains(vertexB))
                {
                    if (IsOutlineEdge(vertexIndex, vertexB))
                        return vertexB;
                }
            }
        }

        return -1;
    }

    //When an edge(2 vertex) is shared between 2 triangles it's not an outline edge
    bool IsOutlineEdge(int vertexA, int vertexB)
    {
        List<Triangle> trianglesContainingVertexA = triangleDictionary[vertexA];
        int sharedTriangleCount = 0;
        for (int i = 0; i < trianglesContainingVertexA.Count; i++)
        {
            if (trianglesContainingVertexA[i].Contains(vertexB))
            {
                sharedTriangleCount++;
                if (sharedTriangleCount > 1)
                    break;
            }
        }

        return sharedTriangleCount == 1;
    }

    void OnDrawGizmos ()
    {
        if(!drawGizmos)
            return;

		if (squareGrid != null) {
			for (int x = 0; x < squareGrid.squares.GetLength(0); x ++) {
				for (int y = 0; y < squareGrid.squares.GetLength(1); y ++) {

					Gizmos.color = (squareGrid.squares[x,y].topLeft.active)?Color.black:Color.white;
					Gizmos.DrawCube(squareGrid.squares[x,y].topLeft.position, Vector3.one * .4f);

					Gizmos.color = (squareGrid.squares[x,y].topRight.active)?Color.black:Color.white;
					Gizmos.DrawCube(squareGrid.squares[x,y].topRight.position, Vector3.one * .4f);

					Gizmos.color = (squareGrid.squares[x,y].bottomRight.active)?Color.black:Color.white;
					Gizmos.DrawCube(squareGrid.squares[x,y].bottomRight.position, Vector3.one * .4f);

					Gizmos.color = (squareGrid.squares[x,y].bottomLeft.active)?Color.black:Color.white;
					Gizmos.DrawCube(squareGrid.squares[x,y].bottomLeft.position, Vector3.one * .4f);


					Gizmos.color = Color.grey;
					Gizmos.DrawCube(squareGrid.squares[x,y].centerTop.position, Vector3.one * .15f);
					Gizmos.DrawCube(squareGrid.squares[x,y].centerRight.position, Vector3.one * .15f);
					Gizmos.DrawCube(squareGrid.squares[x,y].centerBottom.position, Vector3.one * .15f);
					Gizmos.DrawCube(squareGrid.squares[x,y].centerLeft.position, Vector3.one * .15f);

				}
			}
		}
    }
}
