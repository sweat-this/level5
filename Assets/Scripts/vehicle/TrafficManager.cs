using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrafficManager : MonoBehaviour
{

    [SerializeField]
    GameObject[] _vehicleTargetPositions;

    [SerializeField]
    GameObject _vehicleSpawnLeftPosition;
    [SerializeField]
    GameObject _vehicleSpawnRightPosition;
    private Transform eastBoundRightTarget;
    private Transform westBoundLeftTarget;

    [SerializeField]
    List<VehicleController> _vehiclesList;

    [SerializeField]
    List<GameObject> _vehiclesPrefabsList;

    [SerializeField]
    List<GameObject> _customVehiclePrefabList;

    const string vehicleTargetsTag = "vehicle_position_marker";
    const string spawnLeftTag = "vehicle_spawn_left";
    const string spawnRightTag = "vehicle_spawn_right";

    const string leftSpawnText1 = "leftSpawn1";
    const string leftSpawnText2 = "leftSpawn2";
    const string rightSpawnText1 = "rightSpawn1";
    const string rightpawnText2 = "rightSpawn1";

    const string eastBoundLeftText = "eastBoundLeft";
    const string eastBoundRightText = "eastBoundRight";
    const string westBoundLeftText = "westBoundLeft";
    const string westBoundRightText = "westBoundRight";

    const string trafficManagerText = "traffic_manager";

    // create custom vehicle list for specific level
    [SerializeField]
    bool customVehicles;
    [SerializeField]
    bool trafficEnabled;

    public static TrafficManager instance;

    // Start is called before the first frame update
    void Awake()
    {
        instance = this;
        trafficEnabled = GameOptions.trafficEnabled;
        // NOTE : for testing purposes
        //GameOptions.trafficEnabled = true;

        // if traffic enabled
        if (trafficEnabled)
        {
            // get spawn points
            _vehicleSpawnLeftPosition = GameObject.FindGameObjectWithTag(spawnLeftTag);
            _vehicleSpawnRightPosition = GameObject.FindGameObjectWithTag(spawnRightTag);
            GameObject eastTarget = GameObject.Find(eastBoundRightText);
            GameObject westTarget = GameObject.Find(westBoundLeftText);
            eastBoundRightTarget = eastTarget != null ? eastTarget.transform : null;
            westBoundLeftTarget = westTarget != null ? westTarget.transform : null;

            if (_vehicleSpawnLeftPosition == null || _vehicleSpawnRightPosition == null
                || eastBoundRightTarget == null || westBoundLeftTarget == null)
            {
                Debug.LogError("TrafficManager is missing required spawn or target markers and has been disabled.");
                enabled = false;
                return;
            }

            // if vehicleslist is manually created
            if (customVehicles)
            {
                loadCustomVehiclePrefabs();
            }
            // else, load prefabs from folder
            else
            {
                loadVehiclePrefabs();
            }
        }
    }

    private void loadVehiclePrefabs()
    {
        // where are the prefabs, load them
        string path = "Prefabs/vehicle";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];

        // get all the objects in folder, create list of the vehicleControllers
        foreach (GameObject obj in objects)
        {
            VehicleController temp = obj.GetComponent<VehicleController>();
            VehiclesList.Add(temp);
            VehiclesPrefabsList.Add(obj);

            // this where to set direction and target
        }
        //spawn vehicles if traffic manager exists
        if (trafficManagerExists())
        {
            spawnVehiclePrefabs();
        }
    }

    private void loadCustomVehiclePrefabs()
    {
        if (trafficManagerExists())
        {
            // get all the objects in folder, create list of the vehicleControllers
            foreach (GameObject car in CustomVehiclePrefabList)
            {
                if (car != null)
                {
                    VehicleController temp = car.GetComponent<VehicleController>();
                    VehiclesList.Add(temp);
                }
            }
            spawnCustomVehiclePrefabs();
        }
    }

    public void spawnVehicle(int vehicleId, string direction, float waitTimeToRespawn)
    {
        // if traffic manager exists
        if (trafficManagerExists())
        {
            // find object in list by prefab
            //NOTE : if more than one with same id, error called
            //
            VehicleController vehiclePrefab = VehiclesList.Find(x => x.VehicleId == vehicleId);

            // call coroutine
            StartCoroutine(spawnVehicleCoRoutine(vehiclePrefab, direction, waitTimeToRespawn));
        }
    }

    private IEnumerator spawnVehicleCoRoutine(VehicleController vehicle, string direction, float waitTimeToRespawn)
    {
        // Instantiate first, then set Direction/CurrentTarget on the clone - not on `vehicle`
        // (the shared prefab-list entry) before instantiating. Setting fields on the shared
        // reference before a delay let two respawns of the same vehicle race: whichever
        // WaitForSeconds resolved second would silently overwrite the first's pending values on
        // the same object.
        if (direction == "left")
        {
            yield return new WaitForSeconds(waitTimeToRespawn);
            VehicleController clone = Instantiate(vehicle, _vehicleSpawnLeftPosition.transform.position, Quaternion.identity);
            clone.Direction = "right";
            clone.CurrentTarget = eastBoundRightTarget.position;
        }
        if (direction == "right")
        {
            yield return new WaitForSeconds(waitTimeToRespawn);
            VehicleController clone = Instantiate(vehicle, _vehicleSpawnRightPosition.transform.position, Quaternion.identity);
            clone.Direction = "left";
            clone.CurrentTarget = westBoundLeftTarget.position;
        }
    }

    private void spawnVehiclePrefabs()
    {
        // sort list by  mode id
        VehiclesList.Sort(sortByVehicleId);

        //instantiate vehicle at first postion
        int vehicleIndex = 0;

        // to prevent vehicles spawning on top of each other
        Vector3 VectorToAddToSpawn = new Vector3();

        // Instantiate first, then set Direction/FacingRight/CurrentTarget on the clone - not on
        // `v` (the shared prefab-list entry) before instantiating. `v` is reused across every
        // iteration of this loop and by spawnVehicleCoRoutine's respawns, so mutating it directly
        // was writing shared state that a concurrent respawn coroutine could stomp on.
        foreach (VehicleController v in VehiclesList)
        {
            if (vehicleIndex % 2 == 0)
            {
                VectorToAddToSpawn += new Vector3((-5 * vehicleIndex), 0, 0);
                VehicleController clone = Instantiate(v, (_vehicleSpawnLeftPosition.transform.position + VectorToAddToSpawn), Quaternion.identity);
                //direction to move vehicle towards
                clone.Direction = "right";
                clone.FacingRight = true;
                // set target to correct vector3
                clone.CurrentTarget = eastBoundRightTarget.position;
            }
            else
            {
                VectorToAddToSpawn += new Vector3((5 * vehicleIndex), 0, 0);
                VehicleController clone = Instantiate(v, (_vehicleSpawnRightPosition.transform.position + VectorToAddToSpawn), Quaternion.identity);
                //direction to move vehicle towards
                clone.Direction = "left";
                clone.FacingRight = false;
                // set target to vector3
                clone.CurrentTarget = westBoundLeftTarget.position;
            }
            vehicleIndex++;
        }
    }

    private void spawnCustomVehiclePrefabs()
    {
        // sort list by  mode id
        //VehiclesList.Sort(sortByVehicleId);

        //instantiate vehicle at first postion
        int vehicleIndex = 0;

        // to prevent vehicles spawning on top of each other
        Vector3 VectorToAddToSpawn = new Vector3();

        // Instantiate first, then set Direction/FacingRight/CurrentTarget on the clone - see
        // spawnVehiclePrefabs for why mutating the shared `v` reference before instantiate is
        // unsafe.
        foreach (VehicleController v in VehiclesList)
        {
            if (vehicleIndex % 2 == 0)
            {
                VectorToAddToSpawn += new Vector3((-7 * vehicleIndex), 0, 0);
                VehicleController clone = Instantiate(v, (_vehicleSpawnLeftPosition.transform.position + VectorToAddToSpawn), Quaternion.identity);
                //direction to move vehicle towards
                clone.Direction = "right";
                clone.FacingRight = true;
                // set target to correct vector3
                clone.CurrentTarget = eastBoundRightTarget.position;
            }
            else
            {
                VectorToAddToSpawn += new Vector3((7 * vehicleIndex), 0, 0);
                VehicleController clone = Instantiate(v, (_vehicleSpawnRightPosition.transform.position + VectorToAddToSpawn), Quaternion.identity);
                //direction to move vehicle towards
                clone.Direction = "left";
                clone.FacingRight = false;
                // set target to vector3
                clone.CurrentTarget = westBoundLeftTarget.position;
            }
            vehicleIndex++;
        }
    }

    static int sortByVehicleId(VehicleController m1, VehicleController m2)
    {
        return m1.VehicleId.CompareTo(m2.VehicleId);
    }

    private bool trafficManagerExists()
    {
        bool value;
        if (GameObject.FindGameObjectWithTag(trafficManagerText))
        {
            value = true;
        }
        else
        {
            value = false;
        }
        return value;
    }

    public List<VehicleController> VehiclesList { get => _vehiclesList; }
    public List<GameObject> VehiclesPrefabsList { get => _vehiclesPrefabsList; }
    public bool TrafficEnabled { get => trafficEnabled; set => trafficEnabled = value; }
    public List<GameObject> CustomVehiclePrefabList { get => _customVehiclePrefabList; set => _customVehiclePrefabList = value; }
}
