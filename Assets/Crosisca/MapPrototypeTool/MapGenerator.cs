using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Net.Sockets;
using UnityEngine.AI;
using Vexe.Runtime.Extensions;

[RequireComponent(typeof(MeshGenerator))]
public class MapGenerator : MonoBehaviour
{
    [SerializeField]
    Texture2D texture;

    [SerializeField]
    float squareSize = 1;

    public int[,] map;

    [SerializeField]
    bool drawGizmos;

    [SerializeField]
    bool useBorder;

    int width;
    int height;

#if MASTER_CLIENT
    void Start()
    {
        GenerateMapFromTexture(texture);
    }

    [ContextMenu("GenerateMapFromTexture")]
    void EditorGenerateMapFromTexture()
    {
        GenerateMapFromTexture(texture);
    }

    [ContextMenu("SendToClients")]
    void EditorSendMapToClients ()
    {
        GameSession.Current.MasterClient.SendMapGeneratorResyncData(GetResyncData());
    }
#endif

    public void GenerateMapFromTexture (Texture2D tex, bool flip = false)
    {
        texture = tex;
        width = texture.width;
        height = texture.height;

        map = new int[width, height];

        FillMapFromTexture();

        GenerateMapFromGrid(map, flip);
    }

    public void GenerateMapFromGrid(int[,] gridMap, bool flip)
    {
        if (flip)
        {
            map = FlipArray(gridMap);
        }

        //Destroy all children before generating a new map
        Transform[] children = transform.GetComponentsInChildren<Transform>().Where(go => go.gameObject != gameObject).ToArray();
        foreach (Transform t in children)
            DestroyImmediate(t);


        width = gridMap.GetLength(0);
        height = gridMap.GetLength(1);
        
        MeshGenerator meshGen = GetComponent<MeshGenerator>();

        if (useBorder)
        {
            int borderSize = 1;
            int[,] borderedMap = new int[width + borderSize * 2, height + borderSize * 2];

            for (int x = 0; x < borderedMap.GetLength(0); x++)
            {
                for (int y = 0; y < borderedMap.GetLength(1); y++)
                {
                    if (x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize)
                        borderedMap[x, y] = map[x - borderSize, y - borderSize];
                    else
                        borderedMap[x, y] = 1;
                }
            }

            meshGen.GenerateMesh(borderedMap, squareSize);
        }
        else
            meshGen.GenerateMesh(map, squareSize);

        Transform[] generatedMeshs = transform.GetComponentsInChildren<Transform>().Where(go => go.gameObject != gameObject).ToArray();

#if MASTER_CLIENT
        //Create object to hold the navmesh. Generated meshs needs to be children of it to properly bake the navmesh
        NavMeshSurface navMesh = new GameObject("NavMesh_Procedural").AddComponent<NavMeshSurface>();
        navMesh.gameObject.layer = LayerMask.NameToLayer("Ground");
        navMesh.gameObject.AddComponent<BoxCollider>();
        navMesh.transform.localScale = new Vector3(width, 1, height);
        navMesh.transform.parent = transform;
        navMesh.transform.localPosition = Vector3.down / 2;
        navMesh.layerMask = LayerMask.GetMask("Ground", "VisibleWalls");
        navMesh.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        navMesh.collectObjects = CollectObjects.Children;

        //Place generated meshs insive the navmesh surface so we can bake only it's children
        foreach (Transform generatedMesh in generatedMeshs)
            generatedMesh.parent = navMesh.transform;

        navMesh.BuildNavMesh();
#endif
    }

