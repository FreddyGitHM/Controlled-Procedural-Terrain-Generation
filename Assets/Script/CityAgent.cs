using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Terrain))]

public class CityAgent : MonoBehaviour
{
    //Terrain data
    private Terrain _terrain;
    private TerrainData _td;
    private int _x;
    private int _y;
    private float[,] _heightmap;
    private Vector3 _terrainPos;

    //City agent
    public int cityAgentsNr;
    public int cityTokens;
    public GameObject roadGameObject;
    public GameObject houseGameObject;
    public int gap;
    [Range(5, 20)] public int roadLenght;
    
    public int MaxNHouse => gap == 1 ? (roadLenght / gap) - 2 : (roadLenght / gap) - 1;
    
    [HideInInspector]
    public int numberOfHouse = 1;

    // OnDrawGizmos
    private List<Vector2Int> _points;
    private bool _start;
    
    // Instance of this class
    public static CityAgent Instance;
    
    private void Awake()
    {

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

    }

    void Start()
    {

        //Getting terrain information
        _terrain = GetComponent<Terrain>();
        _td = _terrain.terrainData;
        _x = _td.heightmapResolution;
        _y = _td.heightmapResolution;
        _terrainPos = _terrain.GetPosition();

        //Initialize heightmap
        _heightmap = new float[_x, _y];

        _points = new List<Vector2Int>();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        if (_start)
        {
            foreach (var point in _points)
            {
                Gizmos.DrawSphere(GetPoint(point), .2f);
            }
        }
    }

    private Vector3 GetPoint(Vector2 location)
    {
        Vector3 origin = new Vector3(location.x + _terrainPos.x, _td.size.y + 10, location.y + _terrainPos.z);

        //Send the raycast.
        Physics.Raycast(origin, Vector3.down, out var hit);

        return hit.point;
    }

    public IEnumerator Action()
    {
        _heightmap = _td.GetHeights(0, 0, _x, _y);
        
        Debug.Log("Started generating cities...");

        List<Vector2Int> validPoints = GetValidPoints();
        Debug.Log("Number of valid points: " + validPoints.Count);
        
        for (int i = 0; i < cityAgentsNr; i++)
        {

            Vector2Int location = validPoints[Random.Range(0, validPoints.Count)];

            Vector2Int previousLocation = location;

            location = GetNewLocation(location, previousLocation);

            for (int j = 0; j < cityTokens; j++)
            {

                CreateRoad(location, previousLocation);
                CreateHouse(location, previousLocation);

                yield return new WaitForEndOfFrame();

                Vector2Int tmp = location;
                location = GetNewLocation(location, previousLocation);

                if (location == tmp)
                {
                    Debug.Log("Agent can't find good point to place road");
                    break;
                }

                previousLocation = tmp;
            }

        }
        
        Debug.Log("Finished generating cities...");

        yield return TreeAgent.Instance.Action();
    }

    private List<Vector2Int> GetValidPoints()
    {

        float ah = AverageHeight();
        Debug.Log("Average height of the island: " + ah);

        for (int i = 0; i < _x; i++)
        {
            for (int j = 0; j < _y; j++)
            {
                if (_heightmap[j, i] > ah + .05f && CheckSteepness(new Vector2(i, j)))
                {
                    _points.Add(new Vector2Int(i, j));
                }
            }
        }

        return _points;
    }

    private float AverageHeight()
    {

        float sum = 0.0f;
        int nPoints = 0;

        for (int i = 0; i < _x; i++)
        {
            for (int j = 0; j < _y; j++)
            {
                if (_heightmap[j, i] > 0.01f)
                {
                    sum += _heightmap[j, i];
                    nPoints++;
                }
            }
        }

        return sum / nPoints;
    }

    private bool CheckSteepness(Vector2 location)
    {
        //Create origin for raycast that is above the terrain.
        Vector3 origin = new Vector3(location.x + _terrainPos.x, _td.size.y + 10f, location.y + _terrainPos.z);

        //Send the raycast.
        Physics.Raycast(origin, Vector3.down, out var hit);

        return Vector3.Angle(hit.normal, Vector3.up) < 7.0f;
    }

    private Vector2Int GetNewLocation(Vector2Int location, Vector2Int previousLocation)
    {
        Vector2Int[] candidates = previousLocation != location ? GetCandidates(location, previousLocation) : GetCandidates(location);

        List<Vector2Int> checkedCandidates = new List<Vector2Int>();

        foreach (var candidate in candidates)
        {
            if (CheckCandidate(candidate, location))
            {
                checkedCandidates.Add(candidate);
            }
        }

        return checkedCandidates.Count != 0 ? checkedCandidates[Random.Range(0, checkedCandidates.Count)] : location;
    }

    private Vector2Int[] GetCandidates(Vector2Int location)
    {

        Vector2Int[] points =
        {
            location + Vector2Int.down * roadLenght,
            location + Vector2Int.up * roadLenght,
            location + Vector2Int.right * roadLenght,
            location + Vector2Int.left * roadLenght
        };

        return points;
    }

    private Vector2Int[] GetCandidates(Vector2Int location, Vector2 prevLocation)
    {

        Vector2 dir = (location - prevLocation).normalized;
        Vector2 perpendicularDir = Vector2.Perpendicular(dir);

        Vector2Int[] points = new Vector2Int[3];

        points[0] = location + new Vector2Int((int)dir.x, (int)dir.y) * roadLenght;
        points[1] = location + new Vector2Int((int)perpendicularDir.x, (int)perpendicularDir.y) * roadLenght;
        points[2] = location - new Vector2Int((int)perpendicularDir.x, (int)perpendicularDir.y) * roadLenght;

        return points;
    }

