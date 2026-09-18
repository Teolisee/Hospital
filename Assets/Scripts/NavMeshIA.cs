using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class NpcRutina : MonoBehaviour
{
    [Header("Puntos de destino")]
    public Transform[] puntos;

    [Header("Configuracion")]
    public float tiempoEspera = 2f;

    [Header("Animacion")]
    [Tooltip("Nombre del parametro Float en el Animator que controla Idle/Walk")]
    public string parametroVelocidad = "Speed";
    [Tooltip("Suaviza la transicion de velocidad en el Animator")]
    public float suavizadoAnimacion = 0.15f;

    private NavMeshAgent agent;
    private Animator animator;
    private int indice = 0;
    private bool esperando = false;
    private bool tieneParametroVelocidad = false;
    private bool parametroComprobado = false;

    void Start()
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

        if (puntos != null && puntos.Length > 0)
        {
            IrAlSiguientePunto();
        }
        else
        {
            // NPC estatico sin puntos de patrullaje asignados: permanece quieto en Idle
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
        }
    }

    void Update()
    {
        SincronizarAnimacion();

        if (puntos == null || puntos.Length == 0) return;
        if (esperando || agent == null || !agent.isOnNavMesh) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f)
            {
                esperando = true;
                Invoke(nameof(IrAlSiguientePunto), tiempoEspera);
            }
        }
    }

    void SincronizarAnimacion()
    {
        if (animator == null) return;

        if (!parametroComprobado)
        {
            parametroComprobado = true;
            if (animator.runtimeAnimatorController != null)
            {
                foreach (var param in animator.parameters)
                {
                    if (param.name == parametroVelocidad)
                    {
                        tieneParametroVelocidad = true;
                        break;
                    }
                }
            }
        }

        if (tieneParametroVelocidad)
        {
            float velocidadActual = agent != null ? agent.velocity.magnitude : 0f;
            animator.SetFloat(parametroVelocidad, velocidadActual, suavizadoAnimacion, Time.deltaTime);
        }
    }

    void IrAlSiguientePunto()
    {
        esperando = false;

        if (puntos == null || puntos.Length == 0 || agent == null || !agent.isOnNavMesh) return;

        agent.SetDestination(puntos[indice].position);
        indice = (indice + 1) % puntos.Length;
    }
}
