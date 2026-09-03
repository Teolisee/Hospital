using UnityEngine;
using UnityEngine.InputSystem;

// -----------------------------------------------------------------------------
// SCRIPT: RE_PlayerMovement
// METÁFORA: "Las Piernas y el Motor del Jugador"
// Controla el movimiento en 3ª persona: caminar, correr, saltar, gravedad,
// rotación suave en base a la cámara y sincronización con el Animator.
// -----------------------------------------------------------------------------
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

    [Tooltip("Velocidad con la que el personaje gira hacia la dirección de movimiento.")]
    public float rotationSpeed = 12.0f;

    [Header("Animación (Opcional)")]
    [Tooltip("Componente Animator del modelo 3D (se detecta automáticamente si está vacío).")]
    public Animator animator;

    [Tooltip("Nombre del parámetro Float en el Animator para controlar la velocidad (ej: 'Speed').")]
    public string speedParameterName = "Speed";

    // Componentes internos
    private CharacterController controller;
    private float currentSpeed;
    private float speedSmoothVelocity;
    private Vector3 verticalVelocity; // Maneja la gravedad y el salto
    private bool isGrounded;

    private void Start() 
    {
        // 1. Obtenemos el CharacterController del jugador
        controller = GetComponent<CharacterController>(); 

        // 2. Si no asignamos la cámara en el Inspector, buscamos la cámara principal automáticamente
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // 3. Si no asignamos el Animator, lo buscamos en este objeto o en los hijos (donde suele estar el modelo 3D)
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update() 
    {
        // ---------------------------------------------------------
        // PASO 1: DETECTAR SI ESTAMOS TOCANDO EL PISO
        // ---------------------------------------------------------
        isGrounded = controller.isGrounded;

        // Si estamos en el suelo y caíamos, mantenemos una pequeña fuerza hacia abajo para pegarnos a rampas y escalones
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }

        // ---------------------------------------------------------
        // PASO 2: LEER ENTRADAS DEL JUGADOR (Teclado y Gamepad)
        // ---------------------------------------------------------
        float horizontalInput = 0f;
        float verticalInput = 0f;
        bool isSprinting = false;
        bool jumpPressed = false;

        // Soporte Teclado (Nuevo Input System)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontalInput += 1f; 
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontalInput -= 1f;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) verticalInput += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) verticalInput -= 1f;

            if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed) isSprinting = true;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) jumpPressed = true;
        }

        // Soporte Legacy Input (Fallback de seguridad)
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

        // Vector de entrada plano en 2D
        Vector2 inputVector = new Vector2(horizontalInput, verticalInput);
        if (inputVector.magnitude > 1f) inputVector.Normalize();

        // ---------------------------------------------------------
        // PASO 3: TRADUCIR LA DIRECCIÓN SEGÚN LA CÁMARA
        // ---------------------------------------------------------
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

        // ---------------------------------------------------------
        // PASO 4: CALCULAR VELOCIDAD Y SUAVIZADO
        // ---------------------------------------------------------
        float targetSpeed = 0f;
        if (inputVector.magnitude > 0.05f)
        {
            targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        }

        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);

        // ---------------------------------------------------------
        // PASO 5: ROTACIÓN SUAVE DEL PERSONAJE
        // ---------------------------------------------------------
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // ---------------------------------------------------------
        // PASO 6: SALTO Y GRAVEDAD
        // ---------------------------------------------------------
        if (jumpPressed && isGrounded)
        {
            // Fórmula física del salto: v = sqrt(h * -2 * g)
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
        }

        // Aplicamos gravedad continuamente
        verticalVelocity.y += gravity * Time.deltaTime;

        // ---------------------------------------------------------
        // PASO 7: MOVER EL CHARACTER CONTROLLER
        // ---------------------------------------------------------
        Vector3 horizontalMovement = moveDirection.normalized * currentSpeed;
        Vector3 totalMovement = horizontalMovement + verticalVelocity;

        controller.Move(totalMovement * Time.deltaTime);

        // ---------------------------------------------------------
        // PASO 8: ACTUALIZAR EL ANIMATOR
        // ---------------------------------------------------------
        if (animator != null)
        {
            // Enviamos la magnitud de velocidad actual al parámetro "Speed" del Animator
            float normalizedSpeed = currentSpeed / (walkSpeed > 0 ? walkSpeed : 1f);
            animator.SetFloat(speedParameterName, normalizedSpeed, 0.1f, Time.deltaTime);
        }
    }

    private void OnDisable()
    {
        // Al pausar o desactivar el movimiento (ej: al hablar con un NPC),
        // reseteamos la velocidad en el Animator para que no se quede caminando en el sitio.
        currentSpeed = 0f;
        if (animator != null)
        {
            animator.SetFloat(speedParameterName, 0f);
        }
    }
}
