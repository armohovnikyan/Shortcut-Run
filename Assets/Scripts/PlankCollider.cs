using System.Collections.Generic;
using UnityEngine;

// ВАЖНО: имя файла должно совпадать с именем класса для Unity.
// Либо переименуй файл в PlankCollector.cs, либо класс в PlankCollider.
public class PlankCollector : MonoBehaviour
{
    [Header("Настройки сбора досок")]
    public GameObject plankPrefab;
    public Transform stackPosition;
    public float plankHeight = 0.15f;

    [Header("Слои (замена isGrounded)")]
    [Tooltip("Сюда добавить ТОЛЬКО слой главной дороги. Слой досок класть НЕЛЬЗЯ")]
    public LayerMask roadLayer;
    public float groundCheckDistance = 1.2f;
    public float rayOriginHeight = 0.5f;
    [Tooltip("Сколько секунд подряд нет дороги под ногами, чтобы считать сход с дороги")]
    public float offRoadDebounce = 0.01f;
    [Tooltip("Сколько секунд подряд есть дорога, чтобы считать возврат на дорогу")]
    public float onRoadDebounce = 0.1f;

    [Header("Настройки моста")]
    [Tooltip("Расстояние между центрами досок в метрах")]
    public float plankSpacing = 1.5f;

    private CharacterController _characterController;
    private readonly List<GameObject> _collectedPlanks = new List<GameObject>();

    private enum GroundState { OnRoad, Bridging, Falling }
    private GroundState _state = GroundState.OnRoad;

    private float _lastRoadY;
    private float _fixedBridgeY;
    private float _offRoadTimer;
    private float _onRoadTimer;
    private Vector2? _lastPlankSpawnXZ;

    // ВРЕМЕННАЯ ОТЛАДКА — удалишь после того, как всё заработает
    private bool _debugLastHitRoad;
    private Vector3 _debugRayOrigin;
    private Vector3 _debugRayEnd;

    // Для отслеживания движения БЕЗ CharacterController.velocity
    // (velocity ломается, если Move() вызывается больше одного раза за кадр)
    private Vector2 _previousXZ;
    private float _cachedPlankThickness = -1f;

