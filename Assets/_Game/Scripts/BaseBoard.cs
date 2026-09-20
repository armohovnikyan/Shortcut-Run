using UnityEngine;
//using boardSO namespace
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(Collider))]
public abstract class BaseBoard : MonoBehaviour
{
    [SerializeField] protected BoardSO boardData;
    private MeshRenderer meshRenderer;  
    protected virtual void Awake()
    {
        SetScale();
        SetMaterial();
    }

    private void SetScale()
    {
        transform.localScale = boardData.Scale;
    }
    protected void SetMaterial()
    {
        meshRenderer.material = boardData.Material;
    }
}
