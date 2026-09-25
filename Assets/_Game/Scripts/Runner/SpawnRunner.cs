using UnityEngine;
using System.Collections.Generic;
public class SpawnRunner : MonoBehaviour
{
    public Runner Spawn(Runner runner)
    {
        return Instantiate(runner, transform.position, Quaternion.identity);
    }
    public List<Runner> RegisterRunner(List<Runner> runners, Runner agent)
    {
        runners.Add(agent);
        return runners;
    }
}
