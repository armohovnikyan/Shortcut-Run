using Unity.Mathematics;
using UnityEngine;


public class PlayerMovment : MonoBehaviour
{

    [Header("Player Body")]
    [Tooltip("Y-distance from the pivot (transform.position) to the player's feet. " +
             "It used to be derived from CharacterController.center/height; now it is set manually. " +
             "Translation formula: footOffset = height/2 - center.y (for a typical CharacterController).")]
    public float footOffset = 1f;

    [Tooltip("Not necessarily. If the player has a non-kinematic Rigidbody..., " +
             "Height correction will be handled via Rigidbody.MovePosition (it plays better with physics). " +
             "If there is no Rigidbody or it is kinematic, move the transform directly.. " +
             "If the field is not assigned in the Inspector, the script will attempt to find the Rigidbody itself.")]
    public Rigidbody bodyRigidbody;

    [Header("Layers (replacement for isGrounded)")]
    [Tooltip("Add ONLY the main road layer here. Do NOT lay the plank layer.")]
    public LayerMask roadLayer;
    public float groundCheckDistance = 1.2f;
    public float rayOriginHeight = 0.5f;
    [Tooltip("How many consecutive seconds without the ground beneath one's feet constitute leaving the path?")]
    public float offRoadDebounce = 0.05f;
    [Tooltip("How many consecutive seconds must the vehicle be on the road to count as a return to the road?")]
    public float onRoadDebounce = 0.05f;
    public enum GroundState { OnRoad, Bridging, Jump, Falling, OnTrampoline, ClimbUp }
    public GroundState _state = GroundState.OnRoad;

    [Header("Catch the road in mid-air")]
    [Tooltip("How far ahead should we look for the path when there is nothing beneath our feet?")]
    public float grabRoadDistance = 8;
    [Tooltip("How many seconds does a pull-up take?")]
    public float climbDuration = 0.25f;
    private Vector3 _climbStartPos;
    private Vector3 _climbTargetPos;
    private float _climbStartTime;

    [Header("Jump before falling (no animation yet)")]
    [Tooltip("How high the character jumps (in meters)")]
    public float jumpHeight = 1;
    public float jumpTroHeight = 2;
    [Tooltip("How many seconds does the entire jump (up and back down) last?")]
    public float jumpDuration = 1f;
    public float jumpTroDuration = 2f;
    private float _fixedBridgeY;
    private float _offRoadTimer;

    public IRunner MainScript;

    private bool _debugLastHitRoad;
    private Vector3 _debugRayOrigin;
    private Vector3 _debugRayEnd;
    Plank PlankCollector;
    [SerializeField] Transform FeetPos;
    [SerializeField] GameObject ParticleEffect;
    private float _jumpStartTime;

    void Start()
    {
        MainScript = GetComponent<IRunner>();
        PlankCollector = GetComponent<Plank>();

        if (MainScript == null)
            Debug.LogError($"[PlayerMovment] На объекте {gameObject.name} нет компонента, реализующего ICharacter (PlayerController или BotMovement)!", this);

        if (PlankCollector == null)
            Debug.LogError($"[PlayerMovment] На объекте {gameObject.name} нет компонента Plank!", this);
    }

    void LateUpdate()
    {
        if (_state != GroundState.Falling && _state != GroundState.ClimbUp)
        {
            MoveToY(_fixedBridgeY + footOffset);
        }
        if (_state == GroundState.Bridging)
        {
            TryBuildPlank();
        }
        if (_state == GroundState.Jump)
        {
            HandleJump();
        }
        if (_state == GroundState.ClimbUp)
        {
            HandleClimb();
            return;
        }

        CheckGroundState();
    }
    bool isTrampoline;

