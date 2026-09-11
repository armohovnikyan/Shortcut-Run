using UnityEngine;

public class MovingLogic: MonoBehaviour
{
    void Move(Vector3 Target, float speed)
    {

    Vector3 pos = Vector3.MoveTowards(transform.position,Target,speed* Time.deltaTime);  
    Vector3 direction = Target - transform.position;

    direction.y = 0f;
    
    if (direction != Vector3.zero)
       transform.rotation = Quaternion.LookRotation(direction);

    transform.position = pos;
    }
}
