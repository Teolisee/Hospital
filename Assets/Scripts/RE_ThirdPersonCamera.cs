using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controla la cámara en tercera persona flotante que sigue al jugador.
/// Implementa anticolisión con muros mediante SphereCast geométrico y soporte para controles.
/// </summary>
public class RE_ThirdPersonCamera : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("El objeto que la cámara va a seguir (generalmente el jugador).")]
    public Transform target;

    [Header("Posición de Cámara")]
    [Tooltip("La distancia a la que la cámara estará del jugador.")]
    public float distance = 5.0f;
    
    [Tooltip("Altura base hacia donde mira la cámara.")]
    public float height = 1.5f;
    
    [Tooltip("Desplazamiento lateral de la cámara.")]
    public Vector3 offset = Vector3.zero;

    [Header("Sensibilidad")]
    [Tooltip("Velocidad de rotación usando el ratón.")]
    public float mouseSensitivity = 2.0f;

    [Header("Límites de Rotación (Vertical)")]
    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    [Header("Colisiones (Raycast)")]
    [Tooltip("Capas que bloquearán la cámara, acercándola al jugador.")]
    public LayerMask collisionLayers;
    
    [Tooltip("Radio de la esfera de la cámara para evitar que atraviese paredes finas.")]
    public float cameraRadius = 0.3f;

    private float currentX = 0.0f;
    private float currentY = 0.0f;
    private float currentDistance;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; 
        Cursor.visible = false; 

        currentDistance = distance;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                RE_PlayerMovement pm = FindFirstObjectByType<RE_PlayerMovement>();
                if (pm != null) player = pm.gameObject;
            }
            if (player != null) target = player.transform;
        }

        Vector3 angles = transform.eulerAngles;
        currentX = angles.y;
        currentY = angles.x;

        // Si no se asignaron capas de colisión, usar todo excepto Trigger y Player
        if (collisionLayers.value == 0)
        {
            int playerLayer = target != null ? target.gameObject.layer : LayerMask.NameToLayer("Default");
            collisionLayers = ~(1 << LayerMask.NameToLayer("Ignore Raycast") | (1 << playerLayer));
        }
    }

    private void LateUpdate() 
    {
        if (target == null) return;

        // Si el ratón no está bloqueado (menús o diálogos activos), no rotar
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseX = 0f;
            float mouseY = 0f;

            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                mouseX += delta.x * mouseSensitivity * 0.1f;
                mouseY -= delta.y * mouseSensitivity * 0.1f;
            }

            currentX += mouseX;
            currentY += mouseY;
            currentY = Mathf.Clamp(currentY, yMinLimit, yMaxLimit);
        }

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 targetPosition = target.position + Vector3.up * height + rotation * offset;

        // Dirección deseada desde el objetivo hacia donde debería estar la cámara
        Vector3 desiredDirection = -(rotation * Vector3.forward);

        // Comprobación de colisiones con muros (SphereCast hacia atrás)
        float targetDistance = distance;
        RaycastHit hit;

        if (Physics.SphereCast(targetPosition, cameraRadius, desiredDirection, out hit, distance, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Clamp(hit.distance - cameraRadius, 0.5f, distance);
        }

        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * 15f);

        Vector3 finalPosition = targetPosition + desiredDirection * currentDistance;

        transform.position = finalPosition;
        transform.rotation = rotation;
    }
}
