using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour, ICharacter
{
    [Header("Настройки движения")]
    public float forwardSpeed = 7f;   
    public float turnSpeed = 90f;    

    private CharacterController _characterController;
    private bool _isGameStarted = false;
    private float _currentTurnInput = 0f; 
    public int Place;
    public AnimationsControl Animation;
    [SerializeField] TMP_Text PlaceText;

    public CameraFollow cameraFollow;

    PlankCollector PlanksInfo;

    void Start()
    {
        _characterController = GetComponent<CharacterController>();
        Animation = GetComponent<AnimationsControl>();
        PlanksInfo = GetComponent<PlankCollector>();
        GameManager.Instance.RegistrRunner(transform);

        Animation.SetIdle();
        StartCoroutine(StartCountdownRoutine());
    }

      void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Finish"))
        {
            Finished();
        }
    }

    void Finished()
    {
        if(Place == 1)
        {
            //FirstPlaceLogic
        }
        else
        {
           StartCoroutine(GoToFinalPoint());
           cameraFollow.RaceEnded();
           _isGameStarted = false;
        }
    }
        IEnumerator GoToFinalPoint()
    {
        Vector3 Target = Finish.Instance.GetFreePoint();
        while(GetDistance(Target) > 4)
        {
        Move(Target);
        yield return null;
        }

        PlanksInfo.RemoveAllPlanks();

        Vector3 direction = Finish.Instance.transform.position - transform.position;
        direction.y = 0f;
        transform.rotation = Quaternion.LookRotation(direction);

        Animation.SetIdle();
    }

        float GetDistance(Vector3 Point)
    {
        Vector3 dir = Point - transform.position;
        dir.y = 0;
        
        return dir.sqrMagnitude;
    }

     void Move(Vector3 Target)
    {

    Vector3 pos = Vector3.MoveTowards(transform.position,Target,5 * Time.deltaTime);  
    Vector3 direction = Target - transform.position;

    direction.y = 0f;
    
    if (direction != Vector3.zero)
       transform.rotation = Quaternion.LookRotation(direction);

    transform.position = pos;
    PlanksInfo.RemoveAllPlanks();
    }

    void Update()
    {
        if (!_isGameStarted) return;

        HandleInput();
        MoveAndRotatePlayer();

        Place = GameManager.Instance.GetMyPlace(transform);
        PlaceText.text = Place.ToString();
    }

     public void IsFailing()
      {
          Animation.SetFailing();
      }

      public void CheckPlanks()
      {
        if(PlanksInfo._collectedPlanks.Count > 0)
        {
            Animation.SetRunningWithPlanks();
        }
        else
        {
            Animation.SetRunning();
        }
      }

    void HandleInput()
    {
        _currentTurnInput = 0f;

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                Vector2 delta = touch.delta.ReadValue();
                _currentTurnInput = delta.x * 0.2f;
            }
        }
        else if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                _currentTurnInput = -1f; 
            }
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                _currentTurnInput = 1f;  
            }
        }
    }

    void MoveAndRotatePlayer()
    {
        if (Mathf.Abs(_currentTurnInput) > 0.01f)
        {
            float rotationAmount = _currentTurnInput * turnSpeed * Time.deltaTime;
            transform.Rotate(0, rotationAmount, 0);
        }

        Vector3 moveDirection = transform.forward * forwardSpeed;

        if (!_characterController.isGrounded)
        {
            moveDirection.y = Physics.gravity.y;
        }

        _characterController.Move(moveDirection * Time.deltaTime);
    }

    private IEnumerator StartCountdownRoutine()
    {
        yield return new WaitForSeconds(3f);
        BotsManager.Instance.StartTheRun();
        Animation.SetRunning();
        _isGameStarted = true;
    }
}