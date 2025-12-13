using UnityEngine;
using UnityEngine.AI;
using Unity.VisualScripting;
using System.Collections;
using UnityEngine.EventSystems;
using DG.Tweening;
using System;

public class PlayerMovement : MonoBehaviour, IMovementHandler
{
    public enum MovementMode { PointAndClick, WASD }
    [Header("Movement")]
    public MovementMode currentMode => (PartyManager.instance != null)
        ? PartyManager.instance.currentMovementMode
        : MovementMode.PointAndClick;
    public float wasdMoveSpeed;

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
    [Header("Game State References")]
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

    // --- Optimization: Hashes ---
    private int velocityZHash;
    private int velocityXHash;
    private int weaponTypeHash;
    private int dodgeHash;

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
            abilityHolder = root.PlayerAbilityHolder;
            animator = root.Animator;
            playerEquipment = root.PlayerEquipment;
        }

        // Cache Hashes
        velocityZHash = Animator.StringToHash("VelocityZ");
        velocityXHash = Animator.StringToHash("VelocityX");
        weaponTypeHash = Animator.StringToHash("WeaponType");
        dodgeHash = Animator.StringToHash(!string.IsNullOrEmpty(dodgeAnimationTrigger) ? dodgeAnimationTrigger : "DodgeRoll");
    }

    void Start()
    {
        wasdMoveSpeed = navMeshAgent.speed;
        UpdateWeaponTypeParameter();
    }

    void Update()
    {
        if (clickLockoutFrames > 0) { clickLockoutFrames--; }
        if (Input.GetKeyDown(KeyCode.X)) { ToggleMovementMode(); }
        if (Input.GetKeyDown(KeyCode.H)) { if (partyAIManager != null) { partyAIManager.IssueCommandFollow(); } }

        if (isDodging || IsSpecialMovementActive) return;

        // --- FIX: Respect Animation Lock ---
        if (abilityHolder != null && abilityHolder.IsAnimationLocked) return;
        // ----------------------------------

        if (abilityHolder != null && abilityHolder.ActiveBeam != null)
        {
            if (navMeshAgent.hasPath) navMeshAgent.ResetPath();
            return;
        }
        if (IsMovingToAttack && TargetObject == null && !navMeshAgent.pathPending) { if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance) { IsMovingToAttack = false; } }

        if (isFacingLocked && (TargetObject == null || (TargetObject.GetComponent<Health>() != null && TargetObject.GetComponent<Health>().currentHealth <= 0)))
        {
            isFacingLocked = false;
        }

        if (UIInteractionState.IsUIBlockingInput || EventSystem.current.IsPointerOverGameObject() || IsUIDragInProgress()) { return; }

        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextDodgeTime)
        {
            StartCoroutine(DodgeRollCoroutine());
            return;
        }

        HandleMovementInput();
    }

    private void ToggleMovementMode()
    {
        if (PartyManager.instance != null)
        {
            PartyManager.instance.ToggleMovementMode();
        }
    }

    public void OnMovementModeChanged(MovementMode newMode)
    {
        if (newMode == MovementMode.WASD)
        {
            if (navMeshAgent.hasPath) { navMeshAgent.ResetPath(); }
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
        }
        if (playerEquipment != null) { playerEquipment.OnEquipmentChanged += HandleEquipmentChanged; }

        if (PartyManager.instance != null)
        {
            OnMovementModeChanged(PartyManager.instance.currentMovementMode);
        }
    }

    void OnDisable()
    {
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
            navMeshAgent.updatePosition = true; // Reset for AI control
        }
        StopAllCoroutines();
        IsMovingToAttack = false;
        if (playerEquipment != null) { playerEquipment.OnEquipmentChanged -= HandleEquipmentChanged; }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        Vector3 worldVelocity;

        // Use wasdVelocity if actively pressing keys in WASD mode
        if (currentMode == MovementMode.WASD && wasdVelocity.sqrMagnitude > 0.01f)
        {
            worldVelocity = wasdVelocity;
        }
        else
        {
            // Fallback to navMeshAgent velocity (handles point-click AND passive movement)
            worldVelocity = navMeshAgent.velocity;
        }

        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);
        float speed = navMeshAgent.speed;

        float vZ = localVelocity.z / speed;
        float vX = localVelocity.x / speed;

        // Deadzone check
        if (Mathf.Abs(vZ) < 0.05f) vZ = 0f;
        if (Mathf.Abs(vX) < 0.05f) vX = 0f;

        animator.SetFloat(velocityZHash, vZ, animationDampTime, Time.deltaTime);
        animator.SetFloat(velocityXHash, vX, animationDampTime, Time.deltaTime);
    }

    private IEnumerator DodgeRollCoroutine()
    {
        isDodging = true;
        nextDodgeTime = Time.time + dodgeDuration + dodgeCooldown;

        abilityHolder.CancelCast();
        StopMovement();
        isFacingLocked = false;
        IsMovingToAttack = false;
        if (interactionCoroutine != null) StopCoroutine(interactionCoroutine);

        RotateTowardsMouse();

        if (animator != null)
        {
            animator.SetTrigger(dodgeHash);
        }

        Vector3 startPosition = transform.position;
        Vector3 destination = startPosition + transform.forward * dodgeDistance;

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            destination = new Vector3(hit.position.x, startPosition.y, hit.position.z);
        }
        else
        {
            destination = startPosition;
        }

        if (navMeshAgent.isOnNavMesh) navMeshAgent.enabled = false;
        if (mainCollider != null) mainCollider.enabled = false;

        float elapsedTime = 0f;
        while (elapsedTime < dodgeDuration)
        {
            transform.position = Vector3.Lerp(startPosition, destination, elapsedTime / dodgeDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
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
            if (abilityHolder != null) abilityHolder.CancelCast();
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

        if (Input.GetMouseButtonDown(0)) { HandleInteractionClick(); }
    }

    private void RotateTowardsMouse() { Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition); int groundLayerMask = LayerMask.GetMask("Terrain", "Water"); if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayerMask)) { transform.LookAt(new Vector3(hit.point.x, transform.position.y, hit.point.z)); } }

    // --- Unchanged Methods ---
    void LateUpdate() { if (!this.enabled || IsSpecialMovementActive || isDodging) return; HandleRotation(); UpdateAnimator(); }
    private void HandleMovementInput() { if (currentMode == MovementMode.PointAndClick && !IsGroundTargeting) { if (Input.GetMouseButtonDown(0)) { HandleInteractionClick(); } else if (Input.GetMouseButton(0)) { HandleHoldMovement(); } } else if (currentMode == MovementMode.WASD) { ProcessWasdInput(); } }
    private void HandleInteractionClick() { bool isCommand = Input.GetKey(KeyCode.LeftControl); if (abilityHolder != null) abilityHolder.CancelCast(); Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition); if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactionLayers, QueryTriggerInteraction.Collide)) { GameObject clickedObject = hit.collider.gameObject; if (isCommand) { if (clickedObject.layer == LayerMask.NameToLayer("Enemy")) { partyAIManager.IssueCommandAttack(clickedObject); } else if (clickedObject.layer == LayerMask.NameToLayer("Terrain")) { partyAIManager.IssueCommandMoveTo(hit.point); } return; } if (interactionCoroutine != null) StopCoroutine(interactionCoroutine); IsMovingToAttack = false; if (clickedObject.GetComponent<DoorController>() != null || clickedObject.GetComponent<IInteractable>() != null) { interactionCoroutine = StartCoroutine(MoveToInteract(clickedObject)); } else if (clickedObject.layer == LayerMask.NameToLayer("Enemy") || clickedObject.layer == LayerMask.NameToLayer("Friendly")) { StartFollowingTarget(clickedObject); } else if (clickedObject.layer == LayerMask.NameToLayer("Terrain")) { isFacingLocked = false; TargetObject = null; navMeshAgent.stoppingDistance = 0f; navMeshAgent.SetDestination(hit.point); clickLockoutFrames = 2; } } }
    private void HandleHoldMovement() { if (clickLockoutFrames > 0) return; if (Input.GetKey(KeyCode.LeftControl)) return; if (abilityHolder != null) abilityHolder.CancelCast(); Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition); if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactionLayers)) { if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Terrain")) { if (TargetObject != null) { StopAllCoroutines(); TargetObject = null; } isFacingLocked = false; IsMovingToAttack = false; navMeshAgent.stoppingDistance = 0f; navMeshAgent.SetDestination(hit.point); } } }
    private IEnumerator FollowAndUseAbility() { if (TargetObject == null) yield break; IsMovingToAttack = true; if (TargetObject.TryGetComponent<NPCInteraction>(out var npc)) { interactionCoroutine = StartCoroutine(MoveToInteract(TargetObject)); yield return interactionCoroutine; IsMovingToAttack = false; TargetObject = null; yield break; } Ability abilityToUse = queuedAbility != null ? queuedAbility : defaultAttackAbility; if (abilityToUse == null) { IsMovingToAttack = false; yield break; } navMeshAgent.stoppingDistance = abilityToUse.range; while (Vector3.Distance(transform.position, TargetObject.transform.position) > abilityToUse.range) { if (TargetObject == null || (TargetObject.GetComponent<Health>() != null && TargetObject.GetComponent<Health>().currentHealth <= 0)) { TargetObject = null; queuedAbility = null; IsMovingToAttack = false; isFacingLocked = false; yield break; } navMeshAgent.SetDestination(TargetObject.transform.position); yield return null; } navMeshAgent.ResetPath(); if (abilityHolder != null) { abilityHolder.UseAbility(abilityToUse, TargetObject); isFacingLocked = true; if (queuedAbility != null) { queuedAbility = null; } } IsMovingToAttack = false; }
    private void HandleRotation() { if (isFacingLocked && TargetObject != null) { transform.LookAt(new Vector3(TargetObject.transform.position.x, transform.position.y, TargetObject.transform.position.z)); } else if (IsMovingToAttack && TargetObject != null) { transform.LookAt(new Vector3(TargetObject.transform.position.x, transform.position.y, TargetObject.transform.position.z)); } else { RotateTowardsMouse(); } }
    public void StartFollowingTarget(GameObject newTarget, Ability abilityToQueue = null) { if (newTarget == null) return; TargetObject = newTarget; queuedAbility = abilityToQueue; if (interactionCoroutine != null) StopCoroutine(interactionCoroutine); StartCoroutine(FollowAndUseAbility()); }

    // --- UPDATED METHOD ---
    private IEnumerator MoveToInteract(GameObject targetObject)
    {
        if (targetObject == null) yield break;

        Vector3 destination = targetObject.transform.position;
        float currentInteractionRange = interactionRange; // Default global range

        // Check for interaction overrides
        if (targetObject.TryGetComponent<DoorController>(out var door))
        {
            // If the door has a specific interaction point, use it
            if (door.interactionPoint != null)
            {
                destination = door.interactionPoint.position;
            }
            // Use the specific activation distance from the door script
            currentInteractionRange = door.activationDistance;
        }
        else if (targetObject.TryGetComponent<WorldMapExit>(out var exit))
        {
            // If the exit has a specific interaction point, use it
            if (exit.interactionPoint != null)
            {
                destination = exit.interactionPoint.position;
            }
            // Use the specific activation distance from the exit script
            currentInteractionRange = exit.activationDistance;
        }

        navMeshAgent.stoppingDistance = currentInteractionRange;
        navMeshAgent.SetDestination(destination);

        // Wait until we are close enough
        while (Vector3.Distance(transform.position, destination) > currentInteractionRange)
        {
            if (targetObject == null)
            {
                navMeshAgent.ResetPath();
                yield break;
            }
            yield return null;
        }

        // We arrived!
        navMeshAgent.ResetPath();

        if (targetObject.TryGetComponent<IInteractable>(out var interactable))
        {
            interactable.Interact(this.gameObject);
        }
        else if (targetObject.TryGetComponent<DoorController>(out var doorComponent))
        {
            doorComponent.UseDoor();
        }

        interactionCoroutine = null;
    }
    // ---------------------

    private void HandleEquipmentChanged(EquipmentType slotType) { if (slotType == EquipmentType.LeftHand || slotType == EquipmentType.RightHand) UpdateWeaponTypeParameter(); }
    public void UpdateWeaponTypeParameter() { if (animator == null || playerEquipment == null) return; int weaponType = 0; playerEquipment.equippedItems.TryGetValue(EquipmentType.RightHand, out var rightHandItem); playerEquipment.equippedItems.TryGetValue(EquipmentType.LeftHand, out var leftHandItem); var rightWeaponStats = rightHandItem?.itemData.stats as ItemWeaponStats; var leftWeaponStats = leftHandItem?.itemData.stats as ItemWeaponStats; if ((rightWeaponStats != null && rightWeaponStats.handed == ItemWeaponStats.Handed.TwoHanded) || (leftWeaponStats != null && leftWeaponStats.handed == ItemWeaponStats.Handed.TwoHanded)) { weaponType = 2; } else if (rightWeaponStats != null || leftWeaponStats != null) { weaponType = 1; } animator.SetInteger(weaponTypeHash, weaponType); }
    private bool IsUIDragInProgress() { if (uiManagerObject != null && Variables.Object(uiManagerObject).IsDefined(dragInProgressVariableName)) { return Variables.Object(uiManagerObject).Get<bool>(dragInProgressVariableName); } return false; }
    public void StopMovement() { if (navMeshAgent.hasPath) { navMeshAgent.ResetPath(); } }
    public IEnumerator ResetGroundTargetingFlag() { while (Input.GetMouseButton(0)) { yield return null; } yield return null; IsGroundTargeting = false; }
    public void ExecuteLeap(Vector3 destination, Action onLandAction) { if (movementHandler == null) return; StopAllCoroutines(); if (navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath(); IsMovingToAttack = false; TargetObject = null; queuedAbility = null; movementHandler.ExecuteLeap(destination, onLandAction); }
    public void InitiateCharge(GameObject target, Ability chargeAbility) { if (movementHandler == null || !abilityHolder.CanUseAbility(chargeAbility, target)) return; abilityHolder.PayCostAndStartCooldown(chargeAbility); StopAllCoroutines(); movementHandler.ExecuteCharge(target, chargeAbility); }
    public void ExecuteTeleport(Vector3 destination) { StopAllCoroutines(); if (navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath(); IsMovingToAttack = false; TargetObject = null; queuedAbility = null; if (navMeshAgent.isOnNavMesh) { navMeshAgent.Warp(destination); } else { transform.position = destination; } }
}