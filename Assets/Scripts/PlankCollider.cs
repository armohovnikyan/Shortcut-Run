//using System.Collections.Generic;
//using UnityEngine;
//
//// ВАЖНО: имя файла должно совпадать с именем класса для Unity.
//// Файл уже называется PlankCollector.cs — ничего переименовывать не нужно.
//public class PlankCollector : MonoBehaviour
//{
//    [Header("Настройки сбора досок")]
//    public GameObject plankPrefab;
//    public Transform stackPosition;
//    public float plankHeight = 0.15f;
//
//    [Header("Тело игрока (замена CharacterController)")]
//    [Tooltip("Расстояние по Y от pivot (transform.position) до ступней игрока. " +
//             "Раньше бралось из CharacterController.center/height — теперь задаётся вручную. " +
//             "Формула перевода: footOffset = height/2 - center.y (для типового CharacterController).")]
//    public float footOffset = 1f;
//
//    [Tooltip("Необязательно. Если на игроке есть НЕ-кинематический Rigidbody, " +
//             "коррекция высоты пойдёт через Rigidbody.MovePosition (лучше дружит с физикой). " +
//             "Если Rigidbody нет или он кинематический — двигаем transform напрямую. " +
//             "Если поле не назначено в инспекторе, скрипт попробует найти Rigidbody сам.")]
//    public Rigidbody bodyRigidbody;
//
//    [Header("Слои (замена isGrounded)")]
//    [Tooltip("Сюда добавить ТОЛЬКО слой главной дороги. Слой досок класть НЕЛЬЗЯ")]
//    public LayerMask roadLayer;
//    public float groundCheckDistance = 1.2f;
//    public float rayOriginHeight = 0.5f;
//    [Tooltip("Сколько секунд подряд нет дороги под ногами, чтобы считать сход с дороги")]
//    public float offRoadDebounce = 0.05f;
//    [Tooltip("Сколько секунд подряд есть дорога, чтобы считать возврат на дорогу")]
//    public float onRoadDebounce = 0.05f;
//
//    [Header("Настройки моста")]
//    [Tooltip("Расстояние между центрами досок в метрах")]
//    public float plankSpacing = 1.5f;
//
//    [Header("Теги")]
//    public string plankTag = "Plank";
//
//    public List<GameObject> _collectedPlanks = new List<GameObject>();
//
//    private enum GroundState { OnRoad, Bridging, Falling }
//    private GroundState _state = GroundState.OnRoad;
//
//    private float _fixedBridgeY;
//    private float _offRoadTimer;
//    private float _onRoadTimer;
//    private Vector2? _lastPlankSpawnXZ;
//
//    ICharacter MainScript;
//
//    // ВРЕМЕННАЯ ОТЛАДКА — удалишь после того, как всё заработает
//    private bool _debugLastHitRoad;
//    private Vector3 _debugRayOrigin;
//    private Vector3 _debugRayEnd;
//
//    // Для отслеживания движения без завязки на конкретный контроллер движения
//    [SerializeField] Transform FeetPos;
//
//    void Start()
//    {
//        MainScript = GetComponent<ICharacter>();
//    }
//
//    // LateUpdate — чтобы коррекция высоты применялась ПОСЛЕ любого скрипта движения игрока
//    void LateUpdate()
//    {
//        CheckGroundState();
//        if (_state != GroundState.Falling)
//        {
//            MaintainBridgeHeight();
//        }
//        if(_state == GroundState.Bridging)
//        {
//            TryBuildPlank();
//        }
//    }
//    private void CheckGroundState()
//    {
//        Vector3 feetPos = FeetPos.position;
//        Vector3 origin = feetPos + Vector3.up * rayOriginHeight;
//
//        bool hitRoad = Physics.Raycast(
//            origin,
//            Vector3.down,
//            out RaycastHit hit,
//            rayOriginHeight + groundCheckDistance,
//            roadLayer
//            );
//
//        // ВРЕМЕННАЯ ОТЛАДКА
//        _debugLastHitRoad = hitRoad;
//        _debugRayOrigin = origin;
//        _debugRayEnd = origin + Vector3.down * (rayOriginHeight + groundCheckDistance);
//
//        if (hitRoad)
//        {
//            _offRoadTimer = 0f;
//            _onRoadTimer += Time.deltaTime;
//
//            if (_state != GroundState.OnRoad && _onRoadTimer >= onRoadDebounce)
//            {
//                _state = GroundState.OnRoad;
//                Debug.Log($"[Bridge] -> OnRoad (Y={transform.position.y:F2})");
//            }
//        }
//        else
//        {
//            _onRoadTimer = 0f;
//
//            if (_state == GroundState.OnRoad)
//            {
//                _offRoadTimer += Time.deltaTime;
//
//                if (_offRoadTimer >= offRoadDebounce)
//                {
//                    _state = GroundState.Bridging;
//                    _lastPlankSpawnXZ = new Vector2(transform.position.x, transform.position.z);
//                    Debug.Log($"[Bridge] -> Bridging, fixedY={_fixedBridgeY:F2}, planksInHand={_collectedPlanks.Count}");
//                }
//            }
//        }
//    }
//
//    // ВРЕМЕННАЯ ОТЛАДКА — рисует луч проверки земли в Scene view (зелёный = попал, красный = мимо)
//    private void OnDrawGizmos()
//    {
//        if (!Application.isPlaying) return;
//        Gizmos.color = _debugLastHitRoad ? Color.green : Color.red;
//        Gizmos.DrawLine(_debugRayOrigin, _debugRayEnd);
//        Gizmos.DrawWireSphere(_debugRayEnd, 0.1f);
//    }
//
//   private void MaintainBridgeHeight()
//   {
//      float targetY = _fixedBridgeY + footOffset;
//      float deltaY = targetY - transform.position.y;
//
//      if (Mathf.Abs(deltaY) <= 0.0001f) return;
//
//      Vector3 newPos = transform.position + Vector3.up * deltaY;
//      transform.position = newPos;     
//   }
//
//    void TryBuildPlank()
//    {
//        //Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
//
//       // if (_lastPlankSpawnXZ != null && Vector2.Distance(currentXZ, _lastPlankSpawnXZ.Value) < plankSpacing)
//       // {
//       //     return; // ещё не прошли нужное расстояние с прошлой доски
//       // }
//
//        if (_collectedPlanks.Count == 0)
//        {
//            _state = GroundState.Falling; 
//           // MainScript.IsFailing();
//           // Debug.Log("[Bridge] Доски кончились -> Falling");
//            return;
//        }
//
//        int lastIndex = _collectedPlanks.Count - 1;
//        GameObject plankFromHand = _collectedPlanks[lastIndex];
//        _collectedPlanks.RemoveAt(lastIndex);
//        
//        plankFromHand.transform.SetParent(null);
//        plankFromHand.transform.position = new Vector3(transform.position.x, _fixedBridgeY, transform.position.z) + (transform.forward * 0.5f);
//        plankFromHand.tag = "Untagged";
//        BoxCollider plankCol = plankFromHand.GetComponent<BoxCollider>();
//        plankCol.size = new Vector3(1.5f,1,2);
//        plankCol.enabled = true;
//        plankFromHand.layer = LayerMask.NameToLayer("Road");;
//
//     //   _lastPlankSpawnXZ = currentXZ;
//        MainScript.CheckPlanks();
//        Debug.Log($"[Bridge] Доска установлена, осталось в руках: {_collectedPlanks.Count}");
//    }
//
//    private void OnTriggerEnter(Collider other)
//    {
//        if (other.CompareTag(plankTag))
//        {
//            Destroy(other.gameObject);
//            AddPlankToStack();
//        }
//    }
//    void AddPlankToStack()
//    {
//        GameObject newPlank = Instantiate(plankPrefab);
//        newPlank.GetComponent<Collider>().enabled = false;
//        newPlank.transform.SetParent(stackPosition);
//
//        float spawnYOffset = _collectedPlanks.Count * (0.2f + 0.03f);
//        newPlank.transform.localPosition = new Vector3(0, spawnYOffset, 0);
//        newPlank.transform.localRotation = Quaternion.identity;
//
//        _collectedPlanks.Add(newPlank);
//        MainScript.CheckPlanks();
//    }
//
//    public void RemoveAllPlanks()
//    {
//        foreach(GameObject plank in _collectedPlanks)
//        {
//            Destroy(plank);
//        }
//
//        _collectedPlanks.Clear();
//    }
//}
//