    // @param
    // location = k
    // prevLocation = location
    private bool CheckCandidate(Vector2 location, Vector2 previousLocation)
    {

        // Check if the location is inside the terrain
        if (!(location.x > 0 && location.x <= (_x - 2)) || !(location.y > 0 && location.y <= (_y - 2)))
        {
            return false;
        }

        // Check the steepness of the point
        if (!CheckSteepness(location))
        {
            return false;
        }

        // The direction in which the road will be placed
        Vector2 dir = (location - previousLocation).normalized;

        // Perpendicular direction that helps to check the right and left point
        Vector2 perpendicularDir = Vector2.Perpendicular(dir);

        RaycastHit hit1;
        RaycastHit hit2;

        float dist = Vector2.Distance(location, previousLocation);

        // In order to not check the points near the previousLocation(location, not k)
        previousLocation += dir;

        for (int i = 0; i < dist - 1; i++)
        {
            Vector2 right = previousLocation + perpendicularDir;
            Vector2 right2 = previousLocation + perpendicularDir * 2;

            Vector2 left = previousLocation - perpendicularDir;
            Vector2 left2 = previousLocation - perpendicularDir * 2;

            bool isHit1 = Physics.Raycast(new Vector3(right.x + _terrainPos.x, _td.size.y + 10, right.y + _terrainPos.z), Vector3.down, out hit1, Mathf.Infinity);
            bool isHit2 = Physics.Raycast(new Vector3(right2.x + _terrainPos.x, _td.size.y + 10, right2.y + _terrainPos.z), Vector3.down, out hit2, Mathf.Infinity);
            if (isHit1 && isHit2)
            {
                if ((hit1.collider.name == "Quad") != (hit2.collider.name == "Quad"))
                {
                    return false;
                }
            }

            isHit1 = Physics.Raycast(new Vector3(left.x + _terrainPos.x, _td.size.y + 10, left.y + _terrainPos.z), Vector3.down, out hit1, Mathf.Infinity);
            isHit2 = Physics.Raycast(new Vector3(left2.x + _terrainPos.x, _td.size.y + 10, left2.y + _terrainPos.z), Vector3.down, out hit2, Mathf.Infinity);
            if (isHit1 && isHit2)
            {
                if ((hit1.collider.name == "Quad") != (hit2.collider.name == "Quad"))
                {
                    return false;
                }
            }

            // Check if along the direction where will be placed the road, there is already another road
            isHit1 = Physics.Raycast(new Vector3(previousLocation.x + _terrainPos.x, _td.size.y + 10, previousLocation.y + _terrainPos.z), Vector3.down, out hit1, Mathf.Infinity);
            if (isHit1)
            {
                if (hit1.collider.name == "Quad")
                {
                    return false;
                }
            }

            previousLocation += dir;

        }

        return true;
    }

    private void CreateRoad(Vector2 location, Vector2 prevLocation)
    {
        Vector3 worldLocation = GetPoint(new Vector2(location.x, location.y));
        Vector3 prevWorldLocation = GetPoint(new Vector2(prevLocation.x, prevLocation.y));

        Vector3 dir = (worldLocation - prevWorldLocation).normalized;

        float length = Vector3.Distance(worldLocation, prevWorldLocation);
        Quaternion roadRot = Quaternion.identity;
        if (dir != Vector3.zero)
        {
            roadRot = Quaternion.LookRotation(dir, Vector3.up);
        }
        Vector3 scaleRoad = new Vector3(1.0f, 1.0f, length);

        GameObject road = Instantiate(roadGameObject, worldLocation, roadRot);
        road.transform.Translate(Vector3.up * .1f);
        road.transform.Translate(-Vector3.forward * ((length + 1.0f) * 0.5f));
        road.transform.localScale = scaleRoad;
    }

    private void CreateHouse(Vector2 location, Vector2 prevLocation)
    {

        Vector3 worldLocation = GetPoint(new Vector2(location.x, location.y));
        Vector3 prevWorldLocation = GetPoint(new Vector2(prevLocation.x, prevLocation.y));

        Vector3 dir = (worldLocation - prevWorldLocation).normalized;

        Vector3 housePos = Vector3.Lerp(worldLocation, prevWorldLocation, 0.5f);
        Quaternion houseRot = Quaternion.identity;
        if (dir != Vector3.zero)
        {
            houseRot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
        }

        GameObject house = Instantiate(houseGameObject, housePos, houseRot);
        house.transform.Translate(Vector3.up * 0.5f);
        house.transform.Translate(-Vector3.forward);

        if (numberOfHouse == 1)
        {
            return;
        }

        Vector3 rightPos = housePos;
        Vector3 leftPos = housePos;

        for (int i = 0; i < numberOfHouse - 1; i++)
        {

            if (i % 2 == 0)
            {
                rightPos += dir * gap;
                house = Instantiate(houseGameObject, rightPos, houseRot);
                house.transform.Translate(Vector3.up * 0.5f);
                house.transform.Translate(-Vector3.forward);
            }
            else
            {
                leftPos -= dir * gap;
                house = Instantiate(houseGameObject, leftPos, houseRot);
                house.transform.Translate(Vector3.up * 0.5f);
                house.transform.Translate(-Vector3.forward);
            }

        }
    }
}
