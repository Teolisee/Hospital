using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controla el movimiento en 3ª persona del jugador: caminar, correr, saltar, gravedad,
/// rotación hacia la dirección de la cámara y sincronización eficiente con el Animator.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class RE_PlayerMovement : MonoBehaviour 
{
    [Header("Velocidades de Movimiento")]
    [Tooltip("Velocidad normal al caminar.")]
    public float walkSpeed = 5.0f;

    [Tooltip("Velocidad al correr (manteniendo Shift).")]
    public float sprintSpeed = 8.5f;

    [Tooltip("Suavizado de aceleración/desaceleración.")]
    public float speedSmoothTime = 0.1f;

    [Header("Salto y Gravedad")]
    [Tooltip("Altura máxima que alcanza el salto en metros.")]
    public float jumpHeight = 1.2f;

    [Tooltip("Fuerza de gravedad hacia abajo.")]
    public float gravity = -20.0f;

    [Header("Cámara y Orientación")]
    [Tooltip("Referencia a la cámara principal para orientar el movimiento.")]
    public Transform cameraTransform;

    [Tooltip("Velocidad de giro del personaje hacia la dirección de movimiento.")]
    public float rotationSpeed = 12.0f;

    [Header("Animación (Opcional)")]
    [Tooltip("Componente Animator del modelo 3D.")]
    public Animator animator;

    [Tooltip("Nombre del parámetro Float en el Animator para controlar la velocidad.")]
    public string speedParameterName = "Speed";

    private CharacterController controller;
    private float currentSpeed;
    private float speedSmoothVelocity;
    private Vector3 verticalVelocity;
    private bool isGrounded;
    private int speedParamHash = 0;
    private bool hasSpeedParameter = false;

    private void Start() 
    {
        controller = GetComponent<CharacterController>(); 

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Nota: no usar '??' con componentes de Unity (el "null falso" del editor rompe el fallback).
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }
    }

    /// <summary>
    /// Verifica (una sola vez, cuando el Animator ya está inicializado) si existe el parámetro de velocidad.
    /// </summary>
    private bool EnsureAnimatorParameter()
    {
        if (hasSpeedParameter) return true;
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(speedParameterName)) return false;
        if (!animator.isActiveAndEnabled || !animator.isInitialized) return false;

        speedParamHash = Animator.StringToHash(speedParameterName);
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == speedParamHash)
            {
                hasSpeedParameter = true;
                break;
            }
        }
        return hasSpeedParameter;
    }

    private void Update() 
    {
        // Si el juego está en pausa o tiempo congelado, no procesar movimiento
        if (Time.timeScale <= 0.0001f) return;

        // 1. Estado en el suelo
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }

        // 2. Lectura de entradas
        float horizontalInput = 0f;
        float verticalInput = 0f;
        bool isSprinting = false;
        bool jumpPressed = false;

        // Soporte Teclado (Input System moderno)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontalInput += 1f; 
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontalInput -= 1f;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) verticalInput += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) verticalInput -= 1f;

            if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed) isSprinting = true;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) jumpPressed = true;
        }

        // Fallback Entrada Tradicional
        try
        {
            if (horizontalInput == 0f) horizontalInput = Input.GetAxisRaw("Horizontal");
            if (verticalInput == 0f) verticalInput = Input.GetAxisRaw("Vertical");
            if (!isSprinting && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) isSprinting = true;
            if (!jumpPressed && Input.GetKeyDown(KeyCode.Space)) jumpPressed = true;
        }
        catch { }

        // Soporte Mando / Gamepad
        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            horizontalInput += stick.x;
            verticalInput += stick.y;

            if (Gamepad.current.leftStickButton.isPressed || Gamepad.current.rightShoulder.isPressed) isSprinting = true;
            if (Gamepad.current.buttonSouth.wasPressedThisFrame) jumpPressed = true;
        }

        Vector2 inputVector = new Vector2(horizontalInput, verticalInput);
        if (inputVector.sqrMagnitude > 1f) inputVector.Normalize();

        // 3. Dirección según la cámara
        Vector3 moveDirection = Vector3.zero;

        if (cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            moveDirection = (camRight * inputVector.x) + (camForward * inputVector.y);
        }
        else
        {
            moveDirection = new Vector3(inputVector.x, 0f, inputVector.y);
        }

        // 4. Velocidad y suavizado
        float targetSpeed = 0f;
        if (inputVector.sqrMagnitude > 0.0025f)
        {
            targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        }

        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);

        // 5. Rotación del personaje
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 6. Salto y gravedad
        if (jumpPressed && isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
        }

        verticalVelocity.y += gravity * Time.deltaTime;

        // 7. Aplicar movimiento
        Vector3 horizontalMovement = moveDirection.normalized * currentSpeed;
        Vector3 totalMovement = horizontalMovement + verticalVelocity;

        controller.Move(totalMovement * Time.deltaTime);

        // 8. Actualizar Animator sin allocs
        if (EnsureAnimatorParameter())
        {
            float normalizedSpeed = currentSpeed / (walkSpeed > 0 ? walkSpeed : 1f);
            animator.SetFloat(speedParamHash, normalizedSpeed, 0.1f, Time.deltaTime);
        }
    }

    private void OnDisable()
    {
        currentSpeed = 0f;
        verticalVelocity = Vector3.zero;

        if (animator != null && hasSpeedParameter && animator.isActiveAndEnabled)
        {
            animator.SetFloat(speedParamHash, 0f);
        }
    }
}