    void FillMapFromTexture ()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                map[x, y] = texture.GetPixel(x, y) == Color.black ? 1 : 0;
            }
        }
    }

    int[,] FlipArray (int[,] arrayToFlip)
    {
        int rows = arrayToFlip.GetLength(0);
        int columns = arrayToFlip.GetLength(1);
        int[,] flippedArray = new int[rows, columns];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                //horizontal
                //flippedArray[i, j] = arrayToFlip[(rows - 1) - i, j];
                //vertical
                flippedArray[i, j] = arrayToFlip[i, (columns - 1) - j];
            }
        }
        return flippedArray;
    }

    //public int[,] GetMapInfo()
    //{
    //    int[,] tempMap = new int[texture.width,texture.height];

    //    for (int x = 0; x < width; x++)
    //        for (int y = 0; y < height; y++)
    //            tempMap[x, y] = texture.GetPixel(x, y) == Color.black ? 1 : 0;

    //    return tempMap;
    //}

    Vector2 selectedNode = Vector2.zero;

    bool isBuilding = false;
    bool isDestroying = false;

    List<Vector2> selectedNodes = new List<Vector2>();

    void Update()
    {
        if (Camera.main == null)
            return;

        if (!Input.GetKey(KeyCode.C))
            return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Input.GetMouseButtonDown(0))
        {
            isBuilding = true;
        }
        if (Input.GetMouseButtonDown(1))
        {
            isDestroying = true;
        }
        
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, LayerMaskUtils.Defaults.Ground))
        {
            selectedNode = new Vector2(Mathf.FloorToInt(transform.position.x + hit.point.x + (width / 2)), Mathf.Abs((Mathf.FloorToInt(Mathf.Abs(hit.point.z) - Mathf.Abs(transform.position.z) - (height / 2)) - height)) - height);

            if (isBuilding || isDestroying)
            {
                if (selectedNode.x > 0 && selectedNode.x < map.GetLength(0) && selectedNode.y > 0 && selectedNode.y < map.GetLength(1))
                {
                    if (!selectedNodes.Contains(selectedNode))
                        selectedNodes.Add(selectedNode);
                }
            }
        }
        
        //RaycastHit hit;
        //if (Physics.Raycast(ray, out hit, LayerMaskUtils.Defaults.Ground))
        //{
        //    selectedNode = new Vector2(Mathf.FloorToInt(transform.position.x + hit.point.x + (width / 2)), Mathf.Abs((Mathf.FloorToInt(Mathf.Abs(hit.point.z) - Mathf.Abs(transform.position.z) - (height / 2)) - height)) - height);

        //    if (Input.GetMouseButtonDown(1))
        //    {
        //        Debug.Log($"Selected Node: {selectedNode}  Pos: {new Vector2(hit.point.x, hit.point.z)}");

        //        map[(int)selectedNode.x, (int)selectedNode.y] = map[(int)selectedNode.x, (int)selectedNode.y] == 0 ? 1 : 0;
        //        GenerateMapFromGrid(map, false);

        //    }
        //}
        //else
        //{
        //    selectedNode = Vector2.zero;
        //}

        if (Input.GetMouseButtonUp(0))
        {
            isBuilding = false;

            //int value = map[(int)selectedNodes[0].x, (int)selectedNodes[0].y] == 0 ? 1 : 0;

            foreach (Vector2 v in selectedNodes)
                map[(int) v.x, (int) v.y] = 1;//map[(int)v.x, (int)v.y] == 0 ? 1 : 0;

            GenerateMapFromGrid(map, false);
            selectedNodes.Clear();
        }

        if (Input.GetMouseButtonUp(1))
        {
            isDestroying = false;

            foreach (Vector2 v in selectedNodes)
                map[(int)v.x, (int)v.y] = 0;//map[(int)v.x, (int)v.y] == 0 ? 1 : 0;

            GenerateMapFromGrid(map, false);
            selectedNodes.Clear();
        }

    }

    [SerializeField]
    float selectedNodeGizmoSize = 0.7f;
    void OnDrawGizmos ()
    {
        if (!drawGizmos)
            return;

		if (map != null) {
			for (int x = 0; x < width; x ++) {
				for (int y = 0; y < height; y ++) {
					Gizmos.color = (map[x,y] == 1)?Color.black:Color.white;

				    if (selectedNodes.Contains(new Vector2(x, y)))
				    {
				        if(isBuilding)
				            Gizmos.color = Color.green;
                        else if (isDestroying)
				            Gizmos.color = Color.red;
                        else
				            Gizmos.color = Color.yellow;
                    }

				    //if (selectedNode == new Vector2(x,y))


					Vector3 pos = transform.position +  new Vector3(-width /2 + x + .5f,0, -height /2 + y+.5f);
					Gizmos.DrawCube(pos,Vector3.one * selectedNodeGizmoSize);
				}
			}
		}
    }
#if MASTER_CLIENT
    public ParamTable GetResyncData ()
    {
        ParamTable table = ParamTable.GetNew();
        table[ParamCode.Position] = transform.position;
        table[ParamCode.LogicalIndex01] = texture.format;
        table[ParamCode.LogicalIndex02] = texture.GetRawTextureData();
        table[ParamCode.LogicalIndex03] = texture.width;
        table[ParamCode.LogicalIndex04] = texture.height;
        return table;
    }
#endif
}
