using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using UnityEngine.EventSystems;
using System;
using System.Linq;
using Unity.VisualScripting;
using System.Diagnostics; // Required for Stopwatch

public class PlayerMovement : MonoBehaviour, IMovementHandler
{
    // --- Performance Monitoring ---
    public float LastExecutionTimeMs { get; private set; }
    private Stopwatch _perfWatch = new Stopwatch();

    public enum MovementMode { PointAndClick, WASD }

    [Header("Movement")]
    public MovementMode currentMode => (PartyManager.instance != null) ? PartyManager.instance.currentMovementMode : MovementMode.PointAndClick;
    public float wasdMoveSpeed;

    [Header("PoE2 Rotation Style")]
    public float bodyTurnThreshold = 15f;
    public float bodyTurnSpeed = 5f;

    [Space(10)]
    [Range(0, 1)] public float headLookWeight = 1f;
    public float headLookSpeed = 8f;
    public float headWeightBlendSpeed = 5f;
    public float headLookHeight = 1.6f;

    public Vector3 CurrentHeadLookPosition { get; private set; }
    public Vector3 CurrentLookTarget { get; private set; }
    public float CurrentHeadLookWeight { get; private set; }

    [Header("Dodge Roll")]
    public float dodgeDistance = 5f;
    public float dodgeDuration = 0.4f;
    public float dodgeCooldown = 0.5f;
    public string dodgeAnimationTrigger = "DodgeRoll";

    private bool isDodging = false;
    private float nextDodgeTime = 0f;

    [Header("Animation")]
    public float animationDampTime = 0.1f;

    [Header("Targeting")]
    public GameObject TargetObject;

    [Header("Abilities")]
    public Ability defaultAttackAbility;
    private Ability queuedAbility;

    [Header("Configuration")]
    public LayerMask interactionLayers;
    public float stoppingDistance = 1.0f;
    public float interactionRange = 3f;

    [Header("Game State")]
    public GameObject uiManagerObject;
    public string dragInProgressVariableName = "UIDragInProgress";

    private CharacterMovementHandler movementHandler;
    public bool IsSpecialMovementActive => movementHandler != null && movementHandler.IsSpecialMovementActive;

    private NavMeshAgent navMeshAgent;
    private Camera mainCamera;
    private PlayerAbilityHolder abilityHolder;
    private PartyAIManager partyAIManager;
    private Animator animator;
    private PlayerEquipment playerEquipment;
    private Vector3 wasdVelocity;

    public bool IsMovingToAttack { get; private set; } = false;
    public bool IsGroundTargeting { get; set; } = false;

    private Coroutine interactionCoroutine;
    private int clickLockoutFrames = 0;
    private bool isFacingLocked = false;
    private Collider mainCollider;
    private Health myHealth;

    private int velocityZHash;
    private int velocityXHash;
    private int weaponTypeHash;
    private int dodgeHash;

    private int frameSkipCounter = 0;

    void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        mainCamera = Camera.main;
        partyAIManager = PartyAIManager.instance;
        movementHandler = GetComponent<CharacterMovementHandler>();
        mainCollider = GetComponent<Collider>();

        CharacterRoot root = GetComponent<CharacterRoot>();
        if (root != null)
        {
            myHealth = root.Health;
            abilityHolder = root.PlayerAbilityHolder;
            animator = root.Animator;
            playerEquipment = root.PlayerEquipment;
        }
        else
        {
            myHealth = GetComponent<Health>();
            abilityHolder = GetComponent<PlayerAbilityHolder>();
            animator = GetComponentInChildren<Animator>();
            playerEquipment = GetComponent<PlayerEquipment>();
        }

        velocityZHash = Animator.StringToHash("VelocityZ");
        velocityXHash = Animator.StringToHash("VelocityX");
        weaponTypeHash = Animator.StringToHash("WeaponType");
        dodgeHash = Animator.StringToHash(!string.IsNullOrEmpty(dodgeAnimationTrigger) ? dodgeAnimationTrigger : "DodgeRoll");

