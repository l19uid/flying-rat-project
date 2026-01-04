using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class World : MonoBehaviour
{
    public enum BlockType
    {
        AIR,
        GRASS,
        STONE,
        SNOW
    }
    
    public GameObject chunkPrefab;
    public int chunkSize = 16;
    public int renderDistance = 4;
    public int seed = 0;

    private Dictionary<Vector2Int, Chunk> _chunks = new Dictionary<Vector2Int, Chunk>();
    public Vector3 playerPosition;
    public Vector2 playerChunkPosition;
    [Header("Noise Settings")]
    
    [Header("Main noise")]
    public float mainNoiseFrequency = 0.0001f;
    public int mainNoiseOctaves = 1;
    public FastNoiseLite.NoiseType mainNoiseType = FastNoiseLite.NoiseType.OpenSimplex2;
    public FastNoiseLite.FractalType mainNoiseFractalType = FastNoiseLite.FractalType.FBm;
    [Header("Hill noise")]
    public float hillNoiseFrequency = 0.0001f;
    public int hillNoiseOctaves = 1;
    public FastNoiseLite.NoiseType hillNoiseType = FastNoiseLite.NoiseType.OpenSimplex2;
    public FastNoiseLite.FractalType hillNoiseFractalType = FastNoiseLite.FractalType.FBm;
    [Header("Detail noise")]
    public float detailNoiseFrequency = 0.0001f;
    public int detailNoiseOctaves = 1;
    public FastNoiseLite.NoiseType detailNoiseType = FastNoiseLite.NoiseType.OpenSimplex2;
    public FastNoiseLite.FractalType detailNoiseFractalType = FastNoiseLite.FractalType.FBm;
    [Header("Mountain noise")]
    public float mountainNoiseFrequency = 0.0001f;
    public int mountainNoiseOctaves = 1;
    public FastNoiseLite.NoiseType mountainNoiseType = FastNoiseLite.NoiseType.OpenSimplex2;
    public FastNoiseLite.FractalType mountainNoiseFractalType = FastNoiseLite.FractalType.FBm;
    [Header("Color noise")]
    public float colorNoiseFrequency = 0.0001f;
    public int colorNoiseOctaves = 1;
    public FastNoiseLite.NoiseType colorNoiseType = FastNoiseLite.NoiseType.OpenSimplex2;
    public FastNoiseLite.FractalType colorNoiseFractalType = FastNoiseLite.FractalType.FBm;
    public Color32 grassColor;
    public Color32 grassSecondaryColor;
    
    [Header("Height generation settings")]
    public float mainHeightMultiplier = 48f;
    public float hillHeightMultiplier = 64f;
    public float detailHeightMultiplier = 96f;
    public float mountainHeightMultiplier = 16f;
    public float mountainHeightPower = 2f;

    private FastNoiseLite _mainNoise;
    private FastNoiseLite _hillNoise;
    private FastNoiseLite _detailNoise;
    private FastNoiseLite _mountainNoise;
    private FastNoiseLite _mountainDetailNoise;
    private FastNoiseLite _negativeNoise;
    private FastNoiseLite _colorNoise;

    public static World Instance;
    private readonly List<Vector3> _placedBlocks = new List<Vector3>();
    private readonly Queue<Vector2Int> _chunkGenerationQueue = new Queue<Vector2Int>();
    [Header("Chunk generation")]
    public int chunksPerSecond = 1;
    private float _lastChunkGenerationTime = 0f;

    private Vector2 _baseOffset;

    // Noise initialization, dont mind all the garbage
    void Start()
    {
        chunksPerSecond = renderDistance * 5;
        _lastChunkGenerationTime = Time.time;
        _baseOffset = new Vector2(Random.Range(-100, 100), Random.Range(-100, 100)*100);
        Instance = this;
        seed = Random.Range(int.MinValue, int.MaxValue);
        _mainNoise = new FastNoiseLite();
        _mainNoise.SetNoiseType(mainNoiseType);
        _mainNoise.SetFractalType(mainNoiseFractalType);
        _mainNoise.SetFractalOctaves(mainNoiseOctaves);      
        _mainNoise.SetFrequency(mainNoiseFrequency);
        _mainNoise.SetSeed(seed);
        
        _hillNoise = new FastNoiseLite();
        _hillNoise.SetNoiseType(hillNoiseType);
        _hillNoise.SetFractalType(hillNoiseFractalType);
        _hillNoise.SetFractalOctaves(hillNoiseOctaves);      
        _hillNoise.SetFrequency(hillNoiseFrequency);
        _hillNoise.SetSeed(seed);

        _detailNoise = new FastNoiseLite();
        _detailNoise.SetNoiseType(detailNoiseType);
        _detailNoise.SetFractalType(detailNoiseFractalType);
        _detailNoise.SetFractalOctaves(detailNoiseOctaves);      
        _detailNoise.SetFrequency(detailNoiseFrequency);
        _detailNoise.SetSeed(seed);

        _mountainNoise = new FastNoiseLite();
        _mountainNoise.SetNoiseType(mountainNoiseType);
        _mountainNoise.SetFractalType(mountainNoiseFractalType);
        _mountainNoise.SetFractalOctaves(mountainNoiseOctaves);
        _mountainNoise.SetFrequency(mountainNoiseFrequency);
        _mountainNoise.SetSeed(seed);
        _mountainDetailNoise = new FastNoiseLite();
        _mountainDetailNoise.SetNoiseType(mountainNoiseType);
        _mountainDetailNoise.SetFractalType(mountainNoiseFractalType);
        _mountainDetailNoise.SetFractalOctaves(mountainNoiseOctaves + 1);
        _mountainDetailNoise.SetFrequency(mountainNoiseFrequency);
        _mountainDetailNoise.SetSeed(seed);
        
        _colorNoise = new FastNoiseLite();
        _colorNoise.SetNoiseType(colorNoiseType);
        _colorNoise.SetFractalType(colorNoiseFractalType);
        _colorNoise.SetFractalOctaves(colorNoiseOctaves);
        _colorNoise.SetFrequency(colorNoiseFrequency);
        _colorNoise.SetSeed(seed);
        
        RequestChunkUpdate();
    }
    
    // Reason why this is in the world generator is because i didnt want to pass all the noise settings to the chunk
    // And its feels easier to manage from here
    // Dont mind all the multipliers and stuff its just to tweak the terrain generation, it was just me experimenting having some fun
    // Im a big fan of procedural generation but i have no clue what im doing
    public int GetHeightAtCord(int x, int z, Vector2 position, int size = 1)
    {
        x += (int)_baseOffset.x;
        z += (int)_baseOffset.y;
        
        int height = 64;
        
        float mainHeight = Math.Abs(_mainNoise.GetNoise(x + position.x * size, z + position.y * size)) * mainHeightMultiplier;
        float hillHeight = Math.Abs(_hillNoise.GetNoise(x + position.x * size, z + position.y * size)) * hillHeightMultiplier;
        float detailHeight = Math.Abs(_detailNoise.GetNoise(x + position.x * size,z + position.y * size)) * detailHeightMultiplier;
        float mountainHeight = Math.Abs(_mountainNoise.GetNoise(x + position.x * size,z + position.y * size));
        
        mountainHeight = (float)Math.Pow((mountainHeight * mountainHeightMultiplier),
                                        mountainHeightPower + Math.Clamp(_mountainDetailNoise.GetNoise(x + position.x * size, z + position.y * size),.1f,.25f));
        
        height += (int)(mainHeight + hillHeight + detailHeight + mountainHeight);
        height /= 5; 
        height = Mathf.Clamp(height, 0, size*size-1);
        return height;
    }
    
    private void LateUpdate()
    {
        while (_chunkGenerationQueue.Count > 0 && Time.time > _lastChunkGenerationTime + 1f / chunksPerSecond)
        {
            _lastChunkGenerationTime = Time.time;
            Vector2Int pos = _chunkGenerationQueue.Dequeue();

            // Safety check in case it was generated earlier
            if (_chunks.ContainsKey(pos))
                continue;

            Vector3 worldPos = new Vector3(pos.x * chunkSize, 0, pos.y * chunkSize);
            Chunk chunk = Instantiate(chunkPrefab, worldPos, Quaternion.identity)
                .GetComponent<Chunk>();

            chunk.GenerateChunk(pos, chunkSize, this);
            _chunks.Add(pos, chunk);

        }
    }

    
    void RequestChunkUpdate()
    {
        // Disable based on render distance
        // Key is position, value is chunk
        foreach (var chunk in _chunks)
        {
            Vector2 delta = playerChunkPosition - chunk.Key;
            if (delta.sqrMagnitude > renderDistance * renderDistance)
                chunk.Value.gameObject.SetActive(false);
        }
        
        // Enable/generate based on render distance
        for (int x = -renderDistance + (int)playerChunkPosition.x;
             x < renderDistance + (int)playerChunkPosition.x; x++)
        {
            for (int y = -renderDistance + (int)playerChunkPosition.y;
                 y < renderDistance + (int)playerChunkPosition.y; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);

                if (!_chunks.ContainsKey(pos))
                    _chunkGenerationQueue.Enqueue(pos);
                else
                    _chunks[pos].gameObject.SetActive(true);
            }
        }
    }
    
    private void Update()
    {
        playerPosition = Player.Instance.transform.position;
        int x = Mathf.FloorToInt(playerPosition.x / chunkSize);
        int z = Mathf.FloorToInt(playerPosition.z / chunkSize);
        if (new Vector2(x, z) != playerChunkPosition)
        {
            RequestChunkUpdate();
            playerChunkPosition = new Vector2(x, z);
        }
    }

    public Color32 GetColor(int x, int y, int z, Vector2 position)
    {
        float colorNoise = this._colorNoise.GetNoise(x + position.x * chunkSize, z + position.y * chunkSize)*3+.5f;
        Color32 color = Color32.Lerp(grassColor, grassSecondaryColor, colorNoise);
        return color;
    }
    
    public void PlaceBlockAt(Vector3 position, BlockType type)
    {
        Vector2 chunkPos = new Vector2(Mathf.FloorToInt(position.x / chunkSize), Mathf.FloorToInt(position.z / chunkSize));
        foreach (var chunk in _chunks)
        {
            if(chunk.Value.GetComponent<Chunk>().GetPosition() == chunkPos)
            {
                _placedBlocks.Add(position);
                chunk.Value.GetComponent<Chunk>().PlaceBlockAt(position, type);
                return;
            }
        }
    }
    
    public void RemoveBlockAt(Vector3 breakPosition)
    {
        Vector2 chunkPos = new Vector2(Mathf.FloorToInt(breakPosition.x / chunkSize), Mathf.FloorToInt(breakPosition.z / chunkSize));
        foreach (var chunk in _chunks)
        {
            if(chunk.Value.GetComponent<Chunk>().GetPosition() == chunkPos)
            {
                _placedBlocks.Remove(breakPosition);
                chunk.Value.GetComponent<Chunk>().RemoveBlockAt(breakPosition);
                return;
            }
        }
    }
    
    public float GetBlockTimeToBreak(BlockType lookingAtBlockType)
    {
        switch (lookingAtBlockType)
        {
            case BlockType.AIR:
                return 0f;
            case BlockType.GRASS:
                return 1f;
            case BlockType.STONE:
                return 2f;
            case BlockType.SNOW:
                return .5f;
            default:
                return 0f;
        }
    }

    public BlockType GetBlockAt(Vector3 pos)
    {
        Vector2 chunkPos = new Vector2(Mathf.FloorToInt(pos.x / chunkSize), Mathf.FloorToInt(pos.z / chunkSize));
        foreach (var chunk in _chunks)
        {
            if(chunk.Value.GetComponent<Chunk>().GetPosition() == chunkPos)
            {
                return chunk.Value.GetComponent<Chunk>().GetBlockAt(pos);
            }
        }

        return BlockType.AIR;
    }
}
