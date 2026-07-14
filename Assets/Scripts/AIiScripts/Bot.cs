using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

public class Bot : MonoBehaviour
{
    public NavMeshAgent Agent;
    public Transform Finish;

    void Start()
    {
       // Agent = GetComponent<NavMeshAgent>();
    }
    public void BuildWay()
    {
        Agent.SetDestination(Finish.position);
    }
}