        if (navMeshAgent != null)
        {
            navMeshAgent.updateRotation = false;
        }
    }

    void Start()
    {
        if (navMeshAgent != null) wasdMoveSpeed = navMeshAgent.speed;
        UpdateWeaponTypeParameter();
        CurrentHeadLookPosition = transform.position + transform.forward * 5f;
    }

    // --- UPDATED: Performance Tracking ---
    void Update()
    {
        _perfWatch.Restart(); // Start Timer

        if (myHealth != null && myHealth.isDowned)
        {
            if (navMeshAgent != null && navMeshAgent.enabled && !navMeshAgent.isStopped)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.ResetPath();
            }
            _perfWatch.Stop();
            LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
            return;
        }

        if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != this.gameObject)
        {
            _perfWatch.Stop();
            LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
            return;
        }

        if (clickLockoutFrames > 0) clickLockoutFrames--;
        if (Input.GetKeyDown(KeyCode.X)) ToggleMovementMode();
        if (Input.GetKeyDown(KeyCode.H) && partyAIManager != null) partyAIManager.IssueCommandFollow();

        if (isDodging || IsSpecialMovementActive)
        {
            _perfWatch.Stop();
            LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
            return;
        }

        if (abilityHolder != null && abilityHolder.IsAnimationLocked)
        {
            _perfWatch.Stop();
            LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
            return;
        }

        if (abilityHolder != null && abilityHolder.ActiveBeam != null)
        {
            if (navMeshAgent.hasPath) navMeshAgent.ResetPath();
            RotateTowardsMouse(true);
            _perfWatch.Stop();
            LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
            return;
        }

        if (IsMovingToAttack && TargetObject == null && !navMeshAgent.pathPending)
        {
            if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance) IsMovingToAttack = false;
        }

        if (isFacingLocked && (TargetObject == null || IsTargetDead(TargetObject))) isFacingLocked = false;
        if (UIInteractionState.IsUIBlockingInput || EventSystem.current.IsPointerOverGameObject() || IsUIDragInProgress())
        {
            _perfWatch.Stop();
            LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextDodgeTime)
        {
            StartCoroutine(DodgeRollCoroutine());
            _perfWatch.Stop();
            LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
            return;
        }

        HandleMovementInput();

        _perfWatch.Stop(); // Stop Timer
        LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
    }

    void LateUpdate()
    {
        if (!this.enabled || IsSpecialMovementActive || isDodging) return;
        if (myHealth != null && myHealth.isDowned) return;

        HandleRotation();
        UpdateAnimator();
        UpdateHeadLookLogic();
    }

    private void UpdateHeadLookLogic()
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        int groundLayerMask = LayerMask.GetMask("Terrain", "Water");

        Vector3 targetLookPos = transform.position + transform.forward * 5f;

        if (TargetObject != null)
        {
            targetLookPos = TargetObject.transform.position;
        }
        else if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayerMask))
        {
            targetLookPos = hit.point;
        }

        targetLookPos.y = transform.position.y + headLookHeight;
        CurrentLookTarget = targetLookPos;
        CurrentHeadLookPosition = Vector3.Lerp(CurrentHeadLookPosition, CurrentLookTarget, Time.deltaTime * headLookSpeed);

        float targetWeight = (isDodging || (myHealth != null && myHealth.isDowned)) ? 0f : headLookWeight;
        CurrentHeadLookWeight = Mathf.Lerp(CurrentHeadLookWeight, targetWeight, Time.deltaTime * headWeightBlendSpeed);
    }

    private void HandleInteractionClick()
    {
        if (myHealth != null && myHealth.isDowned) return;

        bool isCommand = Input.GetKey(KeyCode.LeftControl);

        if (abilityHolder != null) abilityHolder.CancelCast(true);

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] allHits = Physics.RaycastAll(ray, 100f);
        Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));

        GameObject target = null;
        Vector3 hitPoint = Vector3.zero;

        foreach (var h in allHits)
        {
            if (h.collider.GetComponent<IInteractable>() != null || h.collider.GetComponent<DoorController>() != null)
            {
                target = h.collider.gameObject;
                hitPoint = h.point;
                break;
            }
        }

        if (target == null)
        {
            if (Physics.Raycast(ray, out RaycastHit standardHit, 100f, interactionLayers, QueryTriggerInteraction.Collide))
            {
                target = standardHit.collider.gameObject;
                hitPoint = standardHit.point;
            }
        }

        if (target != null)
        {
            if (isCommand)
            {
                if (target.layer == LayerMask.NameToLayer("Enemy")) partyAIManager.IssueCommandAttack(target);
                else if (target.layer == LayerMask.NameToLayer("Terrain")) partyAIManager.IssueCommandMoveTo(hitPoint);
                return;
            }

            if (interactionCoroutine != null) StopCoroutine(interactionCoroutine);
            IsMovingToAttack = false;

            if (target.GetComponent<DoorController>() != null || target.GetComponent<IInteractable>() != null)
            {
                interactionCoroutine = StartCoroutine(MoveToInteract(target));
            }
            else if (target.layer == LayerMask.NameToLayer("Enemy") || target.layer == LayerMask.NameToLayer("Friendly"))
            {
                StartFollowingTarget(target);
            }
            else if (target.layer == LayerMask.NameToLayer("Terrain"))
            {
                isFacingLocked = false;
                TargetObject = null;
                navMeshAgent.stoppingDistance = 0f;
                navMeshAgent.SetDestination(hitPoint);
                clickLockoutFrames = 2;
            }
        }
    }

    private void HandleHoldMovement()
    {
        if (myHealth != null && myHealth.isDowned) return;
        if (clickLockoutFrames > 0) return;
        if (Input.GetKey(KeyCode.LeftControl)) return;

        if (abilityHolder != null) abilityHolder.CancelCast(true);

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactionLayers))
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Terrain"))
            {
                if (TargetObject != null) { StopAllCoroutines(); TargetObject = null; }
                isFacingLocked = false;
                IsMovingToAttack = false;
                navMeshAgent.stoppingDistance = 0f;
                navMeshAgent.SetDestination(hit.point);
            }
        }
    }

    private bool IsTargetDead(GameObject target)
    {
        if (target == null) return true;
        Health h = target.GetComponent<Health>();
        if (h == null) h = target.GetComponentInParent<Health>();
        return h != null && (h.currentHealth <= 0 || h.isDowned);
    }

    private void ToggleMovementMode() { if (PartyManager.instance != null) PartyManager.instance.ToggleMovementMode(); }

    public void OnMovementModeChanged(MovementMode newMode)
    {
        if (newMode == MovementMode.WASD)
        {
            if (navMeshAgent.hasPath) navMeshAgent.ResetPath();
            navMeshAgent.updatePosition = false;
        }
        else
        {
            navMeshAgent.Warp(transform.position);
            navMeshAgent.updatePosition = true;
        }
    }

    void OnEnable()
    {
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.updateRotation = false;
            navMeshAgent.speed = wasdMoveSpeed;
        }

        if (playerEquipment != null) playerEquipment.OnEquipmentChanged += HandleEquipmentChanged;
        if (PartyManager.instance != null) OnMovementModeChanged(PartyManager.instance.currentMovementMode);
    }

    void OnDisable() { if (navMeshAgent != null && navMeshAgent.isOnNavMesh) { navMeshAgent.isStopped = true; navMeshAgent.ResetPath(); navMeshAgent.updatePosition = true; } StopAllCoroutines(); IsMovingToAttack = false; if (playerEquipment != null) playerEquipment.OnEquipmentChanged -= HandleEquipmentChanged; }
    private void UpdateAnimator() { if (animator == null) return; Vector3 worldVelocity = (currentMode == MovementMode.WASD && wasdVelocity.sqrMagnitude > 0.01f) ? wasdVelocity : navMeshAgent.velocity; Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity); float speed = navMeshAgent.speed; float vZ = localVelocity.z / speed; float vX = localVelocity.x / speed; if (Mathf.Abs(vZ) < 0.05f) vZ = 0f; if (Mathf.Abs(vX) < 0.05f) vX = 0f; animator.SetFloat(velocityZHash, vZ, animationDampTime, Time.deltaTime); animator.SetFloat(velocityXHash, vX, animationDampTime, Time.deltaTime); }

    private IEnumerator DodgeRollCoroutine()
    {
        isDodging = true;
        nextDodgeTime = Time.time + dodgeDuration + dodgeCooldown;

        abilityHolder.CancelCast(false);

        StopMovement();
        isFacingLocked = false;
        IsMovingToAttack = false;
        if (interactionCoroutine != null) StopCoroutine(interactionCoroutine);
        RotateTowardsMouse(true);
        if (animator != null) animator.SetTrigger(dodgeHash);
        Vector3 startPosition = transform.position;
        Vector3 destination = startPosition + transform.forward * dodgeDistance;
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) destination = new Vector3(hit.position.x, startPosition.y, hit.position.z); else destination = startPosition;
        if (navMeshAgent.isOnNavMesh) navMeshAgent.enabled = false;
        if (mainCollider != null) mainCollider.enabled = false;
        float elapsedTime = 0f;
        while (elapsedTime < dodgeDuration) { transform.position = Vector3.Lerp(startPosition, destination, elapsedTime / dodgeDuration); elapsedTime += Time.deltaTime; yield return null; }
        transform.position = destination;
        if (mainCollider != null) mainCollider.enabled = true;
        navMeshAgent.enabled = true;
        if (navMeshAgent.isOnNavMesh) navMeshAgent.Warp(transform.position);
        isDodging = false;
    }

    private void ProcessWasdInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        if ((horizontal != 0 || vertical != 0))
        {
            if (navMeshAgent.hasPath) navMeshAgent.ResetPath();
            IsMovingToAttack = false;
            isFacingLocked = false;
            if (abilityHolder != null) abilityHolder.CancelCast(true);
        }

        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraRight = mainCamera.transform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();
        Vector3 moveDirection = (cameraForward * vertical) + (cameraRight * horizontal);
        moveDirection.Normalize();
        wasdVelocity = moveDirection * wasdMoveSpeed;
        navMeshAgent.Move(wasdVelocity * Time.deltaTime);
        transform.position = navMeshAgent.nextPosition;
        if (Input.GetMouseButtonDown(0)) HandleInteractionClick();
    }

    private void RotateTowardsMouse(bool forceRotation = false)
    {
        frameSkipCounter++;
        if (frameSkipCounter < 2 && !forceRotation) return;
        frameSkipCounter = 0;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        int groundLayerMask = LayerMask.GetMask("Terrain", "Water");

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayerMask))
        {
            Vector3 directionToMouse = (hit.point - transform.position).normalized;
            directionToMouse.y = 0;

            if (directionToMouse != Vector3.zero)
            {
                float angleToMouse = Vector3.Angle(transform.forward, directionToMouse);

                if (forceRotation || angleToMouse > bodyTurnThreshold)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToMouse);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * bodyTurnSpeed);
                }
            }
        }
    }

    private void HandleMovementInput() { if (currentMode == MovementMode.PointAndClick && !IsGroundTargeting) { if (Input.GetMouseButtonDown(0)) HandleInteractionClick(); else if (Input.GetMouseButton(0)) HandleHoldMovement(); } else if (currentMode == MovementMode.WASD) ProcessWasdInput(); }

    private IEnumerator FollowAndUseAbility()
    {
        if (TargetObject == null) yield break;
        IsMovingToAttack = true;

        if (TargetObject.TryGetComponent<NPCInteraction>(out var npc))
        {
            interactionCoroutine = StartCoroutine(MoveToInteract(TargetObject));
            yield return interactionCoroutine;
            IsMovingToAttack = false;
            TargetObject = null;
            yield break;
        }

        Ability abilityToUse = queuedAbility != null ? queuedAbility : defaultAttackAbility;
        if (abilityToUse == null) { IsMovingToAttack = false; yield break; }

        navMeshAgent.stoppingDistance = abilityToUse.range;

        WaitForSeconds pathUpdateDelay = new WaitForSeconds(0.2f);

        while (Vector3.Distance(transform.position, TargetObject.transform.position) > abilityToUse.range)
        {
            if (IsTargetDead(TargetObject))
            {
                TargetObject = null;
                queuedAbility = null;
                IsMovingToAttack = false;
                isFacingLocked = false;
                yield break;
            }

            navMeshAgent.SetDestination(TargetObject.transform.position);
            yield return pathUpdateDelay;
        }

        navMeshAgent.ResetPath();
        if (abilityHolder != null)
        {
            abilityHolder.UseAbility(abilityToUse, TargetObject);
            isFacingLocked = true;
            if (queuedAbility != null) queuedAbility = null;
        }
        IsMovingToAttack = false;
    }

    private void HandleRotation()
    {
        if (isFacingLocked && TargetObject != null)
        {
            Vector3 dir = (TargetObject.transform.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
        }
        else if (IsMovingToAttack && TargetObject != null)
        {
            Vector3 dir = (TargetObject.transform.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * bodyTurnSpeed);
        }
        else
        {
            RotateTowardsMouse(false);
        }
    }

    public void StartFollowingTarget(GameObject newTarget, Ability abilityToQueue = null) { if (newTarget == null) return; TargetObject = newTarget; queuedAbility = abilityToQueue; if (interactionCoroutine != null) StopCoroutine(interactionCoroutine); StartCoroutine(FollowAndUseAbility()); }

    private IEnumerator MoveToInteract(GameObject targetObject)
    {
        if (targetObject == null) yield break;

        Vector3 destination = targetObject.transform.position;
        float currentInteractionRange = interactionRange;

        if (targetObject.TryGetComponent<DoorController>(out var door))
        {
            if (door.interactionPoint != null) destination = door.interactionPoint.position;
            currentInteractionRange = door.activationDistance;
        }
        else if (targetObject.TryGetComponent<WorldMapExit>(out var exit))
        {
            if (exit.interactionPoint != null) destination = exit.interactionPoint.position;
            currentInteractionRange = exit.activationDistance;
        }
        else if (targetObject.TryGetComponent<DungeonExit>(out var dungeonExit))
        {
            currentInteractionRange = dungeonExit.interactionDistance;
        }
        else if (targetObject.TryGetComponent<NPCInteraction>(out var npc))
        {
            Vector3 targetSpot = npc.GetInteractionPosition();
            if (NavMesh.SamplePosition(targetSpot, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) destination = hit.position;
            else destination = targetSpot;
            currentInteractionRange = 0.2f;
        }

        navMeshAgent.stoppingDistance = currentInteractionRange;
        navMeshAgent.SetDestination(destination);

        while (Vector3.Distance(transform.position, destination) > currentInteractionRange)
        {
            if (targetObject == null)
            {
                navMeshAgent.ResetPath();
                yield break;
            }
            yield return null;
        }

        Vector3 dir = (targetObject.transform.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);

        navMeshAgent.ResetPath();

        if (targetObject.TryGetComponent<IInteractable>(out var interactable))
            interactable.Interact(this.gameObject);
        else if (targetObject.TryGetComponent<DoorController>(out var doorComponent))
            doorComponent.UseDoor();

        interactionCoroutine = null;
    }

    private void HandleEquipmentChanged(EquipmentType slotType) { if (slotType == EquipmentType.LeftHand || slotType == EquipmentType.RightHand) UpdateWeaponTypeParameter(); }
    public void UpdateWeaponTypeParameter() { if (animator == null || playerEquipment == null) return; int weaponType = 0; playerEquipment.equippedItems.TryGetValue(EquipmentType.RightHand, out var rightHandItem); playerEquipment.equippedItems.TryGetValue(EquipmentType.LeftHand, out var leftHandItem); var rightWeaponStats = rightHandItem?.itemData.stats as ItemWeaponStats; var leftWeaponStats = leftHandItem?.itemData.stats as ItemWeaponStats; if ((rightWeaponStats != null && rightWeaponStats.handed == ItemWeaponStats.Handed.TwoHanded) || (leftWeaponStats != null && leftWeaponStats.handed == ItemWeaponStats.Handed.TwoHanded)) weaponType = 2; else if (rightWeaponStats != null || leftWeaponStats != null) weaponType = 1; animator.SetInteger(weaponTypeHash, weaponType); }
    private bool IsUIDragInProgress() { if (uiManagerObject != null && Variables.Object(uiManagerObject).IsDefined(dragInProgressVariableName)) return Variables.Object(uiManagerObject).Get<bool>(dragInProgressVariableName); return false; }
    public void StopMovement() { if (navMeshAgent.hasPath) navMeshAgent.ResetPath(); }
    public IEnumerator ResetGroundTargetingFlag() { while (Input.GetMouseButton(0)) yield return null; yield return null; IsGroundTargeting = false; }
    public void ExecuteLeap(Vector3 destination, Action onLandAction) { if (movementHandler == null) return; StopAllCoroutines(); if (navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath(); IsMovingToAttack = false; TargetObject = null; queuedAbility = null; movementHandler.ExecuteLeap(destination, onLandAction); }
    public void InitiateCharge(GameObject target, Ability chargeAbility) { if (movementHandler == null || !abilityHolder.CanUseAbility(chargeAbility, target)) return; abilityHolder.PayCostAndStartCooldown(chargeAbility); StopAllCoroutines(); movementHandler.ExecuteCharge(target, chargeAbility); }
    public void ExecuteTeleport(Vector3 destination) { StopAllCoroutines(); if (navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath(); IsMovingToAttack = false; TargetObject = null; queuedAbility = null; if (navMeshAgent.isOnNavMesh) navMeshAgent.Warp(destination); else transform.position = destination; }
}