    bool IsOnRoad()
    {
        Vector3 feetPos = FeetPos.position;
        Vector3 origin = feetPos + Vector3.up * rayOriginHeight;

        RaycastHit hit;

        bool hitRoad = Physics.Raycast(
            origin,
            Vector3.down,
            out hit,
            rayOriginHeight + groundCheckDistance,
            roadLayer
            );

        return hitRoad;
    }
    private void CheckGroundState()
    {
        Vector3 feetPos = FeetPos.position;
        Vector3 origin = feetPos + Vector3.up * rayOriginHeight;

        RaycastHit hit;

        bool hitRoad = Physics.Raycast(
            origin,
            Vector3.down,
            out hit,
            rayOriginHeight + groundCheckDistance,
            roadLayer
            );

        if (hit.collider != null && hit.collider.CompareTag("Tramp"))
        {
            isTrampoline = true;
        }

        if (isTrampoline && _state != GroundState.Jump && _state != GroundState.OnTrampoline && _state != GroundState.Falling && _state != GroundState.ClimbUp)
        {
            StartJump(true);
            isTrampoline = false;
            return;
        }

        _debugLastHitRoad = hitRoad;
        _debugRayOrigin = origin;
        _debugRayEnd = origin + Vector3.down * (rayOriginHeight + groundCheckDistance);


        if (hitRoad)
        {
            _offRoadTimer = 0f;

            if (_state != GroundState.OnRoad)
            {
                _state = GroundState.OnRoad;
                MainScript.CheckPlanks();
                Debug.Log($"[Bridge] -> OnRoad (Y={transform.position.y:F2})");
            }

            if (hit.collider.CompareTag("PlacedPlank"))
            {
                MainScript.ChangeSpeedBonus(0.03f);
            }
            else
            {
                MainScript.ChangeSpeedBonus(-0.03f);
            }
        }
        else
        {
            if (_state == GroundState.OnRoad)
            {
                _offRoadTimer += Time.deltaTime;

                if (_offRoadTimer >= offRoadDebounce)
                {
                    _state = GroundState.Bridging;
                    _lastPlankSpawnXZ = new Vector2(transform.position.x, transform.position.z);
                    Debug.Log($"[Bridge] -> Bridging, fixedY={_fixedBridgeY:F2}, planksInHand={PlankCollector.CollectedPlanks.Count}");
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = _debugLastHitRoad ? Color.green : Color.red;
        Gizmos.DrawLine(_debugRayOrigin, _debugRayEnd);
        Gizmos.DrawWireSphere(_debugRayEnd, 0.1f);
    }



    private void MoveToY(float targetY)
    {
        float deltaY = targetY - transform.position.y;
        if (Mathf.Abs(deltaY) <= 0.0001f) return;

        Vector3 newPos = transform.position + Vector3.up * deltaY;

        if (bodyRigidbody != null && !bodyRigidbody.isKinematic)
        {
            bodyRigidbody.MovePosition(newPos);
        }
        else
        {
            transform.position = newPos;
        }
    }

    Vector2 _lastPlankSpawnXZ;
    void TryBuildPlank()
    {
        if (PlankCollector.CollectedPlanks.Count == 0)
        {
            StartJump(false);
            return;
        }

        int lastIndex = PlankCollector.CollectedPlanks.Count - 1;
        GameObject plankFromHand = PlankCollector.CollectedPlanks[lastIndex];
        PlankCollector.CollectedPlanks.RemoveAt(lastIndex);

        plankFromHand.transform.SetParent(null);
        plankFromHand.transform.position = new Vector3(_lastPlankSpawnXZ.x, _fixedBridgeY, _lastPlankSpawnXZ.y) + (transform.forward * 0.5f);
        plankFromHand.tag = "PlacedPlank";
        BoxCollider plankCol = plankFromHand.GetComponent<BoxCollider>();
        plankCol.size = new Vector3(1.5f, 1, 2);
        plankCol.enabled = true;
        plankFromHand.layer = LayerMask.NameToLayer("Road");

        Instantiate(ParticleEffect, plankFromHand.transform.position, plankFromHand.transform.rotation * Quaternion.Euler(0, 90, 0));

        _lastPlankSpawnXZ = new Vector2(plankFromHand.transform.position.x, plankFromHand.transform.position.z);
        MainScript.CheckPlanks();
        Debug.Log($"[Bridge] Доска установлена, осталось в руках: {PlankCollector.CollectedPlanks.Count}");
    }

    bool _currentJumpIsTrampoline;

    private void StartJump(bool trampoline)
    {
        _currentJumpIsTrampoline = trampoline;
        MainScript.Jump();
        _state = GroundState.Jump;
        _jumpStartTime = Time.time;
    }


    private void HandleJump()
    {
        float duration = _currentJumpIsTrampoline ? jumpTroDuration : jumpDuration;
        float elapsed = Time.time - _jumpStartTime;

        if (elapsed >= duration)
        {
            // 1. Приземлились прямо на дорогу — просто продолжаем бежать
            if (IsOnRoad())
            {
                _state = GroundState.OnRoad;
                MainScript.CheckPlanks();
                return;
            }

            // 2. Дороги под ногами нет, но она есть впереди — подтягиваемся
            if (TryFindNearbyRoad(out Vector3 grabPoint))
            {
                StartClimb(grabPoint);
                Debug.Log("climbing!!!!!!!!!!!!!!!!");
                return;
            }

            // 3. Батут, дороги нет, но остались доски — строим мост
            if (_currentJumpIsTrampoline && PlankCollector.CollectedPlanks.Count != 0)
            {
                _state = GroundState.Bridging;
                _lastPlankSpawnXZ = new Vector2(transform.position.x, transform.position.z);
                Debug.Log($"[Bridge] -> Bridging, fixedY={_fixedBridgeY:F2}, planksInHand={PlankCollector.CollectedPlanks.Count}");
                return;
            }

            // 4. Ни один вариант не сработал — только теперь падаем
            _state = GroundState.Falling;
            MainScript.IsFailing();
            return;
        }

        float t = elapsed / duration;
        float arc = 4f * (_currentJumpIsTrampoline ? jumpTroHeight : jumpHeight) * t * (1f - t);
        MoveToY(_fixedBridgeY + footOffset + arc);
    }

    private bool TryFindNearbyRoad(out Vector3 grabPoint)
    {
        grabPoint = default;

        Vector3 origin = new Vector3(transform.position.x, 0, transform.position.z);
        if (Physics.Raycast(
        origin,
        transform.forward,
        out RaycastHit hitInfo,
        grabRoadDistance,
        roadLayer,
        QueryTriggerInteraction.Collide))
        {
            grabPoint = hitInfo.point;
            return true;
        }

        return false;
    }


    private void StartClimb(Vector3 grabPoint)
    {
        _state = GroundState.ClimbUp;
        _climbStartPos = transform.position;
        MainScript.Climb(true);
        _climbTargetPos = new Vector3(grabPoint.x, grabPoint.y + footOffset, grabPoint.z);
        _climbStartTime = Time.time;
    }

    private void HandleClimb()
    {
        float elapsed = Time.time - _climbStartTime;
        float t = Mathf.Clamp01(elapsed / climbDuration);

        transform.position = Vector3.Lerp(_climbStartPos, _climbTargetPos, t);

        if (t >= 1f)
        {
            _fixedBridgeY = _climbTargetPos.y - footOffset;
            _state = GroundState.OnRoad;
            MainScript.Climb(false);
            MainScript.CheckPlanks();
        }
    }
}