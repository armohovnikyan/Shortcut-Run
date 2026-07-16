using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

public class Bot : MonoBehaviour
{
    public NavMeshAgent Agent;
    Vector3 Destination;
    public void Spawn(Transform Finish)
    {
       Destination = Finish.position;
    }
    public void StartGoing()
    {
        Agent.SetDestination(Destination);
    }
}
