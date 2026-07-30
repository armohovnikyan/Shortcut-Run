using System.Collections.Generic;
using UnityEngine;

// ВАЖНО: имя файла должно совпадать с именем класса для Unity.
// Файл уже называется PlankCollector.cs — ничего переименовывать не нужно.
public class BridgeBuilder : MonoBehaviour
{

    [Header("Тело игрока (замена CharacterController)")]
    [Tooltip("Расстояние по Y от pivot (transform.position) до ступней игрока. " +
             "Раньше бралось из CharacterController.center/height — теперь задаётся вручную. " +
             "Формула перевода: footOffset = height/2 - center.y (для типового CharacterController).")]
    public float footOffset = 1f;

    [Tooltip("Необязательно. Если на игроке есть НЕ-кинематический Rigidbody, " +
             "коррекция высоты пойдёт через Rigidbody.MovePosition (лучше дружит с физикой). " +
             "Если Rigidbody нет или он кинематический — двигаем transform напрямую. " +
             "Если поле не назначено в инспекторе, скрипт попробует найти Rigidbody сам.")]
    public Rigidbody bodyRigidbody;

    [Header("Слои (замена isGrounded)")]
    [Tooltip("Сюда добавить ТОЛЬКО слой главной дороги. Слой досок класть НЕЛЬЗЯ")]
    public LayerMask roadLayer;
    public float groundCheckDistance = 1.2f;
    public float rayOriginHeight = 0.5f;
    [Tooltip("Сколько секунд подряд нет дороги под ногами, чтобы считать сход с дороги")]
    public float offRoadDebounce = 0.05f;
    [Tooltip("Сколько секунд подряд есть дорога, чтобы считать возврат на дорогу")]
    public float onRoadDebounce = 0.05f;
    private enum GroundState { OnRoad, Bridging, Jump, Falling }
    private GroundState _state = GroundState.OnRoad;

    [Header("Прыжок перед падением (пока нет анимации)")]
    [Tooltip("Насколько высоко подпрыгивает персонаж, метры")]
    public float jumpHeight = 0.4f;
    [Tooltip("Сколько секунд длится прыжок целиком (вверх и обратно вниз)")]
    public float jumpDuration = 0.35f;

    private float _fixedBridgeY;
    private float _offRoadTimer;

    ICharacter MainScript;

    // ВРЕМЕННАЯ ОТЛАДКА — удалишь после того, как всё заработает
    private bool _debugLastHitRoad;
    private Vector3 _debugRayOrigin;
    private Vector3 _debugRayEnd;
PlankCollector PlankCollector;
    // Для отслеживания движения без завязки на конкретный контроллер движения
    [SerializeField] Transform FeetPos;

    private float _jumpStartTime;

    void Start()
    {
        MainScript = GetComponent<ICharacter>();
        PlankCollector = GetComponent<PlankCollector>();
    }

    // LateUpdate — чтобы коррекция высоты применялась ПОСЛЕ любого скрипта движения игрока
    void LateUpdate()
    {
        CheckGroundState();

        if (_state != GroundState.Falling)
        {
            MoveToY(_fixedBridgeY + footOffset);
        }
        if(_state == GroundState.Bridging)
        {
            TryBuildPlank();
        }
        if(_state == GroundState.Jump)
        {
            HandleJump();
        }
    }
    private void CheckGroundState()
    {
        Vector3 feetPos = FeetPos.position;
        Vector3 origin = feetPos + Vector3.up * rayOriginHeight;

        bool hitRoad = Physics.Raycast(
            origin,
            Vector3.down,
            rayOriginHeight + groundCheckDistance,
            roadLayer
            );

        // ВРЕМЕННАЯ ОТЛАДКА
        _debugLastHitRoad = hitRoad;
        _debugRayOrigin = origin;
        _debugRayEnd = origin + Vector3.down * (rayOriginHeight + groundCheckDistance);

        if (hitRoad)
        {
            _offRoadTimer = 0f;

            if (_state != GroundState.OnRoad )
            {
                _state = GroundState.OnRoad;
                Debug.Log($"[Bridge] -> OnRoad (Y={transform.position.y:F2})");
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
                    Debug.Log($"[Bridge] -> Bridging, fixedY={_fixedBridgeY:F2}, planksInHand={PlankCollector.CollectedPlanks.Count}");
                }
            }
        }
    }

    // ВРЕМЕННАЯ ОТЛАДКА — рисует луч проверки земли в Scene view (зелёный = попал, красный = мимо)
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = _debugLastHitRoad ? Color.green : Color.red;
        Gizmos.DrawLine(_debugRayOrigin, _debugRayEnd);
        Gizmos.DrawWireSphere(_debugRayEnd, 0.1f);
    }

  //private void MaintainBridgeHeight()
  //{
  //   float targetY = _fixedBridgeY + footOffset;
  //   float deltaY = targetY - transform.position.y;

  //   if (Mathf.Abs(deltaY) <= 0.0001f) return;

  //   Vector3 newPos = transform.position + Vector3.up * deltaY;
  //   transform.position = newPos;     
  //}

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

    void TryBuildPlank()
    {
        if (PlankCollector.CollectedPlanks.Count == 0)
        {
            StartJump();
            return;
        }

        int lastIndex = PlankCollector.CollectedPlanks.Count - 1;
        GameObject plankFromHand = PlankCollector.CollectedPlanks[lastIndex];
        PlankCollector.CollectedPlanks.RemoveAt(lastIndex);
        
        plankFromHand.transform.SetParent(null);
        plankFromHand.transform.position = new Vector3(transform.position.x, _fixedBridgeY, transform.position.z) + (transform.forward * 0.5f);
        plankFromHand.tag = "Untagged";
        BoxCollider plankCol = plankFromHand.GetComponent<BoxCollider>();
        plankCol.size = new Vector3(1.5f,1,2);
        plankCol.enabled = true;
        plankFromHand.layer = LayerMask.NameToLayer("Road");;

     //   _lastPlankSpawnXZ = currentXZ;
        MainScript.CheckPlanks();
        Debug.Log($"[Bridge] Доска установлена, осталось в руках: {PlankCollector.CollectedPlanks.Count}");
    }

     private void StartJump()
    {
        _state = GroundState.Jump;
        _jumpStartTime = Time.time;
    }

    // Простая параболическая дуга: 0 в начале, jumpHeight в середине, снова 0 в конце.
    // Потом отпускаем персонажа — переходим в Falling и включаем анимацию/логику провала.
    private void HandleJump()
    {
        float elapsed = Time.time - _jumpStartTime;

        if (elapsed >= jumpDuration)
        {
            _state = GroundState.Falling;
            MainScript.IsFailing();
            return;
        }

        float t = elapsed / jumpDuration;
        float arc = 4f * jumpHeight * t * (1f - t); // парабола: пик ровно посередине

        MoveToY(_fixedBridgeY + footOffset + arc);
    }
}


