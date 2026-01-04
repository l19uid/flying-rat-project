using System.Collections.Generic;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class Chunk : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;
    public Mesh mesh;
    public Material material;
    
    private Vector2 _position;
    private int _size;
    private int _height;
    
    private World.BlockType[,,] _chunkData;
    
    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<int> _triangles = new List<int>();
    private readonly List<Vector2> _uv = new List<Vector2>();
    private readonly List<Color32> _colors = new List<Color32>();
    
    private World _world;
    private int[,] _heightMap;
    
    private BoxCollider _triggerCollider;
    private MeshCollider _meshCollider;
    public void GenerateChunk(Vector2 pos, int chunkSize,  World world)
    {
        _meshCollider = gameObject.GetComponent<MeshCollider>();
        _position = pos;
        _size = chunkSize;
        _height = _size * _size / 2; // Smaller for speed
        _world = world;
        
        // Optimizing list allocations
        int estimatedFaces = _size * _size * 3;
        _vertices.Capacity = estimatedFaces * 4;
        _triangles.Capacity = estimatedFaces * 6;
        _uv.Capacity = estimatedFaces * 4;
        _colors.Capacity = estimatedFaces * 4;
        
        // Adding trigger collider for activating/deactivating mesh collider since mesh colliders are expensive
        _triggerCollider = gameObject.AddComponent<BoxCollider>();
        _triggerCollider.isTrigger = true;  
        _triggerCollider.size = new Vector3(_size+8, _height, _size+8);
        _triggerCollider.center = new Vector3(_size / 2f, _height / 2f, _size / 2f);
        
        // This isnt really necessary but I had it for experimental purposes
        _heightMap = new int[_size, _size];
        
        // Create mesh
        mesh = new Mesh();
        meshFilter.mesh = mesh;
        meshRenderer.material = material;
        // Generate chunk data and mesh, then render it
        GenerateChunkData();
        GenerateMesh();
        RenderMesh();
    }
    
    private void GenerateChunkData()
    {
        _chunkData = new World.BlockType[_size, _height, _size];
        
        for (int x = 0; x < _size; x++)
        {
            for (int z = 0; z < _size; z++)
            {
                // Get height from world heightmap
                int heightAtCord = _world.GetHeightAtCord(x, z, _position, _size);
                // Go through all heights and assign block types
                for (int y = 0; y < _height; y++)
                {
                    if (y > heightAtCord)
                    {
                        _chunkData[x, y, z] = World.BlockType.AIR;
                    }
                    else if (y == heightAtCord)
                    {
                        // Top block always snow or grass
                        if (heightAtCord > 100)
                            _chunkData[x, y, z] = World.BlockType.SNOW;
                        else
                            _chunkData[x, y, z] = World.BlockType.GRASS;
                    }
                    else
                    {
                        _chunkData[x, y, z] = World.BlockType.STONE;
                    }
                }

                _heightMap[x, z] = heightAtCord;
            }
        }
    }

    public void GenerateMesh()
    {
        for (int x = 0; x < _size; x++)
        {
            for (int z = 0; z < _size; z++)
            {
                for (int y = 0; y < _height; y++)
                {
                    // Optimization: stop checking for blocks above the heightmap
                    if(_heightMap[x,z]+1 < y)
                        break;
                    if(_chunkData[x, y, z] == World.BlockType.AIR)
                        continue;
                    
                    // Generate voxel where there isnt air
                    GenerateVoxel(x,y,z);
                }
            }
        }
    }
    
    public void DestroyMesh()
    {
        _vertices.Clear();
        _triangles.Clear();
        _uv.Clear();
        _colors.Clear();
        mesh.Clear();
    }
    
    public void RenderMesh()
    {
        mesh.Clear();
        mesh.vertices = _vertices.ToArray();
        mesh.triangles = _triangles.ToArray();
        mesh.uv = _uv.ToArray();
        mesh.colors32 = _colors.ToArray();
        meshFilter.mesh = mesh;
        mesh.RecalculateNormals();
    }
    
    public void ActivateCollider()
    {
        _meshCollider.enabled = true;
        _meshCollider.sharedMesh = mesh;
    }
    
    public void DeactivateCollider()
    {
        _meshCollider.enabled = false;
    }
    
    private void GenerateVoxel(int x, int y, int z)
    {
        if (IsFaceVisible(x, y, z, Vector3.up))
            GenerateFace(x, y, z, FaceUp);
        if (IsFaceVisible(x, y, z, Vector3.down))
            GenerateFace(x, y, z, FaceDown);
        if (IsFaceVisible(x, y, z, Vector3.forward))
            GenerateFace(x, y, z, FaceForward);
        if (IsFaceVisible(x, y, z, Vector3.back))
            GenerateFace(x, y, z, FaceBack);
        if (IsFaceVisible(x, y, z, Vector3.right))
            GenerateFace(x, y, z, FaceRight);
        if (IsFaceVisible(x, y, z, Vector3.left))
            GenerateFace(x, y, z, FaceLeft);
    }

    private void GenerateFace(int x, int y, int z, FaceData faceData)
    {
        int vertexIndex = _vertices.Count;
        for (int i = 0; i < 4; i++)
        {
            _vertices.Add(faceData.vertices[i] + new Vector3(x, y, z));
        }
        
        for (int i = 0; i < 4; i++)
        {
            _colors.Add(GetColor(x, y, z));
        }
        
        for (int i = 0; i < 6; i++)
        {
            _triangles.Add(faceData.triangles[i] + vertexIndex);
        }
        
        for (int i = 0; i < 4; i++)
        {
            _uv.Add(faceData.uv[i]);
        }
    }

    private bool IsFaceVisible(int x, int y, int z, Vector3 faceNormal)
    {
        // Dont render faces under the chunk
        if (y + faceNormal.y < 0)
            return false;
        // Check if face is at chunk boundary
        // I was considering that instead of checking chunk width bounds we ask the world what the block on the neighbor chunk is
        // This turned out to be very slow and inefficient so I abandoned that idea
        
        // Check sides of chunk
        if (x + faceNormal.x < 0 || x + faceNormal.x >= _size || z + faceNormal.z < 0 || z + faceNormal.z >= _size)
            return true;

        // Actually check neighboring block
        return _chunkData[x + (int) faceNormal.x, y + (int) faceNormal.y, z + (int) faceNormal.z] == World.BlockType.AIR;
    }

    public class FaceData
    {
        public readonly Vector3[] vertices;
        public readonly int[] triangles;
        public readonly Vector3 normal;
        public readonly Vector2[] uv;

        public FaceData(Vector3[] vertices, int[] triangles, Vector3 normal, Vector2[] uv)
        {
            this.vertices = vertices;
            this.triangles = triangles;
            this.normal = normal;
            this.uv = uv;
        }
    }

    static readonly FaceData FaceUp = new FaceData(
        new[]
        {
            new Vector3(0, 1, 0),
            new Vector3(1, 1, 0),
            new Vector3(0, 1, 1),
            new Vector3(1, 1, 1)
        },
        new[] { 0, 2, 1, 1, 2, 3 },
        Vector3.up,
        new[]
        {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 0)
        }
    );

    public static readonly FaceData FaceDown = new FaceData(
        new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 0, 1),
            new Vector3(1, 0, 1)
        },
        new[] { 0, 1, 2, 3, 2, 1 },
        Vector3.down,
        new[]
        {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 0)
        }
    );

    public static readonly FaceData FaceForward = new FaceData(
        new[]
        {
            new Vector3(0, 0, 1),
            new Vector3(1, 0, 1),
            new Vector3(0, 1, 1),
            new Vector3(1, 1, 1)
        },
        new[] { 0, 1, 2, 3, 2, 1 },
        Vector3.forward,
        new[]
        {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 0)
        }
    );

    public static readonly FaceData FaceBack = new FaceData(
        new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(1, 1, 0)
        },
        new[] { 0, 2, 1, 1, 2, 3 },
        Vector3.back,
        new[]
        {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 0)
        }
    );

    public static readonly FaceData FaceRight = new FaceData(
        new[]
        {
            new Vector3(1, 0, 0),
            new Vector3(1, 0, 1),
            new Vector3(1, 1, 0),
            new Vector3(1, 1, 1)
        },
        new[] { 0, 2, 1, 1, 2, 3 },
        Vector3.right,
        new[]
        {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 0)
        }
    );

    public static readonly FaceData FaceLeft = new FaceData(
        new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 1),
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 1)
        },
        new[] { 0, 1, 2, 3, 2, 1 },
        Vector3.left,
        new[]
        {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 0)
        }
    );

    public Vector2 GetPosition()
    {
        return _position;
    }

    public void PlaceBlockAt(Vector3 placePos, World.BlockType type)
    {
        // Calculate relative position within chunk
        Vector3 rel = placePos - new Vector3(_position.x * _size, 0, _position.y * _size);

        int x = (int)rel.x;
        int y = (int)rel.y;
        int z = (int)rel.z;

        _chunkData[x, y, z] = type;
        
        // Check for out of bounds
        if(y > _height || y < 0)
            return;

        // Recompute height
        if (y > _heightMap[x, z])
            _heightMap[x, z] = y;
        
        // Regenerate mesh
        DestroyMesh();
        GenerateMesh();
        RenderMesh();
        ActivateCollider();
    }

    public void RemoveBlockAt(Vector3 breakPosition)
    {
        // Calculate relative position within chunk
        Vector3 rel = breakPosition - new Vector3(_position.x * _size, 0, _position.y * _size);

        int x = (int)rel.x;
        int y = (int)rel.y;
        int z = (int)rel.z;

        _chunkData[x, y, z] = World.BlockType.AIR;

        // Recompute height
        if (y == _heightMap[x, z])
        {
            for (int ny = y - 1; ny >= 0; ny--)
            {
                if (_chunkData[x, ny, z] != World.BlockType.AIR)
                {
                    _heightMap[x, z] = ny;
                    break;
                }

                if (ny == 0)
                    _heightMap[x, z] = 0;
            }
        }

        // Regenerate mesh
        DestroyMesh();
        GenerateMesh();
        RenderMesh();
        ActivateCollider();
    }

    private Color GetColor(int x, int y, int z)
    {
        World.BlockType type = _chunkData[x, y, z];
        switch (type)
        {
            case World.BlockType.GRASS:
                return World.Instance.GetColor(x, y, z, _position);
            case World.BlockType.STONE:
                return new Color(.25f, .25f, .35f);
            case World.BlockType.SNOW:
                return Color.white;
            default:
                return Color.clear;
        }
    }

    
    // Little trick to activate/deactivate colliders when player enters/exits chunk trigger
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ActivateCollider();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            DeactivateCollider();
        }
    }

    public World.BlockType GetBlockAt(Vector3 pos)
    {
        Vector3 relativePlacePos = pos - new Vector3(_position.x * _size, 0 ,_position.y * _size);
        return _chunkData[(int)relativePlacePos.x, (int)relativePlacePos.y, (int)relativePlacePos.z];
    }
}
