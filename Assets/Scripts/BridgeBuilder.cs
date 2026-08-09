using UnityEngine;


[RequireComponent(typeof(PlankStacker))]
public class BridgeBuilder : MonoBehaviour
{
    [Header("Тело игрока (замена CharacterController)")]
    [Tooltip("Расстояние по Y от pivot (transform.position) до ступней игрока. " +
             "Формула перевода: footOffset = height/2 - center.y (для типового CharacterController).")]
    public float footOffset = 1f;

    [Tooltip("Необязательно. Если на игроке есть НЕ-кинематический Rigidbody, " +
             "коррекция высоты идёт через Rigidbody.MovePosition. Иначе двигаем transform напрямую.")]
    public Rigidbody bodyRigidbody;

    [SerializeField] private Transform FeetPos;

    [Header("Слои (замена isGrounded)")]
    [Tooltip("Сюда добавить ТОЛЬКО слой главной дороги. Слой досок класть НЕЛЬЗЯ")]
    public LayerMask roadLayer;
    public LayerMask trampolineLayer;
    public float groundCheckDistance = 1.2f;
    public float rayOriginHeight = 0.5f;
    [Tooltip("Сколько секунд подряд нет дороги под ногами, чтобы считать сход с дороги")]
    public float offRoadDebounce = 0.05f;
    [Tooltip("Сколько секунд подряд есть дорога, чтобы считать возврат на дорогу")]
    public float onRoadDebounce = 0.05f;

    [Header("Настройки моста")]
    [Tooltip("Расстояние между центрами досок в метрах")]
    public float plankSpacing = 1.5f;
    [Tooltip("Слой, который получают уложенные доски моста. ВАЖНО: должен ОТЛИЧАТЬСЯ " +
             "от roadLayer, иначе луч проверки земли спутает доску с настоящей дорогой " +
             "и удержание высоты будет постоянно сбрасываться")]
    public string plankLayerName = "Bridge";

    [Header("Прыжок перед падением (пока нет анимации)")]
    [Tooltip("Насколько высоко подпрыгивает персонаж, метры")]
    public float jumpHeight = 0.4f;
    public float jumpTroHeight = 0.8f;
    [Tooltip("Сколько секунд длится прыжок целиком (вверх и обратно вниз)")]
    public float jumpDuration = 0.35f;
    public float jumpTroDuration = 0.7f;
    private bool isTrampoline;
    private bool _currentJumpIsTrampoline;

    private PlankStacker _stacker;
    private ICharacter MainScript;

    private enum GroundState { OnRoad, Bridging, Jump, Falling, OnTrampoline }
    private GroundState _state = GroundState.OnRoad;

    private float _fixedBridgeY;
    private float _lastRoadY;
    private float _offRoadTimer;
    private float _onRoadTimer;
    private float _jumpStartTime;
    private Vector2? _lastPlankSpawnXZ;

    void Start()
    {
        _stacker = GetComponent<PlankStacker>();
        MainScript = GetComponent<ICharacter>();
    }

    void LateUpdate()
    {
        CheckGroundState();

        switch (_state)
        {
            case GroundState.Bridging:
                MoveToY(_fixedBridgeY + footOffset);
                TryBuildPlank();
                break;

            case GroundState.Jump:
                HandleJump();
                break;

            case GroundState.OnTrampoline:
                HandleJump();
                break;

            case GroundState.Falling:
                break;
        }
    }

    private void CheckGroundState()
    {
        Vector3 origin = FeetPos.position + Vector3.up * rayOriginHeight;

        isTrampoline = Physics.Raycast(
           origin,
           Vector3.down,
           rayOriginHeight + groundCheckDistance,
           trampolineLayer
           );


        if (isTrampoline && _state != GroundState.Jump && _state != GroundState.OnTrampoline && _state != GroundState.Falling)
        {
            StartJump(true);
            return;
        }

        bool hitRoad = Physics.Raycast(
            origin,
            Vector3.down,
            out RaycastHit hit,
            rayOriginHeight + groundCheckDistance,
            roadLayer
        );

        if (hitRoad)
        {
            _lastRoadY = hit.point.y;
            _offRoadTimer = 0f;
            _onRoadTimer += Time.deltaTime;

            if (_state != GroundState.OnRoad && _onRoadTimer >= onRoadDebounce)
            {
                _state = GroundState.OnRoad;
            }
        }
        else
        {
            _onRoadTimer = 0f;

            if (_state == GroundState.OnRoad)
            {
                _offRoadTimer += Time.deltaTime;

                if (_offRoadTimer >= offRoadDebounce)
                {
                    _fixedBridgeY = _lastRoadY;
                    _lastPlankSpawnXZ = new Vector2(transform.position.x, transform.position.z);

                    if (_stacker.Count == 0)
                    {
                        StartJump(false);
                    }
                    else
                    {
                        _state = GroundState.Bridging;
                    }
                }
            }
        }
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

    private bool IsOnRoadNow()
    {
        Vector3 origin = FeetPos.position + Vector3.up * rayOriginHeight;
        return Physics.Raycast(origin, Vector3.down, rayOriginHeight + groundCheckDistance, roadLayer);
    }

    private void StartJump(bool trampoline)
    {
        _currentJumpIsTrampoline = trampoline;
        _state = trampoline ? GroundState.OnTrampoline : GroundState.Jump;
        _jumpStartTime = Time.time;
    }


    private void HandleJump()
    {
        float elapsed = Time.time - _jumpStartTime;
        float arc;

        if (_currentJumpIsTrampoline)
        {
            if (elapsed >= jumpTroDuration)
            {
                _state = GroundState.OnRoad;
                _onRoadTimer = 0f;
                _offRoadTimer = 0f;
                return;
            }

            float t = elapsed / jumpTroDuration;
            arc = 4f * jumpTroHeight * t * (1f - t);
        }
        else
        {
            if (elapsed >= jumpDuration)
            {
                if (IsOnRoadNow())
                {
                    _state = GroundState.OnRoad;
                    _onRoadTimer = 0f;
                    _offRoadTimer = 0f;
                }
                else
                {
                    _state = GroundState.Falling;
                    MainScript.IsFailing();
                }
                return;
            }

            float t = elapsed / jumpDuration;
            arc = 4f * jumpHeight * t * (1f - t);
        }

        MoveToY(_fixedBridgeY + footOffset + arc);
    }

    void TryBuildPlank()
    {
        Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);

        if (_lastPlankSpawnXZ != null && Vector2.Distance(currentXZ, _lastPlankSpawnXZ.Value) < plankSpacing)
        {
            return;
        }

        GameObject plank = _stacker.TakePlank();

        if (plank == null)
        {
            StartJump(false); 
            return;
        }

        plank.transform.SetParent(null);
        plank.transform.position = new Vector3(transform.position.x, _fixedBridgeY, transform.position.z)
                                    + (transform.forward * 0.5f);
        plank.tag = "Untagged";

        BoxCollider plankCol = plank.GetComponent<BoxCollider>();
        plankCol.size = new Vector3(1.5f, 1, 2);
        plankCol.enabled = true;
        plank.layer = LayerMask.NameToLayer(plankLayerName);

        _lastPlankSpawnXZ = currentXZ;
    }
}