    void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _previousXZ = new Vector2(transform.position.x, transform.position.z);
    }

    // LateUpdate — чтобы коррекция высоты применялась ПОСЛЕ Player.cs (движения)
    void LateUpdate()
    {
        if (_characterController == null) return;

        CheckGroundState();

        // Смещение по X/Z считаем ДО коррекции высоты — она чисто вертикальная
        Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
        bool isMoving = Vector2.Distance(currentXZ, _previousXZ) > 0.005f;
        _previousXZ = currentXZ;

        if (_state == GroundState.Bridging)
        {
            MaintainBridgeHeight();

            if (isMoving)
            {
                TryBuildPlank();
            }
        }
    }

    private void CheckGroundState()
    {
        // Учитываем center коллайдера, а не только height/2
        Vector3 feetPos = transform.position + _characterController.center - Vector3.up * (_characterController.height / 2f);
        Vector3 origin = feetPos + Vector3.up * rayOriginHeight;

        bool hitRoad = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayOriginHeight + groundCheckDistance, roadLayer);

        // ВРЕМЕННАЯ ОТЛАДКА
        _debugLastHitRoad = hitRoad;
        _debugRayOrigin = origin;
        _debugRayEnd = origin + Vector3.down * (rayOriginHeight + groundCheckDistance);

        if (hitRoad)
        {
            _lastRoadY = hit.point.y;
            _offRoadTimer = 0f;
            _onRoadTimer += Time.deltaTime;

            if (_state != GroundState.OnRoad && _onRoadTimer >= onRoadDebounce)
            {
                _state = GroundState.OnRoad;
                Debug.Log($"[Bridge] -> OnRoad (Y={transform.position.y:F2})");
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
                    // Фиксируем высоту РОВНО в момент схода с дороги
                    _fixedBridgeY = _lastRoadY;
                    _state = GroundState.Bridging;
                    _lastPlankSpawnXZ = new Vector2(transform.position.x, transform.position.z);
                    Debug.Log($"[Bridge] -> Bridging, fixedY={_fixedBridgeY:F2}, planksInHand={_collectedPlanks.Count}");
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

    private void MaintainBridgeHeight()
    {
        // Не опираемся на физику досок — держим высоту сами.
        // Именно это убирает эффект "лестницы"
        float feetOffset = _characterController.center.y - _characterController.height / 2f;
        float targetY = _fixedBridgeY - feetOffset;

        float deltaY = targetY - transform.position.y;
        if (Mathf.Abs(deltaY) > 0.001f)
        {
            _characterController.Move(Vector3.up * deltaY);
        }
    }

    void TryBuildPlank()
    {
        Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);

        if (_lastPlankSpawnXZ != null && Vector2.Distance(currentXZ, _lastPlankSpawnXZ.Value) < plankSpacing)
        {
            return; // ещё не прошли нужное расстояние с прошлой доски
        }

        if (_collectedPlanks.Count == 0)
        {
            _state = GroundState.Falling; // доски кончились — падаем
            Debug.Log("[Bridge] Доски кончились -> Falling");
            return;
        }

        int lastIndex = _collectedPlanks.Count - 1;
        GameObject plankFromHand = _collectedPlanks[lastIndex];
        _collectedPlanks.RemoveAt(lastIndex);
        Destroy(plankFromHand);

        Vector3 spawnPos = new Vector3(transform.position.x, _fixedBridgeY, transform.position.z)
                           + (transform.forward * 0.5f);

        GameObject bridgePlank = Instantiate(plankPrefab, spawnPos, transform.rotation);

        // ВАЖНО: доска остаётся триггером — она ЧИСТО визуальная,
        // высоту держит скрипт, а не физическое столкновение
        BoxCollider plankCollider = bridgePlank.GetComponent<BoxCollider>();
        if (plankCollider != null)
        {
            plankCollider.isTrigger = true;
        }

        bridgePlank.transform.localScale = plankPrefab.transform.localScale;

        // ВАЖНО: снимаем тег "Plank", иначе игрок тут же подберёт
        // только что уложенную доску обратно через OnTriggerEnter
        bridgePlank.tag = "Untagged";

        _lastPlankSpawnXZ = currentXZ;
        Debug.Log($"[Bridge] Доска установлена в {spawnPos}, осталось в руках: {_collectedPlanks.Count}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Plank"))
        {
            Destroy(other.gameObject);
            AddPlankToStack();
        }
    }

    // Реальная толщина доски по Y. Считаем напрямую из BoxCollider.size * localScale —
    // Collider.bounds на НЕинстанциированном префабе-ассете ненадёжен и часто даёт 0
    private float GetPlankThickness()
    {
        if (_cachedPlankThickness > 0f) return _cachedPlankThickness;

        if (plankPrefab.TryGetComponent<BoxCollider>(out var box))
        {
            _cachedPlankThickness = box.size.y * plankPrefab.transform.localScale.y;
        }
        else if (plankPrefab.TryGetComponent<Renderer>(out var rend))
        {
            // Резервный вариант для непрямоугольных досок
            _cachedPlankThickness = rend.bounds.size.y > 0f ? rend.bounds.size.y : plankHeight;
        }
        else
        {
            _cachedPlankThickness = plankHeight;
        }

        // Небольшой предохранитель, если что-то всё равно посчиталось в 0
        if (_cachedPlankThickness <= 0f) _cachedPlankThickness = plankHeight;

        return _cachedPlankThickness;
    }

    void AddPlankToStack()
    {
        if (plankPrefab == null || stackPosition == null) return;

        GameObject newPlank = Instantiate(plankPrefab);
        newPlank.transform.SetParent(stackPosition);

        // Сохраняем НАСТОЯЩИЙ размер доски (как у префаба), просто компенсируя масштаб родителя,
        // а не выдумывая произвольные числа
        Vector3 parentScale = stackPosition.lossyScale;
        Vector3 originalScale = plankPrefab.transform.localScale;
        newPlank.transform.localScale = new Vector3(
            originalScale.x / (parentScale.x != 0 ? parentScale.x : 1f),
            originalScale.y / (parentScale.y != 0 ? parentScale.y : 1f),
            originalScale.z / (parentScale.z != 0 ? parentScale.z : 1f)
        );

        // Шаг стопки = реальная толщина доски, но компенсированная под масштаб
        // родителя (stackPosition), иначе localPosition умножится на его scale
        // при переводе в мировые координаты — и доски "разъедутся" по высоте
        float stepY = GetPlankThickness();
        float compensatedStep = stepY / (parentScale.y != 0 ? parentScale.y : 1f);
        float spawnYOffset = _collectedPlanks.Count * compensatedStep;
        newPlank.transform.localPosition = new Vector3(0, spawnYOffset, 0);
        newPlank.transform.localRotation = Quaternion.identity;

        _collectedPlanks.Add(newPlank);
    }
}