using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controla la rutina de patrullaje de un NPC usando NavMesh y sincroniza su animación.
/// El nombre de la clase coincide con el nombre del archivo 'NavMeshIA' para compatibilidad en Unity.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class NavMeshIA : MonoBehaviour
{
    [Header("Puntos de destino")]
    [Tooltip("Puntos de patrullaje por los cuales el NPC se moverá en bucle.")]
    public Transform[] puntos;

    [Header("Configuración")]
    [Tooltip("Tiempo de espera en segundos al llegar a cada punto.")]
    public float tiempoEspera = 2f;

    [Header("Animación")]
    [Tooltip("Nombre del parámetro Float en el Animator que controla Idle/Walk.")]
    public string parametroVelocidad = "Speed";

    [Tooltip("Suaviza la transición de velocidad en el Animator.")]
    public float suavizadoAnimacion = 0.15f;

    private NavMeshAgent agent;
    private Animator animator;
    private int indice = 0;
    private bool esperando = false;
    private int velocidadParamHash = 0;
    private bool tieneParametroVelocidad = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (agent != null)
        {
            agent.updateRotation = true;
            agent.updateUpAxis = false;
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void Start()
    {
        if (puntos != null && puntos.Length > 0)
        {
            IrAlSiguientePunto();
        }
        else
        {
            // NPC estático sin puntos de patrullaje asignados: permanece quieto en Idle
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
        }
    }

    private void Update()
    {
        SincronizarAnimacion();

        if (puntos == null || puntos.Length == 0) return;
        if (esperando || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        // Comprobar si llegó a su destino actual
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f)
            {
                esperando = true;
                Invoke(nameof(IrAlSiguientePunto), tiempoEspera);
            }
        }
    }

    private bool parametroComprobado = false;

    private void VerificarParametroVelocidad()
    {
        // Esperar a que el Animator esté inicializado: antes de eso 'parameters' puede venir vacío.
        if (animator == null || !animator.isActiveAndEnabled || !animator.isInitialized) return;
        parametroComprobado = true;

        if (animator.runtimeAnimatorController == null || string.IsNullOrEmpty(parametroVelocidad))
        {
            tieneParametroVelocidad = false;
            return;
        }

        velocidadParamHash = Animator.StringToHash(parametroVelocidad);
        tieneParametroVelocidad = false;

        foreach (var param in animator.parameters)
        {
            if (param.nameHash == velocidadParamHash)
            {
                tieneParametroVelocidad = true;
                break;
            }
        }
    }

    private void SincronizarAnimacion()
    {
        if (animator == null) return;
        if (!parametroComprobado) VerificarParametroVelocidad();
        if (!tieneParametroVelocidad) return;

        float velocidadActual = (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) ? agent.velocity.magnitude : 0f;
        animator.SetFloat(velocidadParamHash, velocidadActual, suavizadoAnimacion, Time.deltaTime);
    }

    private void IrAlSiguientePunto()
    {
        esperando = false;

        if (puntos == null || puntos.Length == 0 || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        // Filtrar posibles puntos nulos en la lista
        int intentos = 0;
        while (puntos[indice] == null && intentos < puntos.Length)
        {
            indice = (indice + 1) % puntos.Length;
            intentos++;
        }

        if (puntos[indice] != null)
        {
            agent.isStopped = false;
            agent.SetDestination(puntos[indice].position);
            indice = (indice + 1) % puntos.Length;
        }
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(IrAlSiguientePunto));
        esperando = false;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        if (animator != null && tieneParametroVelocidad)
        {
            animator.SetFloat(velocidadParamHash, 0f);
        }
    }

    private void OnDestroy()
    {
        CancelInvoke();
    }
}

/// <summary>
/// Alias para mantener retrocompatibilidad en caso de referencias históricas al nombre anterior.
/// </summary>
public class NpcRutina : NavMeshIA { }
