using UnityEngine;
//using boardSO namespace
[RequireComponent(typeof(Collider))]
public abstract class BaseBoard : MonoBehaviour
{
    [SerializeField] protected BoardSO boardSO;
}
