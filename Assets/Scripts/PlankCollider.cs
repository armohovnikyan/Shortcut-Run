using System.Collections.Generic;
using UnityEngine;

public class PlankCollector : MonoBehaviour
{
    [Header("Настройки сбора досок")]
    public GameObject plankPrefab;    
    public Transform stackPosition;   
    public float plankHeight = 0.15f;   

    [Header("Эффект покачивания рук")]
    public float swaySpeed = 8f;        
    public float swayAmountX = 0.05f;   
    public float swayAmountY = 0.03f;   

    private List<GameObject> _collectedPlanks = new List<GameObject>();
    private Vector3 _initialStackLocalPos;
    private float _timer = 0f;

    void Start()
    {
        if (stackPosition != null)
            _initialStackLocalPos = stackPosition.localPosition;
    }

    void Update()
    {
        AnimateSway();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Plank"))
        {
            Destroy(other.gameObject);
            AddPlankToStack();
        }
    }

    void AnimateSway()
    {
        if (stackPosition == null || _collectedPlanks.Count == 0) return;

        _timer += Time.deltaTime * swaySpeed;

        float swayX = Mathf.Sin(_timer) * swayAmountX;
        float swayY = Mathf.Cos(_timer * 2) * swayAmountY;

        stackPosition.localPosition = _initialStackLocalPos + new Vector3(swayX, swayY, 0);

        float swayTiltZ = -Mathf.Sin(_timer) * 3f;
        stackPosition.localRotation = Quaternion.Euler(0, 0, swayTiltZ);
    }

  

    void AddPlankToStack()
    {
        if (plankPrefab == null || stackPosition == null) return;

        GameObject newPlank = Instantiate(plankPrefab); 
        newPlank.transform.SetParent(stackPosition);

        float spawnYOffset = _collectedPlanks.Count * plankHeight;
        newPlank.transform.localPosition = new Vector3(0, spawnYOffset, 0);
        newPlank.transform.localRotation = Quaternion.Euler(0,90,0);

        Vector3 parentScale = stackPosition.lossyScale;
        float scaleX = parentScale.x != 0 ? parentScale.x : 1f;
        float scaleY = parentScale.y != 0 ? parentScale.y : 1f;
        float scaleZ = parentScale.z != 0 ? parentScale.z : 1f;

        float targetWidth = 1.0f;
        float targetHeight = 0.5f;
        float targetLength = 3f;

        newPlank.transform.localScale = new Vector3(
            targetWidth / scaleX,
            targetHeight / scaleY,
            targetLength / scaleZ
        );

        _collectedPlanks.Add(newPlank);
    }
}