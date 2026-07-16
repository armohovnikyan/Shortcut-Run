using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

public class Bot : MonoBehaviour
{
    public NavMeshAgent Agent;
    public MeshRenderer Renderer;
    Vector3 Destination;

    void Start()
    {
        Agent = GetComponent<NavMeshAgent>();
        GameManager.Instance.RegistrRunner(transform);
    }
    public void Spawn(Transform Finish, Material ShirtColor,Material PantsColor,Material HairColor)
    {
        Material[] mats = Renderer.materials; 
        mats[0] = ShirtColor;
        mats[1] = PantsColor;
        mats[5] = HairColor;
        Renderer.materials = mats;    

       Destination = Finish.position;
    }
    public void StartGoing()
    {
        Agent.SetDestination(Destination);
    }
}
