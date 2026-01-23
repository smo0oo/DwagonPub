using UnityEngine;
using System.Collections;

public class PlayerCameraController : MonoBehaviour
{
    public static PlayerCameraController instance;

    [Header("Target Settings")]
    public Transform cameraTarget;
    public Transform cameraPivot;
    public GameObject cameraObject; // The actual Camera (or holder)

    [Header("Movement Settings")]
    public float moveSmoothTime = 0.1f;
    public float rotateSmoothTime = 0.1f;
    public float mouseSensitivity = 3.0f;

    [Header("Zoom Settings")]
    public float minZoom = 2.0f;
    public float maxZoom = 15.0f;
    public float zoomSpeed = 5.0f;
    public float currentZoom = 10.0f;

    private Vector3 moveVelocity;
    private Vector3 currentRotation;
    private Vector3 targetRotation;
    private float rotationVelocityX;
    private float rotationVelocityY;

    // --- AAA Shake Fix ---
    private Vector3 shakeOffset;
    private Coroutine shakeCoroutine;
    // ---------------------

    void Awake()
    {
        instance = this;
    }

    void OnEnable()
    {
        // Listen for the ability shake event
        PlayerAbilityHolder.OnCameraShakeRequest += TriggerCameraShake;
    }

    void OnDisable()
    {
        PlayerAbilityHolder.OnCameraShakeRequest -= TriggerCameraShake;
    }

    void LateUpdate()
    {
        HandleCameraLogic();
    }

    private void HandleCameraLogic()
    {
        if (cameraTarget == null || cameraPivot == null || cameraObject == null) return;

        // 1. Follow Target
        transform.position = Vector3.SmoothDamp(transform.position, cameraTarget.position, ref moveVelocity, moveSmoothTime);

        // 2. Handle Rotation
        if (Input.GetMouseButton(1)) // Right click to rotate
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            targetRotation.y += mouseX;
            targetRotation.x -= mouseY;
            targetRotation.x = Mathf.Clamp(targetRotation.x, -40, 85);
        }

        currentRotation.x = Mathf.SmoothDampAngle(currentRotation.x, targetRotation.x, ref rotationVelocityX, rotateSmoothTime);
        currentRotation.y = Mathf.SmoothDampAngle(currentRotation.y, targetRotation.y, ref rotationVelocityY, rotateSmoothTime);

        cameraPivot.localRotation = Quaternion.Euler(currentRotation.x, 0, 0);
        transform.rotation = Quaternion.Euler(0, currentRotation.y, 0);

        // 3. Handle Zoom & Shake
        HandleCameraZoom();
    }

    private void HandleCameraZoom()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            currentZoom -= scrollInput * zoomSpeed;
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
        }

        // AAA FIX: Add the shakeOffset to the zoom position.
        // This ensures shake works regardless of how zoomed in/out you are.
        Vector3 targetLocalPos = new Vector3(0, 0, -currentZoom);

        // Apply Zoom + Shake
        cameraObject.transform.localPosition = Vector3.Lerp(cameraObject.transform.localPosition, targetLocalPos, Time.deltaTime * 10f) + shakeOffset;
    }

    public void TriggerCameraShake(float intensity, float duration)
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeRoutine(intensity, duration));
    }

    private IEnumerator ShakeRoutine(float intensity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Generate random offset within a sphere
            shakeOffset = Random.insideUnitSphere * intensity;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Reset
        shakeOffset = Vector3.zero;
    }
}